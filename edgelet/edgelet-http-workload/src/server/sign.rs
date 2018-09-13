// Copyright (c) Microsoft. All rights reserved.

use base64;
use edgelet_core::crypto::{KeyIdentity, KeyStore, Sign, Signature, SignatureAlgorithm};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use failure::ResultExt;
use futures::{future, Future, Stream};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Request, Response, StatusCode};
use hyper::{Body, Error as HyperError};
use serde_json;
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

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
pub fn sign<K: KeyStore>(
    key_store: K,
    id: String,
    request: SignRequest,
) -> Result<SignResponse, Error> {
    key_store
        .get(&KeyIdentity::Module(id), request.key_id())
        .context(ErrorKind::NotFound)
        .map_err(Error::from)
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
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> BoxFuture<Response<Body>, HyperError> {
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::BadParam))
            .and_then(|name| {
                params
                    .name("genid")
                    .ok_or_else(|| Error::from(ErrorKind::BadParam))
                    .map(|genid| (name, genid))
            }).map(|(name, genid)| {
                let id = name.to_string();
                let genid = genid.to_string();
                let key_store = self.key_store.clone();
                let ok = req.into_body().concat2().map(move |b| {
                    serde_json::from_slice::<SignRequest>(&b)
                        .context(ErrorKind::BadBody)
                        .map_err(From::from)
                        .and_then(|request| {
                            let key_id = format!("{}{}", request.key_id(), genid);
                            sign(key_store, id, request.with_key_id(key_id))
                        }).and_then(|r| {
                            serde_json::to_string(&r)
                                .context(ErrorKind::Serde)
                                .map_err(From::from)
                        }).and_then(|b| {
                            Response::builder()
                                .status(StatusCode::OK)
                                .header(CONTENT_TYPE, "application/json")
                                .header(CONTENT_LENGTH, b.len().to_string().as_str())
                                .body(b.into())
                                .map_err(From::from)
                        }).unwrap_or_else(|e| e.into_response())
                });
                future::Either::A(ok)
            }).unwrap_or_else(|e| future::Either::B(future::ok(e.into_response())));
        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::rc::Rc;

    use edgelet_core::crypto::MemoryKey;
    use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind, KeyStore};
    use edgelet_http::route::Parameters;
    use workload::models::ErrorResponse;

    use super::*;

    #[derive(Debug)]
    struct State {
        last_id: String,
        last_key_name: String,
    }

    impl State {
        fn new() -> State {
            State {
                last_id: "".to_string(),
                last_key_name: "".to_string(),
            }
        }
    }

    #[derive(Clone, Debug)]
    struct TestKeyStore {
        key: MemoryKey,
        state: Rc<RefCell<State>>,
    }

    impl TestKeyStore {
        pub fn new(key: MemoryKey) -> Self {
            TestKeyStore {
                key,
                state: Rc::new(RefCell::new(State::new())),
            }
        }
    }

    impl KeyStore for TestKeyStore {
        type Key = MemoryKey;

        fn get(&self, identity: &KeyIdentity, key_name: &str) -> Result<Self::Key, CoreError> {
            self.state.borrow_mut().last_id = match identity {
                KeyIdentity::Device => "".to_string(),
                KeyIdentity::Module(ref m) => m.to_string(),
            };
            self.state.borrow_mut().last_key_name = key_name.to_string();
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
            Err(CoreError::from(CoreErrorKind::NotFound))
        }
    }

    #[test]
    fn success() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(key);
        let handler = SignHandler::new(store.clone());

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters = Parameters::with_captures(vec![
            (Some("name".to_string()), "test".to_string()),
            (Some("genid".to_string()), "g1".to_string()),
        ]);
        let request = Request::post("http://localhost/modules/name/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        let expected = "97yD9DBThCSxMpjmqm+xQ+9NWaFJRhdZl0edvC0aPNg=";
        assert_eq!(StatusCode::OK, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let sign_response: SignResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(expected, sign_response.digest());
                Ok(())
            }).wait()
            .unwrap();
        assert_eq!(&store.state.borrow().last_id, "test");
        assert_eq!(&store.state.borrow().last_key_name, "primaryg1");
    }

    #[test]
    fn not_found() {
        // arrange
        let store = NullKeyStore::new();
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters = Parameters::with_captures(vec![
            (Some("name".to_string()), "test".to_string()),
            (Some("genid".to_string()), "g1".to_string()),
        ]);
        let request = Request::post("http://localhost/modules/name/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::NOT_FOUND, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                    "Module not found\n\tcaused by: Item not found.",
                    error_response.message()
                );
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn sign_bad_params_name() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(key);
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/modules/unknown/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }

    #[test]
    fn sign_bad_params_genid() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(key);
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            base64::encode("The quick brown fox jumps over the lazy dog"),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters =
            Parameters::with_captures(vec![(Some("name".to_string()), "test".to_string())]);
        let request = Request::post("http://localhost/modules/unknown/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
    }

    #[test]
    fn bad_data_base64() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(key);
        let handler = SignHandler::new(store);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "hmac".to_string(),
            "alsjdfasf".to_string(),
        );
        let body = serde_json::to_string(&sign_request).unwrap();

        let parameters = Parameters::with_captures(vec![
            (Some("name".to_string()), "test".to_string()),
            (Some("genid".to_string()), "g1".to_string()),
        ]);
        let request = Request::post("http://localhost/modules/name/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::UNPROCESSABLE_ENTITY, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(
                  "Invalid base64 string\n\tcaused by: Encoded text cannot have a 6-bit remainder.",
                  error_response.message());
                Ok(())
            }).wait()
            .unwrap();
    }

    #[test]
    fn bad_body() {
        // arrange
        let key = MemoryKey::new("key");
        let store = TestKeyStore::new(key);
        let handler = SignHandler::new(store);

        let body = "invalid";

        let parameters = Parameters::with_captures(vec![
            (Some("name".to_string()), "test".to_string()),
            (Some("genid".to_string()), "g1".to_string()),
        ]);
        let request = Request::post("http://localhost/modules/name/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler.handle(request, parameters).wait().unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected = "Bad body\n\tcaused by: expected value at line 1 column 1";
                assert_eq!(expected, error_response.message());
                Ok(())
            }).wait()
            .unwrap();
    }
}
