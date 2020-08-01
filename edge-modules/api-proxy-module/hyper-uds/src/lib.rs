#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
	clippy::default_trait_access,
	clippy::missing_errors_doc,
	clippy::must_use_candidate,
	clippy::too_many_lines,
	clippy::type_complexity,
)]

pub struct UnixStream {
	pending_read: std::sync::Arc<std::sync::Mutex<PendingRead>>,
	reader_sender: std::sync::mpsc::Sender<std::task::Waker>,

	pending_write: std::sync::Arc<std::sync::Mutex<PendingWrite>>,
	flush_sender: std::sync::mpsc::Sender<std::task::Waker>,
}

struct PendingRead {
	bytes: bytes::BytesMut,
	err: Option<std::io::Error>,
	eof: bool,
}

struct PendingWrite {
	bytes: bytes::BytesMut,
	flushed: bool,
	err: Option<std::io::Error>,
	eof: bool,
}

impl std::fmt::Debug for UnixStream {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		f.debug_struct("UnixStream").finish()
	}
}

impl UnixStream {
	pub async fn connect(path: std::path::PathBuf) -> std::io::Result<Self> {
		let join_handle = tokio::task::spawn_blocking(|| -> std::io::Result<_> {
			// TODO: For Windows, manually open a SOCKET ala mio-uds-windows and use that instead.
			let inner = std::os::unix::net::UnixStream::connect(path)?;
			Ok(inner)
		});
		let inner = join_handle.await??;
		let inner = std::sync::Arc::new(inner);


		let pending_read = std::sync::Arc::new(std::sync::Mutex::new(PendingRead {
			bytes: bytes::BytesMut::new(),
			err: None,
			eof: false,
		}));

		let (reader_sender, reader_receiver) = std::sync::mpsc::channel::<std::task::Waker>();
		let _ = std::thread::spawn({
			let inner = inner.clone();
			let pending_read = pending_read.clone();

			move || -> std::io::Result<()> {
				let mut buf = vec![0_u8; 1024];

				while let Ok(waker) = reader_receiver.recv() {
					match pending_read.lock() {
						Ok(pending_read) => {
							let pending_read = &*pending_read;

							if !pending_read.bytes.is_empty() || pending_read.eof || pending_read.err.is_some() {
								waker.wake();
								continue;
							}
						},

						Err(_) => break,
					}

					let read = std::io::Read::read(&mut &*inner, &mut buf);

					match pending_read.lock() {
						Ok(mut pending_read) => {
							let pending_read = &mut *pending_read;

							match read {
								Ok(0) => pending_read.eof = true,
								Ok(read) => pending_read.bytes.extend_from_slice(&buf[..read]),
								Err(err) => {
									pending_read.err = Some(err);
									pending_read.eof = true;
								},
							}

							waker.wake();
						},

						Err(_) => break,
					}
				}

				Ok(())
			}
		});


		let pending_write = std::sync::Arc::new(std::sync::Mutex::new(PendingWrite {
			bytes: bytes::BytesMut::new(),
			flushed: true,
			err: None,
			eof: false,
		}));

		let (flush_sender, flush_receiver) = std::sync::mpsc::channel::<std::task::Waker>();
		let _ = std::thread::spawn({
			let pending_write = pending_write.clone();

			move || -> std::io::Result<()> {
				'outer: while let Ok(waker) = flush_receiver.recv() {
					loop {
						let pending_write_bytes = match pending_write.lock() {
							Ok(mut pending_write) => {
								let pending_write = &mut *pending_write;

								if !pending_write.bytes.is_empty() && !pending_write.eof && pending_write.err.is_none() {
									Some(pending_write.bytes.split())
								}
								else {
									None
								}
							},

							Err(_) => break 'outer,
						};

						let result =
							if let Some(pending_write_bytes) = pending_write_bytes {
								std::io::Write::write_all(&mut &*inner, &*pending_write_bytes)
								.and_then(|_| std::io::Write::flush(&mut &*inner))
							}
							else {
								Ok(())
							};

						match pending_write.lock() {
							Ok(mut pending_write) => {
								let pending_write = &mut *pending_write;

								match result {
									Ok(()) =>
										if pending_write.bytes.is_empty() {
											pending_write.flushed = true
										}
										else {
											// More pending writes happened while we were flushing
											continue;
										},

									Err(err) => {
										pending_write.err = Some(err);
										pending_write.eof = true;
									},
								}
							},

							Err(_) => break 'outer,
						}

						waker.wake();
						break;
					}
				}

				Ok(())
			}
		});


		Ok(UnixStream {
			pending_read,
			reader_sender,

			pending_write,
			flush_sender,
		})
	}
}

