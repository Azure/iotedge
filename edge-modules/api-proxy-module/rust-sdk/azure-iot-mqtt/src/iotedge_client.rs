use std::future::Future;

pub(crate) struct Client {
	scheme: Scheme,
	base: String,
	inner: hyper::Client<Connector>,
}

impl Client {
	pub(crate) fn new(workload_url: &url::Url) -> Result<Self, Error> {
		let (scheme, base, connector) =
			match workload_url.scheme() {
				"http" => (Scheme::Http, workload_url.to_string(), Connector::Http(hyper::client::HttpConnector::new())),
				"unix" =>
					if cfg!(windows) {
						// We get better handling of Windows file syntax if we parse a
						// unix:// URL as a file:// URL. Specifically:
						// - On Unix, `Url::parse("unix:///path")?.to_file_path()` succeeds and returns `Ok("/path")`.
						// - On Windows, `Url::parse("unix:///C:/path")?.to_file_path()` fails with `Err(())`.
						// - On Windows, `Url::parse("file:///C:/path")?.to_file_path()` succeeds and returns `Ok(r"C:\path")`.
						let mut workload_url = workload_url.clone();
						workload_url.set_scheme("file").expect(r#"changing the scheme of workload URI to "file" should not fail"#);
						let base = workload_url.to_file_path().map_err(|()| Error::ParseWorkloadUrlUnixFilePath)?;
						let base = base.to_str().ok_or_else(|| Error::ParseWorkloadUrlUnixFilePath)?;
						(Scheme::Unix, base.to_owned(), Connector::Unix(hyper_uds::UdsConnector::new()))
					}
					else {
						(Scheme::Unix, workload_url.path().to_owned(), Connector::Unix(hyper_uds::UdsConnector::new()))
					},
				scheme => return Err(Error::UnrecognizedWorkloadUrlScheme(scheme.to_owned())),
			};

		let inner = hyper::Client::builder().build(connector);

		Ok(Client {
			scheme,
			base,
			inner,
		})
	}

	pub(crate) fn get_server_root_certificate(&self) -> impl Future<Output = Result<native_tls::Certificate, Error>> {
		let url =
			make_hyper_uri(self.scheme, &*self.base, "/trust-bundle?api-version=2019-01-30")
			.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ConstructRequestUrl(err)));

		let request =
			url.and_then(|url|
				http::Request::get(url).body(Default::default())
				.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ConstructRequest(err))));

		let response = request.map(|request| self.inner.request(request));

		async {
			use futures_util::StreamExt;

			let response =
				response?.await
				.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ExecuteRequest(err)))?;

			let (response_parts, mut response_body) = response.into_parts();

			let status = response_parts.status;
			if status != http::StatusCode::OK {
				return Err(Error::GetServerRootCertificate(ApiErrorReason::UnsuccessfulResponse(status)));
			}

			let mut response = bytes::BytesMut::new();
			while let Some(chunk) = response_body.next().await {
				let chunk = chunk.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ReadResponse(err)))?;
				response.extend_from_slice(&chunk);
			}

			let TrustBundleResponse { certificate } =
				serde_json::from_slice(&*response)
				.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ParseResponseBody(Box::new(err))))?;

			let certificate =
				native_tls::Certificate::from_pem(certificate.as_bytes())
				.map_err(|err| Error::GetServerRootCertificate(ApiErrorReason::ParseResponseBody(Box::new(err))))?;

			Ok(certificate)
		}
	}

	pub(crate) fn hmac_sha256(&self, module_id: &str, generation_id: &str, data: &str) -> impl Future<Output = Result<String, Error>> {
		let url =
			make_hyper_uri(self.scheme, &*self.base, &format!("/modules/{}/genid/{}/sign?api-version=2019-01-30", module_id, generation_id))
			.map_err(|err| Error::SignSasToken(ApiErrorReason::ConstructRequestUrl(err)));

		let request =
			url.and_then(|url| {
				let data = base64::encode(data.as_bytes());

				let sign_request = SignRequest {
					key_id: "primary",
					algorithm: "HMACSHA256",
					data: &data,
				};

				let body = serde_json::to_vec(&sign_request).map_err(|err| Error::SignSasToken(ApiErrorReason::SerializeRequestBody(err)))?;

				http::Request::post(url).body(body.into())
				.map_err(|err| Error::SignSasToken(ApiErrorReason::ConstructRequest(err)))
			});

		let response = request.map(|request| self.inner.request(request));


		async {
			use futures_util::StreamExt;

			let response =
				response?.await
				.map_err(|err| Error::SignSasToken(ApiErrorReason::ExecuteRequest(err)))?;

			let (response_parts, mut response_body) = response.into_parts();

			let status = response_parts.status;
			if status != http::StatusCode::OK {
				return Err(Error::SignSasToken(ApiErrorReason::UnsuccessfulResponse(status)));
			}

			let mut response = bytes::BytesMut::new();
			while let Some(chunk) = response_body.next().await {
				let chunk = chunk.map_err(|err| Error::SignSasToken(ApiErrorReason::ReadResponse(err)))?;
				response.extend_from_slice(&chunk);
			}

			let SignResponse { digest } =
				serde_json::from_slice(&*response)
				.map_err(|err| Error::SignSasToken(ApiErrorReason::ParseResponseBody(Box::new(err))))?;

			Ok(digest)
		}
	}
}

