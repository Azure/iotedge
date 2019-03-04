// Copyright (c) Microsoft. All rights reserved.

use super::{compute_validity, refresh_cert};
use failure::ResultExt;
use futures::{future, Future, IntoFuture, Stream};
use hyper::{Body, Request, Response};
use serde_json;

use edgelet_core::{
    Certificate, CertificateProperties, CertificateType, CreateCertificate, WorkloadConfig,
};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use edgelet_utils::{
    append_dns_san_entries, ensure_not_empty_with_context, prepare_dns_san_entries,
};
use workload::models::ServerCertificateRequest;

use error::{CertOperation, Error, ErrorKind};
use IntoResponse;

pub struct ServerCertHandler<T: CreateCertificate, W: WorkloadConfig> {
    hsm: T,
    config: W,
}

impl<T: CreateCertificate, W: WorkloadConfig> ServerCertHandler<T, W> {
    pub fn new(hsm: T, config: W) -> Self {
        ServerCertHandler { hsm, config }
    }
}
impl<T, W> Handler<Parameters> for ServerCertHandler<T, W>
where
    T: CreateCertificate + Clone + Send + Sync + 'static,
    <T as CreateCertificate>::Certificate: Certificate,
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
        let hsm = self.hsm.clone();
        let cfg = self.config.clone();
        let max_duration = cfg.get_cert_max_duration(CertificateType::Server);

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let genid = params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("genid")))?;
                Ok((name, genid))
            })
            .map(|(module_id, genid)| {
                let module_id = module_id.to_string();
                let alias = format!("{}{}server", module_id, genid.to_string());

                req.into_body().concat2().then(move |body| {
                    let body =
                        body.context(ErrorKind::CertOperation(CertOperation::GetServerCert))?;
                    Ok((alias, body, module_id))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(alias, body, module_id)| {
                let cert_req: ServerCertificateRequest =
                    serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;

                let expiration = compute_validity(
                    cert_req.expiration(),
                    max_duration,
                    ErrorKind::MalformedRequestBody,
                )?;
                #[allow(clippy::cast_sign_loss)]
                let expiration = match expiration {
                    expiration if expiration < 0 || expiration > max_duration => {
                        return Err(Error::from(ErrorKind::MalformedRequestBody));
                    }
                    expiration => expiration as u64,
                };

                let common_name = cert_req.common_name();
                ensure_not_empty_with_context(common_name, || ErrorKind::MalformedRequestBody)?;

                // add a DNS SAN entry in the server cert that uses the module identifier as
                // an alternative DNS name; we also need to add the common_name that we are using
                // as a DNS name since the presence of a DNS name SAN will take precedence over
                // the common name
                let sans = vec![append_dns_san_entries(
                    &prepare_dns_san_entries(&[&module_id]),
                    &[common_name],
                )];

                #[allow(clippy::cast_sign_loss)]
                let props = CertificateProperties::new(
                    expiration,
                    common_name.to_string(),
                    CertificateType::Server,
                    alias.clone(),
                )
                .with_san_entries(sans);
                let body = refresh_cert(
                    &hsm,
                    alias,
                    &props,
                    ErrorKind::CertOperation(CertOperation::GetServerCert),
                )?;
                Ok(body)
            })
            .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::result::Result as StdResult;
    use std::sync::Arc;

    use chrono::offset::Utc;
    use chrono::Duration;

    use super::*;
    use edgelet_core::{
        CertificateProperties, CertificateType, CreateCertificate, Error as CoreError,
        ErrorKind as CoreErrorKind, KeyBytes, PrivateKey, WorkloadConfig,
    };
    use edgelet_test_utils::cert::TestCert;
    use hyper::StatusCode;
    use workload::models::{CertificateResponse, ErrorResponse, ServerCertificateRequest};

    const MAX_DURATION_SEC: u64 = 7200;

    #[derive(Clone, Default)]
    struct TestHsm {
        on_create: Option<
            Arc<Box<Fn(&CertificateProperties) -> StdResult<TestCert, CoreError> + Send + Sync>>,
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
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());
        let request = Request::get("http://localhost/modules//genid/I/certificate/server")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "The request is missing required parameter `name`",
            parse_error_response(response).message()
        );
    }

    #[test]
    fn missing_genid() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());
        let request = Request::get("http://localhost/modules/beelebrox/genid//certificate/server")
            .body("".into())
            .unwrap();
        let response = handler.handle(request, Parameters::new()).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "The request is missing required parameter `name`",
            parse_error_response(response).message()
        );
    }

    #[test]
    fn empty_body() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());
        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/II/certificate/server")
                .body("".into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "II".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: EOF while parsing a value at line 1 column 0",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn bad_body() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());
        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/III/certificate/server")
                .body("The answer is 42.".into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "III".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: expected value at line 1 column 1",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn empty_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = ServerCertificateRequest::new("".to_string(), "".to_string());

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/IV/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "IV".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: Argument is empty or only has whitespace - []",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn whitespace_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = ServerCertificateRequest::new("".to_string(), "       ".to_string());

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: Argument is empty or only has whitespace - []",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn invalid_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req =
            ServerCertificateRequest::new("".to_string(), "Umm.. No.. Just no..".to_string());

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: input contains invalid characters",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn past_expiration() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req =
            ServerCertificateRequest::new("".to_string(), "1999-06-28T16:39:57-08:00".to_string());

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn empty_common_name() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = ServerCertificateRequest::new(
            "".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: Argument is empty or only has whitespace - []",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn white_space_common_name() {
        let handler = ServerCertHandler::new(TestHsm::default(), TestWorkloadData::default());

        let cert_req = ServerCertificateRequest::new(
            "      ".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_eq!(
            "Request body is malformed\n\tcaused by: Argument is empty or only has whitespace - []",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn create_cert_fails() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Err(CoreError::from(CoreErrorKind::KeyStore))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not get server cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn pem_fails() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Ok(TestCert::default().with_fail_pem(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not get server cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn private_key_fails() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Ok(TestCert::default().with_fail_private_key(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not get server cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }

    #[test]
    fn succeeds_key() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("2020marvin", props.common_name());
                assert_eq!("$beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                let san_entries = props.san_entries().unwrap();
                assert_eq!(1, san_entries.len());
                assert_eq!("DNS:2020marvin, DNS:beeblebrox", san_entries[0]);
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "2020marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/$beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "$beeblebrox".to_string()),
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
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().bytes());
    }

    #[test]
    fn succeeds_ref() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Ok(TestCert::default().with_private_key(PrivateKey::Ref("Betelgeuse".to_string())))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
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
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().ref_());
    }

    #[test]
    fn long_expiration_capped_to_max_duration_ok() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert_eq!(MAX_DURATION_SEC, *props.validity_in_secs());
                Ok(TestCert::default()
                    .with_private_key(PrivateKey::Key(KeyBytes::Pem("Betelgeuse".to_string()))))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(7000)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
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
        assert_eq!(Some("Betelgeuse"), cert_resp.private_key().bytes());
    }

    #[test]
    fn get_cert_time_fails() {
        let handler = ServerCertHandler::new(
            TestHsm::default().with_on_create(|props| {
                assert_eq!("marvin", props.common_name());
                assert_eq!("beeblebroxIserver", props.alias());
                assert_eq!(CertificateType::Server, *props.certificate_type());
                assert!(MAX_DURATION_SEC >= *props.validity_in_secs());
                Ok(TestCert::default().with_fail_valid_to(true))
            }),
            TestWorkloadData::default(),
        );

        let cert_req = ServerCertificateRequest::new(
            "marvin".to_string(),
            (Utc::now() + Duration::hours(1)).to_rfc3339(),
        );

        let request =
            Request::get("http://localhost/modules/beeblebrox/genid/I/certificate/server")
                .body(serde_json::to_string(&cert_req).unwrap().into())
                .unwrap();

        let params = Parameters::with_captures(vec![
            (Some("name".to_string()), "beeblebrox".to_string()),
            (Some("genid".to_string()), "I".to_string()),
        ]);
        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
        assert_eq!(
            "Could not get server cert\n\tcaused by: A error occurred in the key store.",
            parse_error_response(response).message(),
        );
    }
}
