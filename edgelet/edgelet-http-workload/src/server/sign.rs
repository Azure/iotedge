// Copyright (c) Microsoft. All rights reserved.

use base64;
use failure::ResultExt;
use futures::{future, Future, Stream};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use serde_json;

use edgelet_core::KeyStore;
use edgelet_core::crypto::{Sign, SignatureAlgorithm};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use workload::models::{SignRequest, SignResponse};

use error::{Error, ErrorKind};
use IntoResponse;

pub struct SignHandler<K>
where
    K: 'static + KeyStore + Clone,
{
    key_store: K,
}

impl<K> SignHandler<K>
where
    K: 'static + KeyStore + Clone,
{
    pub fn new(key_store: K) -> Self {
        SignHandler { key_store }
    }
}

pub fn sign<K: KeyStore>(
    key_store: K,
    id: String,
    request: SignRequest,
) -> Result<SignResponse, Error> {
    key_store
        .get(&id, request.key_id())
        .ok_or_else(|| Error::from(ErrorKind::NotFound))
        .and_then(|k| {
            let data: Vec<u8> = base64::decode(request.data())?;
            let signature = k.sign(SignatureAlgorithm::HMACSHA256, &data)?;
            let encoded = base64::encode(signature.as_bytes());
            Ok(SignResponse::new(encoded))
        })
}

impl<K> Handler<Parameters> for SignHandler<K>
where
    K: 'static + KeyStore + Clone,
{
    fn handle(&self, req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .map(|name| {
                let id = name.to_string();
                let key_store = self.key_store.clone();
                let ok = req.body().concat2().map(move |b| {
                    serde_json::from_slice::<SignRequest>(&b)
                        .context(ErrorKind::BadBody)
                        .map_err(From::from)
                        .and_then(|request| sign(key_store, id, request))
                        .and_then(|r| {
                            serde_json::to_string(&r)
                                .context(ErrorKind::Serde)
                                .map_err(From::from)
                        })
                        .map(|b| {
                            Response::new()
                                .with_status(StatusCode::Ok)
                                .with_header(ContentLength(b.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(b)
                        })
                        .unwrap_or_else(|e| e.into_response())
                });
                future::Either::A(ok)
            })
            .unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use edgelet_core::KeyStore;
    use edgelet_core::crypto::MemoryKey;
    use edgelet_http::route::Parameters;
    use hyper::{Method, StatusCode, Uri};
    use hyper::server::Request;
    use workload::models::ErrorResponse;

    use super::*;

    #[derive(Clone, Debug)]
    struct TestKeyStore {
        key: Option<MemoryKey>,
    }

    impl TestKeyStore {
        pub fn new(key: Option<MemoryKey>) -> Self {
            TestKeyStore { key }
        }
    }

    impl KeyStore for TestKeyStore {
        type Key = MemoryKey;

        fn get(&self, _identity: &str, _key_name: &str) -> Option<Self::Key> {
            self.key.clone()
        }
    }

    #[test]
    fn success() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(Some(key));
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules/name/sign").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        let expected = "97yD9DBThCSxMpjmqm+xQ+9NWaFJRhdZl0edvC0aPNg=";
        assert_eq!(StatusCode::Ok, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let sign_response: SignResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(expected, sign_response.digest());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn not_found() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(None);
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules/name/sign").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NotFound, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!("Module not found", error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn sign_bad_params() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(Some(key));
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules/unknown/sign").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BadRequest, response.status());
    }

    #[test]
    fn bad_data_base64() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(Some(key));
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            "alsjdfasf".to_string(),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules/name/sign").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::UnprocessableEntity, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                  "Invalid base64 string\n\tcaused by: Encoded text cannot have a 6-bit remainder.",
                  error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_body() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(Some(key));
        let handler = SignHandler::new(store);

        let body = "invalid";

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let mut request = Request::new(
            Method::Post,
            Uri::from_str("http://localhost/modules/name/sign").unwrap(),
        );
        request.set_body(body);

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BadRequest, response.status());
        response
            .body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected = "Bad body\n\tcaused by: expected value at line 1 column 1";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
