// Copyright (c) Microsoft. All rights reserved.

use base64;
use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use serde_json;

use edgelet_core::Encrypt;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use workload::models::{EncryptRequest, EncryptResponse};

use error::{EncryptionOperation, Error, ErrorKind};
use IntoResponse;

pub struct EncryptHandler<T: Encrypt> {
    hsm: T,
}

impl<T: Encrypt> EncryptHandler<T> {
    pub fn new(hsm: T) -> Self {
        EncryptHandler { hsm }
    }
}

impl<T> Handler<Parameters> for EncryptHandler<T>
where
    T: Encrypt + 'static + Clone + Send + Sync,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
        let hsm = self.hsm.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let genid = params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("genid")))?;
                Ok((name, genid))
            }).map(|(module_id, genid)| {
                let id = format!("{}{}", module_id.to_string(), genid.to_string());
                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                    Ok((id, body))
                })
            }).into_future()
            .flatten()
            .and_then(move |(id, body)| -> Result<_, Error> {
                let request: EncryptRequest =
                    serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;
                let plaintext =
                    base64::decode(request.plaintext()).context(ErrorKind::MalformedRequestBody)?;
                let initialization_vector = base64::decode(request.initialization_vector())
                    .context(ErrorKind::MalformedRequestBody)?;
                let ciphertext = hsm
                    .encrypt(id.as_bytes(), &plaintext, &initialization_vector)
                    .context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                let encoded = base64::encode(&ciphertext);
                let response = EncryptResponse::new(encoded);
                let body = serde_json::to_string(&response)
                    .context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .context(ErrorKind::EncryptionOperation(EncryptionOperation::Encrypt))?;
                Ok(response)
            }).or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::Encrypt;
    use edgelet_core::Error as CoreError;
    use edgelet_http::route::Parameters;
    use futures::Future;
    use hyper::{Request, StatusCode};
    use workload::models::EncryptResponse;
    use workload::models::ErrorResponse;

    use super::*;

    #[derive(Clone, Debug, Default)]
    struct TestHsm {}

    impl TestHsm {}

    impl Encrypt for TestHsm {
        type Buffer = Vec<u8>;

        fn encrypt(
            &self,
            _client_id: &[u8],
            plaintext: &[u8],
            _initialization_vector: &[u8],
        ) -> Result<Self::Buffer, CoreError> {
            let mut rev = plaintext.to_vec();
            rev.reverse(); // this "encrypt" function simply reverses the buffer's contents
            Ok(rev)
        }
    }

    fn create_args(
        request: Option<&EncryptRequest>,
        params: Option<Vec<(Option<String>, String)>>,
    ) -> (Request<Body>, Parameters) {
        let request = match request {
            Some(req) => {
                let body = serde_json::to_string(req).unwrap();
                Request::builder().body(body.into()).unwrap()
            }
            None => Request::builder().body(Body::from("xyz")).unwrap(),
        };
        let params = match params {
            Some(param_list) => Parameters::with_captures(param_list),
            None => Parameters::default(),
        };
        (request, params)
    }

    const RAW_TEXT: &str = "!@#$%";

    macro_rules! raw_text {
        () => {
            RAW_TEXT.to_string()
        };
    }

    macro_rules! b64_text {
        () => {
            base64::encode(RAW_TEXT)
        };
    }

    macro_rules! b64_reversed {
        () => {
            base64::encode(&RAW_TEXT.chars().rev().collect::<String>())
        };
    }

    macro_rules! params_ok {
        () => {
            Some(vec![
                (Some("name".to_string()), "test".to_string()),
                (Some("genid".to_string()), "I".to_string()),
            ])
        };
    }

    fn request_ok() -> EncryptRequest {
        EncryptRequest::new(b64_text!(), b64_text!())
    }

    fn request_with_unencoded_plaintext() -> EncryptRequest {
        EncryptRequest::new(raw_text!(), b64_text!())
    }

    fn request_with_unencoded_init_vector() -> EncryptRequest {
        EncryptRequest::new(b64_text!(), raw_text!())
    }

    fn args_ok() -> (Request<Body>, Parameters) {
        create_args(Some(&request_ok()), params_ok!())
    }

    fn args_with_empty_params() -> (Request<Body>, Parameters) {
        create_args(Some(&request_ok()), None)
    }

    fn args_with_no_name() -> (Request<Body>, Parameters) {
        create_args(
            Some(&request_ok()),
            Some(vec![(Some("genid".to_string()), "I".to_string())]),
        )
    }

    fn args_with_no_genid() -> (Request<Body>, Parameters) {
        create_args(
            Some(&request_ok()),
            Some(vec![(Some("name".to_string()), "test".to_string())]),
        )
    }

    fn args_with_bad_request() -> (Request<Body>, Parameters) {
        create_args(None, params_ok!())
    }

    fn assert_response_message_eq(expected: &'static str, response: Response<Body>) {
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(expected, error_response.message());
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn handler_responds_with_ok() {
        let (request, params) = args_ok();
        let handler = EncryptHandler::new(TestHsm::default());

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::OK, response.status());

        let body = response
            .into_body()
            .concat2()
            .map(move |b| serde_json::from_slice::<EncryptResponse>(&b).unwrap())
            .wait()
            .unwrap();

        assert_eq!(b64_reversed!(), body.ciphertext().to_string());
    }

    #[test]
    fn handler_responds_with_bad_request_when_params_are_missing() {
        let (request, params) = args_with_empty_params();
        let handler = EncryptHandler::new(TestHsm::default());

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_response_message_eq("The request is missing required parameter `name`", response);
    }

    #[test]
    fn handler_responds_with_bad_request_when_name_is_missing() {
        let (request, params) = args_with_no_name();
        let handler = EncryptHandler::new(TestHsm::default());

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_response_message_eq("The request is missing required parameter `name`", response);
    }

    #[test]
    fn handler_responds_with_bad_request_when_genid_is_missing() {
        let (request, params) = args_with_no_genid();
        let handler = EncryptHandler::new(TestHsm::default());

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_response_message_eq(
            "The request is missing required parameter `genid`",
            response,
        );
    }

    #[test]
    fn handler_responds_with_bad_request_when_request_is_malformed() {
        let (request, params) = args_with_bad_request();
        let handler = EncryptHandler::new(TestHsm::default());

        let response = handler.handle(request, params).wait().unwrap();

        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        assert_response_message_eq(
            "Request body is malformed\n\tcaused by: expected value at line 1 column 1",
            response,
        );
    }

    #[test]
    fn handler_responds_with_unprocessable_entity_when_request_args_are_not_base64_encoded() {
        let bodies = &[
            request_with_unencoded_plaintext(),
            request_with_unencoded_init_vector(),
        ];
        let handler = EncryptHandler::new(TestHsm::default());

        for body in bodies {
            let (request, params) = create_args(Some(body), params_ok!());
            let response = handler.handle(request, params).wait().unwrap();

            assert_eq!(StatusCode::BAD_REQUEST, response.status());
            assert_response_message_eq(
                "Request body is malformed\n\tcaused by: Encoded text cannot have a 6-bit remainder.",
                response,
            );
        }
    }
}
