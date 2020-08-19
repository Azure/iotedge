//! This module contains the I/O types used by the clients.

use std::future::Future;

/// A [`mqtt3::IoSource`] implementation used by the clients.
pub struct IoSource {
	iothub_hostname: std::sync::Arc<str>,
	iothub_host: std::net::SocketAddr,
	authentication: crate::Authentication,
	timeout: std::time::Duration,
	extra: IoSourceExtra,
}

#[derive(Clone, Debug)]
enum IoSourceExtra {
	Raw,

	WebSocket {
		uri: http::Uri,
	},
}

impl IoSource {
	pub(crate) fn new(
		iothub_hostname: std::sync::Arc<str>,
		authentication: crate::Authentication,
		timeout: std::time::Duration,
		transport: crate::Transport,
	) -> Result<Self, crate::CreateClientError> {
		let port = match transport {
			crate::Transport::Tcp => 8883,
			crate::Transport::WebSocket => 443,
		};

		let iothub_host =
			std::net::ToSocketAddrs::to_socket_addrs(&(&*iothub_hostname, port))
			.map_err(|err| crate::CreateClientError::ResolveIotHubHostname(Some(err)))?
			.next()
			.ok_or(crate::CreateClientError::ResolveIotHubHostname(None))?;

		let extra = match transport {
			crate::Transport::Tcp => crate::io::IoSourceExtra::Raw,

			crate::Transport::WebSocket => {
				let uri = match format!("ws://{}/$iothub/websocket", iothub_hostname).parse() {
					Ok(uri) => uri,
					Err(err) => return Err(crate::CreateClientError::WebSocketUrl(err)),
				};

				crate::io::IoSourceExtra::WebSocket {
					uri,
				}
			},
		};

		Ok(IoSource {
			iothub_hostname,
			iothub_host,
			authentication,
			timeout,
			extra,
		})
	}
}

pub const IOTHUB_ENCODE_SET: &percent_encoding::AsciiSet =
	&percent_encoding::CONTROLS // C0 control
	.add(b' ').add(b'"').add(b'<').add(b'>').add(b'`') // fragment
	.add(b'#').add(b'?').add(b'{').add(b'}') // path
	.add(b'=');

impl mqtt3::IoSource for IoSource {
	type Io = Io<tokio_tls::TlsStream<tokio_io_timeout::TimeoutStream<tokio::net::TcpStream>>>;
	type Error = std::io::Error;
	#[allow(clippy::type_complexity)]
	type Future = std::pin::Pin<Box<dyn Future<Output = std::io::Result<(Self::Io, Option<String>)>> + Send>>;