#[derive(Clone, Copy, Debug)]
enum Scheme {
	Http,
	Unix,
}

fn make_hyper_uri(scheme: Scheme, base: &str, path: &str) -> Result<hyper::Uri, Box<dyn std::error::Error + Send + Sync>> {
	match scheme {
		Scheme::Http => {
			let base = url::Url::parse(base)?;
			let url = base.join(path)?;
			let url = url.as_str().parse()?;
			Ok(url)
		},

		Scheme::Unix => Ok(hyper_uds::make_hyper_uri(base, path)?),
	}
}

#[derive(Clone)]
enum Connector {
	Http(hyper::client::HttpConnector),
	Unix(hyper_uds::UdsConnector),
}

impl hyper::service::Service<http::Uri> for Connector {
	type Response = Transport;
	type Error = std::io::Error;
	type Future = Box<dyn Future<Output = Result<Self::Response, Self::Error>> + Send + Unpin>;

	fn poll_ready(&mut self, cx: &mut std::task::Context<'_>) -> std::task::Poll<Result<(), Self::Error>> {
		match self {
			Connector::Http(connector) => connector.poll_ready(cx).map_err(|err| std::io::Error::new(std::io::ErrorKind::Other, err)),
			Connector::Unix(connector) => connector.poll_ready(cx),
		}
	}

	fn call(&mut self, req: http::Uri) -> Self::Future {
		use futures_util::{ FutureExt, TryFutureExt };

		match self {
			Connector::Http(connector) => Box::new(connector.call(req).map(|transport| match transport {
				Ok(transport) => Ok(Transport::Http(transport)),
				Err(err) => Err(std::io::Error::new(std::io::ErrorKind::Other, err)),
			})) as Self::Future,
			Connector::Unix(connector) => Box::new(connector.call(req).map_ok(Transport::Unix)),
		}
	}
}

enum Transport {
	Http(<hyper::client::HttpConnector as hyper::service::Service<http::Uri>>::Response),
	Unix(<hyper_uds::UdsConnector as hyper::service::Service<http::Uri>>::Response),
}

impl hyper::client::connect::Connection for Transport {
	fn connected(&self) -> hyper::client::connect::Connected {
		match self {
			Transport::Http(transport) => transport.connected(),
			Transport::Unix(transport) => transport.connected(),
		}
	}
}

impl tokio::io::AsyncRead for Transport {
	fn poll_read(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>, buf: &mut [u8]) -> std::task::Poll<std::io::Result<usize>> {
		match &mut *self {
			Transport::Http(transport) => std::pin::Pin::new(transport).poll_read(cx, buf),
			Transport::Unix(transport) => std::pin::Pin::new(transport).poll_read(cx, buf),
		}
	}

	unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [std::mem::MaybeUninit<u8>]) -> bool {
		match self {
			Transport::Http(transport) => transport.prepare_uninitialized_buffer(buf),
			Transport::Unix(transport) => transport.prepare_uninitialized_buffer(buf),
		}
	}
}

