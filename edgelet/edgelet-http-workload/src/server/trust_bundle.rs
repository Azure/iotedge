// Copyright (c) Microsoft. All rights reserved.

use std::str;

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use serde_json;

use edgelet_core::{Certificate, GetTrustBundle};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use workload::models::TrustBundleResponse;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct TrustBundleHandler<T: GetTrustBundle> {
    hsm: T,
}

impl<T> TrustBundleHandler<T>
where
    T: 'static + GetTrustBundle + Clone,
{
    pub fn new(hsm: T) -> Self {
        TrustBundleHandler { hsm }
    }
}

impl<T> Handler<Parameters> for TrustBundleHandler<T>
where
    T: 'static + GetTrustBundle + Send,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = self
            .hsm
            .get_trust_bundle()
            .context(ErrorKind::EncryptionOperation(
                EncryptionOperation::GetTrustBundle,
            ))
            .map_err(Error::from)
            .and_then(|cert| -> Result<_, Error> {
                let cert = cert.pem().context(ErrorKind::EncryptionOperation(
                    EncryptionOperation::GetTrustBundle,
                ))?;
                let cert = str::from_utf8(cert.as_ref())
                    .context(ErrorKind::EncryptionOperation(
                        EncryptionOperation::GetTrustBundle,
                    ))?
                    .to_string();
                let body = serde_json::to_string(&TrustBundleResponse::new(cert)).context(
                    ErrorKind::EncryptionOperation(EncryptionOperation::GetTrustBundle),
                )?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .context(ErrorKind::EncryptionOperation(
                        EncryptionOperation::GetTrustBundle,
                    ))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()))
            .into_future();

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use futures::Future;
    use futures::Stream;

    use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
    use edgelet_test_utils::cert::TestCert;

    use super::*;

    #[derive(Clone, Default, Debug)]
    struct TestHsm {
        fail_call: bool,
        cert: TestCert,
    }

    impl TestHsm {
        fn with_fail_call(mut self, fail_call: bool) -> Self {
            self.fail_call = fail_call;
            self
        }

        fn with_cert(mut self, cert: TestCert) -> Self {
            self.cert = cert;
            self
        }
    }

    impl GetTrustBundle for TestHsm {
        type Certificate = TestCert;

        fn get_trust_bundle(&self) -> Result<Self::Certificate, CoreError> {
            if self.fail_call {
                Err(CoreError::from(CoreErrorKind::KeyStore))
            } else {
                Ok(self.cert.clone())
            }
        }
    }

    #[test]
    fn get_fail() {
        let handler = TrustBundleHandler::new(TestHsm::default().with_fail_call(true));
        let request = Request::get("http://localhost/trust-bundle")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }

    #[test]
    fn pem_fail() {
        let handler = TrustBundleHandler::new(
            TestHsm::default().with_cert(TestCert::default().with_fail_pem(true)),
        );
        let request = Request::get("http://localhost/trust-bundle")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }

    #[test]
    fn utf8_decode_fail() {
        let handler = TrustBundleHandler::new(
            TestHsm::default().with_cert(TestCert::default().with_cert(vec![0, 159, 146, 150])),
        );
        let request = Request::get("http://localhost/trust-bundle")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }

    #[test]
    fn success() {
        let handler = TrustBundleHandler::new(
            TestHsm::default().with_cert(TestCert::default().with_cert(b"boo".to_vec())),
        );
        let request = Request::get("http://localhost/trust-bundle")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::OK, response.status());

        let content_length = {
            let headers = response.headers();
            assert_eq!(headers.get(CONTENT_TYPE).unwrap(), &"application/json");

            headers
                .get(CONTENT_LENGTH)
                .unwrap()
                .to_str()
                .unwrap()
                .to_string()
        };

        response
            .into_body()
            .concat2()
            .and_then(|b| {
                assert_eq!(content_length, b.len().to_string());
                let trust_bundle: TrustBundleResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("boo", trust_bundle.certificate().as_str());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
