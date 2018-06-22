// Copyright (c) Microsoft. All rights reserved.

use chrono::prelude::*;
use failure::ResultExt;
use futures::{future, Future, Stream};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use serde_json;

use edgelet_core::{
    Certificate, CertificateProperties, CertificateType, CreateCertificate, KeyBytes, PrivateKey,
};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use workload::models::{
    CertificateResponse, PrivateKey as PrivateKeyResponse, ServerCertificateRequest,
};

use error::{Error, ErrorKind, Result};
use IntoResponse;

pub struct ServerCertHandler<T: CreateCertificate> {
    hsm: T,
}

impl<T: CreateCertificate> ServerCertHandler<T> {
    pub fn new(hsm: T) -> Self {
        ServerCertHandler { hsm }
    }
}

impl<T> Handler<Parameters> for ServerCertHandler<T>
where
    T: CreateCertificate + 'static + Clone,
    <T as CreateCertificate>::Certificate: Certificate,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let hsm = self.hsm.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .and_then(|name| {
                params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::BadParam))
                    .map(|genid| (name, genid))
            })
            .map(|(module_id, genid)| {
                let alias = format!("{}{}", module_id.to_string(), genid.to_string());
                let result = req.into_body()
                    .concat2()
                    .map(move |body| {
                        serde_json::from_slice::<ServerCertificateRequest>(&body)
                            .context(ErrorKind::BadBody)
                            .map_err(Error::from)
                            .and_then(|cert_req| {
                                compute_validity(ensure_not_empty!(cert_req.expiration()).as_str())
                                    .map(|expiration| (cert_req, expiration))
                            })
                            .and_then(move |(cert_req, expiration)| {
                                let props = CertificateProperties::new(
                                    ensure_range!(expiration, 0, i64::max_value()) as u64,
                                    ensure_not_empty!(cert_req.common_name().to_string()),
                                    CertificateType::Server,
                                    alias,
                                );
                                hsm.create_certificate(&props)
                                    .map_err(Error::from)
                                    .and_then(|cert| {
                                        let cert = cert_to_response(
                                            &cert,
                                            cert_req.expiration().as_str(),
                                        )?;
                                        let body = serde_json::to_string(&cert)?;
                                        Response::builder()
                                            .status(StatusCode::CREATED)
                                            .header(CONTENT_TYPE, "application/json")
                                            .header(CONTENT_LENGTH, body.len().to_string().as_str())
                                            .body(body.into())
                                            .map_err(From::from)
                                    })
                            })
                            .unwrap_or_else(|e| e.into_response())
                    })
                    .map_err(Error::from)
                    .or_else(|e| future::ok(e.into_response()));

                future::Either::A(result)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));

        Box::new(response)
    }
}

fn cert_to_response<T: Certificate>(cert: &T, expiration: &str) -> Result<CertificateResponse> {
    let cert_buffer = cert.pem()?;

    let private_key = match cert.get_private_key()? {
        Some(PrivateKey::Ref(ref_)) => PrivateKeyResponse::new("ref".to_string()).with_ref(ref_),
        Some(PrivateKey::Key(KeyBytes::Pem(buffer))) => PrivateKeyResponse::new("key".to_string())
            .with_bytes(String::from_utf8_lossy(buffer.as_ref()).to_string()),
        None => Err(ErrorKind::BadPrivateKey)?,
    };

    Ok(CertificateResponse::new(
        private_key,
        String::from_utf8_lossy(cert_buffer.as_ref()).to_string(),
        expiration.to_string(),
    ))
}

fn compute_validity(expiration: &str) -> Result<i64> {
    DateTime::parse_from_rfc3339(expiration)
        .map(|expiration| {
            expiration
                .with_timezone(&Utc)
                .signed_duration_since(Utc::now())
                .num_seconds()
        })
        .map_err(Error::from)
}

#[cfg(test)]
mod tests {
    use std::rc::Rc;
    use std::result::Result as StdResult;

    use chrono::offset::Utc;
    use chrono::Duration;

    use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
    use edgelet_test_utils::cert::TestCert;
    use workload::models::ErrorResponse;

    use super::*;

    #[derive(Clone, Default)]
    struct TestHsm {
        on_create: Option<Rc<Box<Fn(&CertificateProperties) -> StdResult<TestCert, CoreError>>>>,
    }

    impl TestHsm {
        fn with_on_create<F>(mut self, on_create: F) -> TestHsm
        where
            F: Fn(&CertificateProperties) -> StdResult<TestCert, CoreError> + 'static,
        {
            self.on_create = Some(Rc::new(Box::new(on_create)));
            self
        }
    }

