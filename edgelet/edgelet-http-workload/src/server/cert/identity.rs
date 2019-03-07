// Copyright (c) Microsoft. All rights reserved.

use super::{compute_validity, refresh_cert};
use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::{Body, Request, Response};
use serde_json;

use edgelet_core::{
    Certificate, CertificateProperties, CertificateType, CreateCertificate, WorkloadConfig,
};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use edgelet_utils::{ensure_not_empty_with_context, prepare_cert_uri_module};
use workload::models::IdentityCertificateRequest;

use crate::error::{CertOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct IdentityCertHandler<T: CreateCertificate, W: WorkloadConfig> {
    hsm: T,
    config: W,
}

impl<T: CreateCertificate, W: WorkloadConfig> IdentityCertHandler<T, W> {
    pub fn new(hsm: T, config: W) -> Self {
        IdentityCertHandler { hsm, config }
    }
}

impl<T, W> Handler<Parameters> for IdentityCertHandler<T, W>
where
    T: CreateCertificate + Clone + Send + Sync + 'static,
    <T as CreateCertificate>::Certificate: Certificate,
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let hsm = self.hsm.clone();
        let cfg = self.config.clone();
        let max_duration = cfg.get_cert_max_duration(CertificateType::Client);

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(|module_id| {
                let cn = module_id.to_string();
                let alias = format!("{}identity", module_id);
                let module_uri =
                    prepare_cert_uri_module(cfg.iot_hub_name(), cfg.device_id(), module_id);

                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(ErrorKind::CertOperation(CertOperation::CreateIdentityCert))?;
                    Ok((cn, alias, module_uri, body))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(cn, alias, module_uri, body)| {
                let cert_req: IdentityCertificateRequest =
                    serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;

                let expiration = cert_req.expiration().map_or_else(
                    || Ok(max_duration),
                    |exp| compute_validity(exp, max_duration, ErrorKind::MalformedRequestBody),
                )?;
                #[allow(clippy::cast_sign_loss)]
                let expiration = match expiration {
                    expiration if expiration < 0 || expiration > max_duration => {
                        return Err(Error::from(ErrorKind::MalformedRequestBody));
                    }
                    expiration => expiration as u64,
                };

                ensure_not_empty_with_context(&cn, || {
                    ErrorKind::MalformedRequestParameter("name")
                })?;

                let sans = vec![module_uri];
                let props = CertificateProperties::new(
                    expiration,
                    cn,
                    CertificateType::Client,
                    alias.clone(),
                )
                .with_san_entries(sans);
                refresh_cert(
                    &hsm,
                    alias,
                    &props,
                    ErrorKind::CertOperation(CertOperation::CreateIdentityCert),
                )
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::result::Result as StdResult;
    use std::sync::Arc;

    use chrono::offset::Utc;
    use chrono::Duration;

    use edgelet_core::{
        CertificateProperties, CertificateType, CreateCertificate, Error as CoreError,
        ErrorKind as CoreErrorKind, KeyBytes, PrivateKey, WorkloadConfig,
    };
    use edgelet_test_utils::cert::TestCert;
    use workload::models::{CertificateResponse, ErrorResponse, IdentityCertificateRequest};

    use super::*;
    use hyper::StatusCode;

    const MAX_DURATION_SEC: u64 = 7200;

    #[derive(Clone, Default)]
    struct TestHsm {
        on_create: Option<
            Arc<
                Box<dyn Fn(&CertificateProperties) -> StdResult<TestCert, CoreError> + Send + Sync>,
            >,
        >,
    }

    impl TestHsm {
        fn with_on_create<F>(mut self, on_create: F) -> Self
        where
            F: Fn(&CertificateProperties) -> StdResult<TestCert, CoreError> + Send + Sync + 'static,
        {
            self.on_create = Some(Arc::new(Box::new(on_create)));
            self
        }
    }

    impl CreateCertificate for TestHsm {
        type Certificate = TestCert;

        fn create_certificate(
            &self,
            properties: &CertificateProperties,
        ) -> StdResult<Self::Certificate, CoreError> {
            let callback = self.on_create.as_ref().unwrap();
            callback(properties)
        }

        fn destroy_certificate(&self, _alias: String) -> StdResult<(), CoreError> {
            Ok(())
        }
    }

    struct TestWorkloadConfig {
        iot_hub_name: String,
        device_id: String,
        duration: i64,
    }

    impl Default for TestWorkloadConfig {
        #[allow(clippy::cast_possible_wrap, clippy::cast_sign_loss)]
        fn default() -> Self {
            assert!(MAX_DURATION_SEC < (i64::max_value() as u64));

            TestWorkloadConfig {
                iot_hub_name: String::from("zaphods_hub"),
                device_id: String::from("marvins_device"),
                duration: MAX_DURATION_SEC as i64,
            }
        }
    }

    #[derive(Clone)]
    struct TestWorkloadData {
        data: Arc<TestWorkloadConfig>,
    }

    impl Default for TestWorkloadData {
        fn default() -> Self {
            TestWorkloadData {
                data: Arc::new(TestWorkloadConfig::default()),
            }
        }
    }

    impl WorkloadConfig for TestWorkloadData {
        fn iot_hub_name(&self) -> &str {
            self.data.iot_hub_name.as_str()
        }

        fn device_id(&self) -> &str {
            self.data.device_id.as_str()
        }

        fn get_cert_max_duration(&self, _cert_type: CertificateType) -> i64 {
            self.data.duration
        }
    }

    fn test_module_uri(module_id: &str) -> String {
        prepare_cert_uri_module("zaphods_hub", "marvins_device", module_id)
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
    fn missing_name_in_path() {
        let handler = IdentityCertHandler::new(TestHsm::default(), TestWorkloadData::default());
        let request = Request::get("http://localhost/modules//certificate/identity")
            .body("{}".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "The request is missing required parameter `name`",
            parse_error_response(response).message()
        );
    }

    #[test]
    fn succeeds_with_private_key_bytes() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());
        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("key", cert_resp.private_key().type_());
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().bytes());
    }

    #[test]
    fn succeeds_with_private_key_ref() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default().with_private_key(PrivateKey::Ref("Betelgeuse".to_string())))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());
        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("ref", cert_resp.private_key().type_());
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().ref_());
    }

    #[test]
    fn empty_expiration_ok() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert_eq!(MAX_DURATION_SEC, *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new();

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());
        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("key", cert_resp.private_key().type_());
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().bytes());
    }

    #[test]
    fn long_expiration_capped_to_max_duration_ok() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert_eq!(MAX_DURATION_SEC, *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(7000)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::CREATED, response.status());
        let cert_resp = response
            .into_body()
            .concat2()
            .and_then(|b| Ok(serde_json::from_slice::<CertificateResponse>(&b).unwrap()))
            .wait()
            .unwrap();
        assert_eq!("key", cert_resp.private_key().type_());
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().bytes());
    }

    #[test]
    fn whitespace_expiration_fails() {
        let handler = IdentityCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = IdentityCertificateRequest::new().with_expiration("       ".to_string());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: Argument is empty or only has whitespace - []",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn invalid_expiration_fails() {
        let handler = IdentityCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req =
            IdentityCertificateRequest::new().with_expiration("Umm.. No.. Just no..".to_string());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: input contains invalid characters",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn past_expiration_fails() {
        let handler = IdentityCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration("1999-06-28T16:39:57-08:00".to_string());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);

        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn empty_body_fails() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert_eq!(MAX_DURATION_SEC, *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body("".into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: EOF while parsing a value at line 1 column 0",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn create_cert_fails() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Err(CoreError::from(CoreErrorKind::KeyStore))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not create identity cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn pem_fails() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default().with_fail_pem(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not create identity cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn private_key_fails() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default().with_fail_private_key(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not create identity cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn get_cert_time_fails() {
        let handler = IdentityCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("beeblebrox", props.common_name());
                assert_eq!("beeblebroxidentity", props.alias());
                assert_eq!(CertificateType::Client, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                let expected_uri = test_module_uri("beeblebrox");
                assert!(props.san_entries().unwrap().contains(&expected_uri));
                Ok(TestCert::default().with_fail_valid_to(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = IdentityCertificateRequest::new()
            .with_expiration((Utc::now() + Duration::hours(1)).to_rfc3339());

        let request = Request::get("http://localhost/modules/beeblebrox/certificate/identity")
            .body(serde_json::to_string(&cert_req).unwrap().into())
            .unwrap();

        let params =
            Parameters::with_captures(vec![(Some("name".to_string()), "beeblebrox".to_string())]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not create identity cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }
}