impl tokio::io::AsyncWrite for Transport {
	fn poll_write(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>, buf: &[u8]) -> std::task::Poll<std::io::Result<usize>> {
		match &mut *self {
			Transport::Http(transport) => std::pin::Pin::new(transport).poll_write(cx, buf),
			Transport::Unix(transport) => std::pin::Pin::new(transport).poll_write(cx, buf),
		}
	}

	fn poll_flush(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		match &mut *self {
			Transport::Http(transport) => std::pin::Pin::new(transport).poll_flush(cx),
			Transport::Unix(transport) => std::pin::Pin::new(transport).poll_flush(cx),
		}
	}

	fn poll_shutdown(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		match &mut *self {
			Transport::Http(transport) => std::pin::Pin::new(transport).poll_shutdown(cx),
			Transport::Unix(transport) => std::pin::Pin::new(transport).poll_shutdown(cx),
		}
	}
}

#[derive(serde_derive::Deserialize)]
struct TrustBundleResponse {
	certificate: String,
}

#[derive(serde_derive::Serialize)]
struct SignRequest<'a> {
	#[serde(rename = "keyId")]
	key_id: &'static str,
	#[serde(rename = "algo")]
	algorithm: &'static str,
	data: &'a str,
}

#[derive(serde_derive::Deserialize)]
struct SignResponse {
	digest: String,
}

#[derive(Debug)]
pub(super) enum Error {
	GetServerRootCertificate(ApiErrorReason),
	ParseWorkloadUrlUnixFilePath,
	SignSasToken(ApiErrorReason),
	UnrecognizedWorkloadUrlScheme(String),
}

impl std::fmt::Display for Error {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			Error::GetServerRootCertificate(reason) => write!(f, "could not get server root certificate: {}", reason),
			Error::ParseWorkloadUrlUnixFilePath => write!(f, "could not parse workload URL as UDS file path"),
			Error::SignSasToken(reason) => write!(f, "could not create SAS token: {}", reason),
			Error::UnrecognizedWorkloadUrlScheme(scheme) => write!(f, "unrecognized scheme {:?}", scheme),
		}
	}
}

impl std::error::Error for Error {
	#[allow(clippy::match_same_arms)]
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		match self {
			Error::GetServerRootCertificate(reason) => reason.source(),
			Error::ParseWorkloadUrlUnixFilePath => None,
			Error::SignSasToken(reason) => reason.source(),
			Error::UnrecognizedWorkloadUrlScheme(_) => None,
		}
	}
}

#[derive(Debug)]
pub(super) enum ApiErrorReason {
	ConstructRequestUrl(Box<dyn std::error::Error + Send + Sync>),
	ConstructRequest(http::Error),
	ExecuteRequest(hyper::Error),
	ParseResponseBody(Box<dyn std::error::Error + Send + Sync>),
	ReadResponse(hyper::Error),
	SerializeRequestBody(serde_json::Error),
	UnsuccessfulResponse(http::StatusCode),
}

impl std::fmt::Display for ApiErrorReason {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			ApiErrorReason::ConstructRequestUrl(err) => write!(f, "could not construct request URL: {}", err),
			ApiErrorReason::ConstructRequest(err) => write!(f, "could not construct request: {}", err),
			ApiErrorReason::ExecuteRequest(err) => write!(f, "could not execute request: {}", err),
			ApiErrorReason::ParseResponseBody(err) => write!(f, "could not deserialize response: {}", err),
			ApiErrorReason::ReadResponse(err) => write!(f, "could not read response: {}", err),
			ApiErrorReason::SerializeRequestBody(err) => write!(f, "could not serialize request: {}", err),
			ApiErrorReason::UnsuccessfulResponse(status) => write!(f, "response has status code {}", status),
		}
	}
}

impl std::error::Error for ApiErrorReason {
	#[allow(clippy::match_same_arms)]
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		match self {
			ApiErrorReason::ConstructRequestUrl(err) => Some(&**err),
			ApiErrorReason::ConstructRequest(err) => Some(err),
			ApiErrorReason::ExecuteRequest(err) => Some(err),
			ApiErrorReason::ParseResponseBody(err) => Some(&**err),
			ApiErrorReason::ReadResponse(err) => Some(err),
			ApiErrorReason::SerializeRequestBody(err) => Some(err),
			ApiErrorReason::UnsuccessfulResponse(_) => None,
		}
	}
}