	fn connect(&mut self) -> Self::Future {
		use futures_util::FutureExt;

		#[allow(clippy::identity_op)]
		const DEFAULT_MAX_TOKEN_VALID_DURATION: std::time::Duration = std::time::Duration::from_secs(1 * 60 * 60);

		let iothub_hostname = self.iothub_hostname.clone();
		let timeout = self.timeout;
		let extra = self.extra.clone();

		let authentication = match &self.authentication {
			crate::Authentication::SasKey { device_id, key, max_token_valid_duration, server_root_certificate } =>
				match prepare_sas_token_request(&*iothub_hostname, device_id, None, *max_token_valid_duration) {
					Ok((signature_data, make_sas_token)) => {
						use hmac::Mac;

						let mut mac = hmac::Hmac::<sha2::Sha256>::new_varkey(key).expect("HMAC can have invalid key length");
						mac.input(signature_data.as_bytes());
						let signature = mac.result().code();
						let signature = base64::encode(signature.as_slice());

						let sas_token = make_sas_token(&signature);

						futures_util::future::Either::Left(futures_util::future::ok((Some(sas_token), None, server_root_certificate.clone())))
					},

					Err(err) => futures_util::future::Either::Left(futures_util::future::err(err)),
				},

			crate::Authentication::SasToken { token, server_root_certificate } =>
				futures_util::future::Either::Left(futures_util::future::ok((Some(token.to_owned()), None, server_root_certificate.clone()))),

			crate::Authentication::Certificate { der, password, server_root_certificate } =>
				match native_tls::Identity::from_pkcs12(der, password) {
					Ok(identity) => futures_util::future::Either::Left(futures_util::future::ok((None, Some(identity), server_root_certificate.clone()))),
					Err(err) => futures_util::future::Either::Left(futures_util::future::err(
						std::io::Error::new(std::io::ErrorKind::Other, format!("could not parse client certificate: {}", err)))),
				},

			crate::Authentication::IotEdge { device_id, module_id, generation_id, iothub_hostname, workload_url } =>
				match crate::iotedge_client::Client::new(workload_url) {
					Ok(iotedge_client) => {
						match prepare_sas_token_request(iothub_hostname, device_id, None, DEFAULT_MAX_TOKEN_VALID_DURATION) {
							Ok((signature_data, make_sas_token)) => {
								let signature = iotedge_client.hmac_sha256(module_id, generation_id, &signature_data);

								let server_root_certificate = iotedge_client.get_server_root_certificate();

								futures_util::future::Either::Right(futures_util::future::try_join(signature, server_root_certificate)
									.map(move |result| match result {
										Ok((signature, server_root_certificate)) => {
											let sas_token = make_sas_token(&signature);
											Ok((Some(sas_token), None, Some(server_root_certificate)))
										},

										Err(err) => Err(std::io::Error::new(std::io::ErrorKind::Other, err)),
									}))
							},

							Err(err) => futures_util::future::Either::Left(futures_util::future::err(err)),
						}
					},

					Err(err) => futures_util::future::Either::Left(futures_util::future::err(
						std::io::Error::new(std::io::ErrorKind::Other, format!("could not initialize iotedge client: {}", err)))),
				},
		};

		let iothub_host = self.iothub_host.to_owned();

		Box::pin(async move {
			let stream = async {
				let stream =
					tokio::time::timeout(timeout, tokio::net::TcpStream::connect(&iothub_host)).await
					.map_err(|_| std::io::ErrorKind::TimedOut)?;
				Ok(stream)
			};

			let ((password, identity, server_root_certificate), stream) = futures_util::future::try_join(authentication, stream).await?;

			let stream = stream?;
			stream.set_nodelay(true)?;

			let mut stream = tokio_io_timeout::TimeoutStream::new(stream);
			stream.set_read_timeout(Some(timeout));

			let mut tls_connector_builder = native_tls::TlsConnector::builder();
			if let Some(identity) = identity {
				tls_connector_builder.identity(identity);
			}
			if let Some(server_root_certificate) = server_root_certificate {
				tls_connector_builder.add_root_certificate(server_root_certificate);
			}
			let connector =
				tls_connector_builder.build()
				.map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, format!("could not create TLS connector: {}", err)))?;
			let connector: tokio_tls::TlsConnector = connector.into();

			let stream =
				connector.connect(&iothub_hostname, stream).await
				.map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, err))?;

			match extra {
				IoSourceExtra::Raw => Ok((Io::Raw(stream), password)),

				IoSourceExtra::WebSocket { uri } => {
					let request =
						http::Request::get(uri)
						.header("sec-websocket-protocol", "mqtt")
						.body(())
						.expect("building client handshake request cannot fail");

					let handshake =
						tungstenite::ClientHandshake::start(TokioToStd { tokio: stream, cx: std::ptr::null_mut() }, request, None)
						.map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, err))?;
					let stream = WsConnect::Handshake(handshake).await?;
					Ok((
						Io::WebSocket {
							inner: stream,
							pending_read: std::io::Cursor::new(vec![]),
						},
						password,
					))
				},
			}
		})
	}
}

/// The transport to use for the connection to the Azure IoT Hub
#[derive(Clone, Copy, Debug)]
pub enum Transport {
	Tcp,
	WebSocket,
}

enum WsConnect<S> where S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin {
	Handshake(tungstenite::handshake::MidHandshake<tungstenite::ClientHandshake<TokioToStd<S>>>),
	Invalid,
}

impl<S> std::fmt::Debug for WsConnect<S> where S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			WsConnect::Handshake(_) => f.debug_struct("Handshake").finish(),
			WsConnect::Invalid => f.debug_struct("Invalid").finish(),
		}
	}
}