impl hyper::client::connect::Connection for UnixStream {
	fn connected(&self) -> hyper::client::connect::Connected {
		hyper::client::connect::Connected::new()
	}
}

impl tokio::io::AsyncRead for UnixStream {
	fn poll_read(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>, buf: &mut [u8]) -> std::task::Poll<std::io::Result<usize>> {
		match self.pending_read.lock() {
			Ok(mut pending_read) => {
				let pending_read = &mut *pending_read;

				if !pending_read.bytes.is_empty() {
					let len = std::cmp::min(pending_read.bytes.len(), buf.len());
					buf[..len].copy_from_slice(&pending_read.bytes[..len]);
					bytes::Buf::advance(&mut pending_read.bytes, len);
					std::task::Poll::Ready(Ok(len))
				}
				else if let Some(err) = pending_read.err.take() {
					std::task::Poll::Ready(Err(err))
				}
				else if pending_read.eof {
					std::task::Poll::Ready(Ok(0))
				}
				else {
					match self.reader_sender.send(cx.waker().clone()) {
						Ok(()) => std::task::Poll::Pending,
						Err(_) => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, "reader receiver dropped"))),
					}
				}
			},

			Err(_) => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, "reader mutex poisoned"))),
		}
	}
}

impl tokio::io::AsyncWrite for UnixStream {
	fn poll_write(self: std::pin::Pin<&mut Self>, _cx: &mut std::task::Context<'_>, buf: &[u8]) -> std::task::Poll<std::io::Result<usize>> {
		match self.pending_write.lock() {
			Ok(mut pending_write) => {
				let pending_write = &mut *pending_write;

				if let Some(err) = pending_write.err.take() {
					std::task::Poll::Ready(Err(err))
				}
				else if pending_write.eof {
					std::task::Poll::Ready(Ok(0))
				}
				else {
					pending_write.bytes.extend_from_slice(buf);
					pending_write.flushed = false;
					std::task::Poll::Ready(Ok(buf.len()))
				}
			},

			Err(_) => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, "writer mutex poisoned"))),
		}
	}

	fn poll_flush(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		match self.pending_write.lock() {
			Ok(mut pending_write) => {
				let pending_write = &mut *pending_write;

				if let Some(err) = pending_write.err.take() {
					std::task::Poll::Ready(Err(err))
				}
				else if pending_write.flushed {
					std::task::Poll::Ready(Ok(()))
				}
				else {
					match self.flush_sender.send(cx.waker().clone()) {
						Ok(()) => std::task::Poll::Pending,
						Err(_) => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, "flush receiver dropped"))),
					}
				}
			},

			Err(_) => std::task::Poll::Ready(Err(std::io::Error::new(std::io::ErrorKind::Other, "writer mutex poisoned"))),
		}
	}

	fn poll_shutdown(self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		self.poll_flush(cx)
	}
}

#[derive(Clone, Debug, Default)]
pub struct UdsConnector;

impl UdsConnector {
	pub fn new() -> Self {
		Default::default()
	}
}

impl hyper::service::Service<hyper::Uri> for UdsConnector {
	type Response = UnixStream;
	type Error = std::io::Error;
	type Future = std::pin::Pin<Box<dyn std::future::Future<Output = Result<Self::Response, Self::Error>> + Send>>;

	fn poll_ready(&mut self, _cx: &mut std::task::Context<'_>) -> std::task::Poll<Result<(), Self::Error>> {
		std::task::Poll::Ready(Ok(()))
	}

	fn call(&mut self, req: hyper::Uri) -> Self::Future {
		Box::pin(async move {
			let scheme = req.scheme_str();
			if scheme != Some("unix") {
				return Err(std::io::Error::new(std::io::ErrorKind::Other, format!(r#"expected req to have "unix" scheme but it has {:?}"#, scheme)));
			}

			let host = req.host();

			let path: std::path::PathBuf =
				host
				.ok_or_else(|| format!("could not decode UDS path from req host {:?}", host))
				.and_then(|host| hex::decode(host).map_err(|err| format!("could not decode UDS path from req host {:?}: {}", host, err)))
				.and_then(|path| String::from_utf8(path).map_err(|err| format!("could not decode UDS path from req host {:?}: {}", host, err)))
				.map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, err))?
				.into();

			let stream = UnixStream::connect(path).await?;
			Ok(stream)
		})
	}
}

pub fn make_hyper_uri(base: &str, path: &str) -> Result<hyper::Uri, <hyper::Uri as std::str::FromStr>::Err> {
	let host = hex::encode(base.as_bytes());
	let uri = format!("unix://{}:0{}", host, path);
	let uri = uri.parse()?;
	Ok(uri)
}

// TODO
// impl hyper::Accept for UnixAccept {
// 	fn accept() {
// 		UnixStream {
// 		}
// 	}
// }
