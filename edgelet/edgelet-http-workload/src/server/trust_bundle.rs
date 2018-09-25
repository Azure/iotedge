// Copyright (c) Microsoft. All rights reserved.

use std::str;

use failure::ResultExt;
use futures::future;
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use serde_json;

use edgelet_core::{Certificate, GetTrustBundle};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use workload::models::TrustBundleResponse;

use error::{Error, ErrorKind};
use IntoResponse;

pub struct TrustBundleHandler<T: GetTrustBundle> {
    hsm: T,
}

impl<T> TrustBundleHandler<T>
where
    T: GetTrustBundle + 'static + Clone,
{
    pub fn new(hsm: T) -> Self {
        TrustBundleHandler { hsm }
    }
}

impl<T> Handler<Parameters> for TrustBundleHandler<T>
where
    T: GetTrustBundle + 'static,
    <T as GetTrustBundle>::Certificate: Certificate,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let response = self
            .hsm
            .get_trust_bundle()
            .and_then(|cert| cert.pem())
            .map_err(Error::from)
            .and_then(|cert| {
                str::from_utf8(cert.as_ref())
                    .context(ErrorKind::Utf8)
                    .map_err(From::from)
                    .map(|s| s.to_string())
            }).and_then(|cert| {
                serde_json::to_string(&TrustBundleResponse::new(cert))
                    .context(ErrorKind::Serde)
                    .map_err(From::from)
            }).and_then(|b| {
                Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .map_err(Error::from)
            }).unwrap_or_else(|e| e.into_response());

        Box::new(future::ok(response))
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
        fn with_fail_call(mut self, fail_call: bool) -> TestHsm {
            self.fail_call = fail_call;
            self
        }

        fn with_cert(mut self, cert: TestCert) -> TestHsm {
            self.cert = cert;
            self
        }
    }

    impl GetTrustBundle for TestHsm {
        type Certificate = TestCert;

        fn get_trust_bundle(&self) -> Result<TestCert, CoreError> {
            if self.fail_call {
                Err(CoreError::from(CoreErrorKind::Io))
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
            TestHsm::default().with_cert(TestCert::default().with_cert("boo".as_bytes().to_vec())),
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
            }).wait()
            .unwrap();
    }
}