impl<S> Future for WsConnect<S> where S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin {
	type Output = std::io::Result<tungstenite::WebSocket<TokioToStd<S>>>;

	fn poll(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Self::Output> {
		match std::mem::replace(&mut *self, WsConnect::Invalid) {
			WsConnect::Handshake(mut handshake) => {
				handshake.get_mut().get_mut().set_cx(cx);
				match handshake.handshake() {
					Ok((mut stream, _)) => {
						stream.get_mut().set_cx(std::ptr::null_mut());
						std::task::Poll::Ready(Ok(stream))
					},

					Err(tungstenite::HandshakeError::Interrupted(mut handshake)) => {
						handshake.get_mut().get_mut().set_cx(std::ptr::null_mut());
						*self = WsConnect::Handshake(handshake);
						std::task::Poll::Pending
					},

					Err(tungstenite::HandshakeError::Failure(err)) =>
						poll_from_tungstenite_error(err),
				}
			},

			WsConnect::Invalid =>
				panic!("future polled after completion"),
		}
	}
}

/// A wrapper around an inner I/O object
pub enum Io<S> {
	Raw(S),

	WebSocket {
		inner: tungstenite::WebSocket<TokioToStd<S>>,
		pending_read: std::io::Cursor<Vec<u8>>,
	},
}

impl<S> tokio::io::AsyncRead for Io<S> where S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin {
	fn poll_read(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>, buf: &mut [u8]) -> std::task::Poll<std::io::Result<usize>> {
		use std::io::Read;

		let (inner, pending_read) = match &mut *self {
			Io::Raw(stream) => return std::pin::Pin::new(stream).poll_read(cx, buf),
			Io::WebSocket { inner, pending_read } => (inner, pending_read),
		};

		if buf.is_empty() {
			return std::task::Poll::Ready(Ok(0));
		}

		loop {
			if pending_read.position() != pending_read.get_ref().len() as u64 {
				return std::task::Poll::Ready(Ok(pending_read.read(buf).expect("Cursor::read cannot fail")));
			}

			inner.get_mut().set_cx(cx);
			let message = inner.read_message();
			inner.get_mut().set_cx(std::ptr::null_mut());

			match message {
				Ok(tungstenite::Message::Binary(b)) => *pending_read = std::io::Cursor::new(b),
				Ok(message) => log::warn!("ignoring unexpected message: {:?}", message),
				Err(err) => return poll_from_tungstenite_error(err),
			};
		}
	}
}

impl<S> tokio::io::AsyncWrite for Io<S> where S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin {
	fn poll_write(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>, buf: &[u8]) -> std::task::Poll<std::io::Result<usize>> {
		let inner = match &mut *self {
			Io::Raw(stream) => return std::pin::Pin::new(stream).poll_write(cx, buf),
			Io::WebSocket { inner, .. } => inner,
		};

		if buf.is_empty() {
			return std::task::Poll::Ready(Ok(0));
		}

		let message = tungstenite::Message::Binary(buf.to_owned());

		inner.get_mut().set_cx(cx);
		let result = inner.write_message(message);
		inner.get_mut().set_cx(std::ptr::null_mut());

		match result {
			Ok(()) => std::task::Poll::Ready(Ok(buf.len())),
			Err(tungstenite::Error::SendQueueFull(_)) => std::task::Poll::Pending, // Hope client calls `poll_flush()` before retrying
			Err(err) => poll_from_tungstenite_error(err),
		}
	}

	fn poll_flush(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		let inner = match &mut *self {
			Io::Raw(stream) => return std::pin::Pin::new(stream).poll_flush(cx),
			Io::WebSocket { inner, .. } => inner,
		};

		inner.get_mut().set_cx(cx);
		let result = inner.write_pending();
		inner.get_mut().set_cx(std::ptr::null_mut());
		match result {
			Ok(()) => std::task::Poll::Ready(Ok(())),
			Err(err) => poll_from_tungstenite_error(err),
		}
	}

