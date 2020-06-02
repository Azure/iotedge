// Copyright (c) Microsoft. All rights reserved.
use crate::error::{Error, ErrorKind};
use edgelet_core::crypto::{KeyStore, Sign, SignatureAlgorithm};
use edgelet_core::{KeyIdentity, Signature};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use failure::ResultExt;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use crate::IntoResponse;
use keyservice::models::{SignRequest, SignResponse};

pub struct SignHandler<K: KeyStore> {
    key_store: K,
}

impl<K: KeyStore> SignHandler<K> {
    pub fn new(key_store: K) -> Self {
        SignHandler { key_store }
    }
}

impl<K> Handler<Parameters> for SignHandler<K>
where
    K: 'static + KeyStore + Clone + Sync + Send,
    K::Key: Sign,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let key_store = self.key_store.clone();

        let response = req
            .into_body()
            .concat2()
            .then(move |b| {
                let b = b.context(ErrorKind::MalformedRequestBody)?;
                let request = serde_json::from_slice::<SignRequest>(&b)
                    .context(ErrorKind::MalformedRequestBody)?;
                let message = base64::decode(
                    request.parameters().message())
                    .context(ErrorKind::MalformedRequestBody)?;
                let key_handle = request.key_handle();
                let device_key = key_store.get(
                    &KeyIdentity::Device, key_handle)
                    .context(ErrorKind::DeviceKeyNotFound)?;
                let signature = device_key
                    .sign(SignatureAlgorithm::HMACSHA256, &message)
                    .map(|s| base64::encode(s.as_bytes()))
                    .context(ErrorKind::GetSignature)
                    .unwrap();

                let sign_response = SignResponse::new(signature);

                let body = serde_json::to_string(&sign_response)
                    .context(ErrorKind::GetSignature)
                    .unwrap();

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .with_context(|_| ErrorKind::GetSignature)?;

                Ok(response)
            })
            .or_else(|e: Error| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::crypto::MemoryKey;
    use crate::sign::SignHandler;
    use edgelet_http::route::{Parameters, Handler};
    use futures::{Stream, Future};
    use keyservice::models::{ErrorResponse, SignRequest, SignParameters, SignResponse};
    use edgelet_core::{KeyStore, KeyIdentity, ErrorKind as CoreErrorKind};
    use hyper::{Request, StatusCode};
    use edgelet_core::Error as CoreError;

    #[derive(Clone, Debug)]
    struct TestKeyStore {
        key: MemoryKey,
    }

    impl TestKeyStore {
        pub fn new(key: MemoryKey) -> Self {
            TestKeyStore {
                key,
            }
        }
    }

    impl KeyStore for TestKeyStore {
        type Key = MemoryKey;

        fn get(&self, _identity: &KeyIdentity, _key_name: &str) -> Result<Self::Key, CoreError> {
            Ok(self.key.clone())
        }
    }

    #[derive(Clone, Debug)]
    struct NullKeyStore;

    impl NullKeyStore {
        pub fn new() -> Self {
            NullKeyStore
        }
    }

    impl KeyStore for NullKeyStore {
        type Key = MemoryKey;

        fn get(&self, _identity: &KeyIdentity, _key_name: &str) -> Result<Self::Key, CoreError> {
            Err(CoreError::from(CoreErrorKind::KeyStoreItemNotFound))
        }
    }

    #[test]
    fn bad_body_not_json() {
        // arrange
        let key = MemoryKey::new("");
        let key_store = TestKeyStore::new(key);
        let handler = SignHandler::new(key_store);

        let body = "not valid json";

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::default()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Request body is malformed\n\tcaused by: expected ident at line 1 column 2";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn key_not_found() {
        // arrange
        let key_store = NullKeyStore::new();
        let handler = SignHandler::new(key_store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "HMAC-SHA256".to_string(),
            SignParameters::new(base64::encode("12345")),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::default()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NOT_FOUND, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Device key not found\n\tcaused by: Item not found.";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_body_invalid_encoding() {
        // arrange
        let key = MemoryKey::new("");
        let key_store = TestKeyStore::new(key);
        let handler = SignHandler::new(key_store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "HMAC-SHA256".to_string(),
            SignParameters::new("not_base64_encoded".to_string()),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::default()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Request body is malformed\n\tcaused by: Invalid byte 95, offset 3.";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn success() {
        // arrange
        let key = MemoryKey::new("");
        let key_store = TestKeyStore::new(key);
        let handler = SignHandler::new(key_store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "HMAC-SHA256".to_string(),
            SignParameters::new(base64::encode("12345")),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::default()).wait().unwrap();

        // assert
        let expected = "riPI9XPTbHodbLyLC+vlLgZm3PFPoEQHMo+5RLj3qC0=";
        assert_eq!(StatusCode::OK, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let sign_response: SignResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(expected, sign_response.signature());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