    impl CreateCertificate for TestHsm {
        type Certificate = TestCert;

        fn create_certificate(
            &self,
            properties: &CertificateProperties,
        ) -> StdResult<TestCert, CoreError> {
            let callback = self.on_create.as_ref().unwrap();
            callback(properties)
        }
    }

    fn parse_error_response(response: Response<Body>) -> ErrorResponse {
        response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<ErrorResponse>(&b).unwrap()))
            .wait()
            .unwrap()
    }

    #[test]
    fn missing_name() {
        let handler = ServerCertHandler::new(TestHsm::default());
        let request = Request::get("http://localhost/modules//genid/I/certificate/server")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!("Bad parameter", parse_error_response(response).message());
    }

    #[test]
    fn missing_genid() {
        let handler = ServerCertHandler::new(TestHsm::default());
        let request = Request::get("http://localhost/modules/beelebrox/genid//certificate/server")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!("Bad parameter", parse_error_response(response).message());
    }

    #[test]
    fn empty_body() {
        let handler = ServerCertHandler::new(TestHsm::default());
        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/II/certificate/server",
        ).body("".into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "II".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_ne!(
            parse_error_response(response).message().find("Bad body"),
            None
        );
    }

    #[test]
    fn bad_body() {
        let handler = ServerCertHandler::new(TestHsm::default());
        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/III/certificate/server",
        ).body("The answer is 42.".into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "III".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_ne!(
            parse_error_response(response).message().find("Bad body"),
            None
        );
    }

    #[test]
    fn empty_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req = ServerCertificateRequest::new("".to_string(), "".to_string());

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/IV/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "IV".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("Argument is empty or only has whitespace"),
            None
        );
    }

    #[test]
    fn whitespace_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req = ServerCertificateRequest::new("".to_string(), "       ".to_string());

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("Argument is empty or only has whitespace"),
            None
        );
    }

    #[test]
    fn invalid_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req =
            ServerCertificateRequest::new("".to_string(), "Umm.. No.. Just no..".to_string());

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("Invalid ISO 8601 date"),
            None
        );
    }

    #[test]
    fn past_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req =
            ServerCertificateRequest::new("".to_string(), "1999-06-28T16:39:57-08:00".to_string());

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("out of range [0, 9223372036854775807)"),
            None
        );
    }

    #[test]
    fn empty_common_name() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req = ServerCertificateRequest::new(
            "".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("Argument is empty or only has whitespace"),
            None
        );
    }

    #[test]
    fn white_space_common_name() {
        let handler = ServerCertHandler::new(TestHsm::default());

        let cert_req = ServerCertificateRequest::new(
            "      ".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("Argument is empty or only has whitespace"),
            None
        );
    }

    #[test]
    fn create_cert_fails() {
        let handler = ServerCertHandler::new(TestHsm::default().with_on_create(|props| {
            assert_eq!("marvin", props.common_name());
            Err(CoreError::from(CoreErrorKind::Io))
        }));

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("An IO error occurred"),
            None
        );
    }

    #[test]
    fn pem_fails() {
        let handler = ServerCertHandler::new(TestHsm::default().with_on_create(|props| {
            assert_eq!("marvin", props.common_name());
            Ok(TestCert::default().with_fail_pem(true))
        }));

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("An IO error occurred"),
            None
        );
    }

    #[test]
    fn private_key_fails() {
        let handler = ServerCertHandler::new(TestHsm::default().with_on_create(|props| {
            assert_eq!("marvin", props.common_name());
            Ok(TestCert::default().with_fail_private_key(true))
        }));

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_ne!(
            parse_error_response(response)
                .message()
                .find("An IO error occurred"),
            None
        );
    }

    #[test]
    fn succeeds_key() {
        let handler = ServerCertHandler::new(TestHsm::default().with_on_create(|props| {
            assert_eq!("marvin", props.common_name());
            Ok(TestCert::default()
                .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
        }));

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());

        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("key", cert_resp.private_key().type_());
        assert_eq!(
            Some(&"Betelgeuse".to_string()),
            cert_resp.private_key().bytes()
        );
    }

    #[test]
    fn succeeds_ref() {
        let handler = ServerCertHandler::new(TestHsm::default().with_on_create(|props| {
            assert_eq!("marvin", props.common_name());
            Ok(TestCert::default().with_private_key(PrivateKey::Ref("Betelgeuse".to_string())))
        }));

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request = Request::get(
            "http://localhost/modules/beeblebrox/genid/I/certificate/server",
        ).body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());

        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("ref", cert_resp.private_key().type_());
        assert_eq!(
            Some(&"Betelgeuse".to_string()),
            cert_resp.private_key().ref_()
        );
    }
}