	fn poll_shutdown(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		let inner = match &mut *self {
			Io::Raw(stream) => return std::pin::Pin::new(stream).poll_shutdown(cx),
			Io::WebSocket { inner, .. } => inner,
		};

		inner.get_mut().set_cx(cx);
		let result = inner.close(None);
		inner.get_mut().set_cx(std::ptr::null_mut());
		match result {
			Ok(()) => std::task::Poll::Ready(Ok(())),
			Err(err) => poll_from_tungstenite_error(err),
		}
	}
}

fn poll_from_tungstenite_error<T>(err: tungstenite::Error) -> std::task::Poll<std::io::Result<T>> {
	match err {
		tungstenite::Error::Io(ref err) if err.kind() == std::io::ErrorKind::WouldBlock => std::task::Poll::Pending,
		tungstenite::Error::Io(err) => std::task::Poll::Ready(Err(err)),
		err => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, err))),
	}
}

fn prepare_sas_token_request(
	iothub_hostname: &str,
	device_id: &str,
	module_id: Option<&str>,
	max_token_valid_duration: std::time::Duration,
) -> std::io::Result<(String, impl FnOnce(&str) -> String)> {
	let since_unix_epoch =
		std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH)
		.map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, format!("could not get current time: {}", err)))?;

	let resource_uri =
		if let Some(module_id) = module_id {
			format!("{}/devices/{}/modules/{}", iothub_hostname, device_id, module_id)
		}
		else {
			format!("{}/devices/{}", iothub_hostname, device_id)
		};
	let resource_uri: String = percent_encoding::utf8_percent_encode(&resource_uri, IOTHUB_ENCODE_SET).collect();

	let expiry = since_unix_epoch + max_token_valid_duration;
	let expiry = expiry.as_secs().to_string();

	let signature_data = format!("{}\n{}", resource_uri, expiry);

	Ok((signature_data, move |signature: &str| {
		let mut serializer = url::form_urlencoded::Serializer::new(format!("SharedAccessSignature sr={}", resource_uri));
		serializer.append_pair("se", &expiry);
		serializer.append_pair("sig", signature);
		serializer.finish()
	}))
}

/// Implements `std::io::{Read, Write}` for a `tokio::io::Async{Read, Write}`
///
/// However the impls still require an active task context, and thus can only be used inside a `tokio::io::Async{Read, Write}` impl.
/// Make sure to call `set_cx` to set the current task context each time the `std::io::{Read, Write}` impls are used.
///
/// (Not part of public API, so it's not exported from the crate root.)
pub struct TokioToStd<T> {
	tokio: T,
	cx: *mut std::task::Context<'static>,
}

impl<T> TokioToStd<T> {
	fn set_cx(&mut self, cx: *mut std::task::Context<'_>) {
		self.cx = cx as *mut std::ffi::c_void as *mut _;
	}
}

unsafe impl<T> Send for TokioToStd<T> where T: Send { }

impl<T> std::io::Read for TokioToStd<T> where T: tokio::io::AsyncRead + Unpin {
	fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
		unsafe {
			let cx = self.cx.as_mut().expect("TokioToStd used without setting task context");

			match std::pin::Pin::new(&mut self.tokio).poll_read(cx, buf) {
				std::task::Poll::Ready(Ok(read)) => Ok(read),
				std::task::Poll::Ready(Err(err)) => Err(err),
				std::task::Poll::Pending => Err(std::io::ErrorKind::WouldBlock.into()),
			}
		}
	}
}

impl<T> std::io::Write for TokioToStd<T> where T: tokio::io::AsyncWrite + Unpin {
	fn write(&mut self, buf: &[u8]) -> std::io::Result<usize> {
		unsafe {
			let cx = self.cx.as_mut().expect("TokioToStd used without setting task context");

			match std::pin::Pin::new(&mut self.tokio).poll_write(cx, buf) {
				std::task::Poll::Ready(result) => result,
				std::task::Poll::Pending => Err(std::io::ErrorKind::WouldBlock.into()),
			}
		}
	}

	fn flush(&mut self) -> std::io::Result<()> {
		unsafe {
			let cx = self.cx.as_mut().expect("TokioToStd used without setting task context");

			match std::pin::Pin::new(&mut self.tokio).poll_flush(cx) {
				std::task::Poll::Ready(result) => result,
				std::task::Poll::Pending => Err(std::io::ErrorKind::WouldBlock.into()),
			}
		}
	}
}
