// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use base64;
use chrono::{DateTime, Utc};
use futures::future::Either;
use futures::{future, Future};
use hyper::client::Service;
use hyper::{Error as HyperError, Method, Request, Response, StatusCode};
use percent_encoding::{percent_encode, PATH_SEGMENT_ENCODE_SET};
use serde_json;
use tokio::prelude::*;
use tokio::timer::Interval;
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_core::crypto::{KeyStore, Sign, Signature, SignatureAlgorithm};
use edgelet_http::ErrorKind as HttpErrorKind;
use edgelet_http::client::{Client, TokenSource};
use error::{Error, ErrorKind};
use model::{DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus,
            TpmRegistrationResult};

#[derive(Clone)]
pub struct DpsTokenSource<K>
where
    K: Sign + Clone,
{
    scope_id: String,
    registration_id: String,
    key: K,
}

impl<K> DpsTokenSource<K>
where
    K: Sign + Clone,
{
    fn new(scope_id: String, registration_id: String, key: K) -> Self {
        DpsTokenSource {
            scope_id,
            registration_id,
            key,
        }
    }
}

impl<K> TokenSource for DpsTokenSource<K>
where
    K: Sign + Clone,
{
    type Error = Error;

    fn get(&self, expiry: &DateTime<Utc>) -> Result<String, Error> {
        let expiry = expiry.timestamp().to_string();
        let audience = format!("{}/registrations/{}", self.scope_id, self.registration_id);

        let resource_uri =
            percent_encode(audience.to_lowercase().as_bytes(), PATH_SEGMENT_ENCODE_SET).to_string();
        let sig_data = format!("{}\n{}", &resource_uri, expiry);

        let signature = self.key
            .sign(SignatureAlgorithm::HMACSHA256, sig_data.as_bytes())
            .map(|s| base64::encode(s.as_bytes()))
            .map_err(Error::from)?;

        let token = UrlSerializer::new(format!("sr={}", resource_uri))
            .append_pair("sig", &signature)
            .append_pair("se", &expiry)
            .append_pair("skn", "registration")
            .finish();
        Ok(token)
    }
}

pub trait Activate {
    type Key;
    fn activate_identity_key(&mut self, key: Self::Key) -> Result<(), Error>;
}

pub trait NewKeyGetter {
    type Key;
    fn get_key<B: AsRef<[u8]>>(&self, key: B) -> Result<Self::Key, Error>;
}

pub struct DpsClient<S, K, A, G>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    K: 'static + Sign + Clone,
    A: 'static + KeyStore<Key = K> + Activate<Key = K> + Clone,
    G: 'static + NewKeyGetter<Key = K> + Clone,
{
    client: Arc<RwLock<Client<S, DpsTokenSource<K>>>>,
    scope_id: String,
    registration_id: String,
    key_store: A,
    key_getter: G,
    seconds_to_try_operation_status: u64,
}

impl<S, K, A, G> DpsClient<S, K, A, G>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    K: 'static + Sign + Clone,
    A: 'static + KeyStore<Key = K> + Activate<Key = K> + Clone,
    G: 'static + NewKeyGetter<Key = K> + Clone,
{
    pub fn new(
        client: Client<S, DpsTokenSource<K>>,
        scope_id: String,
        registration_id: String,
        key_store: A,
        key_getter: G,
        seconds_to_try_operation_status: u64,
    ) -> Result<DpsClient<S, K, A, G>, Error> {
        Ok(DpsClient {
            client: Arc::new(RwLock::new(client)),
            scope_id,
            registration_id,
            key_store,
            key_getter,
            seconds_to_try_operation_status,
        })
    }

    fn get_tpm_challenge_key(body: &str, key_getter: &G) -> Result<K, Error> {
        serde_json::from_str(body).map_err(Error::from).and_then(
            |tpm_challenge: TpmRegistrationResult| {
                tpm_challenge
                    .authentication_key()
                    .ok_or_else(|| Error::from(ErrorKind::InvalidTpmToken))
                    .and_then(|key_str| key_getter.get_key(key_str))
            },
        )
    }

    fn get_operation_id(
        client: &Arc<RwLock<Client<S, DpsTokenSource<K>>>>,
        scope_id: &str,
        registration_id: &str,
        registration: &DeviceRegistration,
        key: K,
    ) -> Box<Future<Item = Option<RegistrationOperationStatus>, Error = Error>> {
        let token_source =
            DpsTokenSource::new(scope_id.to_string(), registration_id.to_string(), key);

        let f = client
            .write()
            .expect("RwLock write failure")
            .clone()
            .with_token_source(token_source)
            .request::<DeviceRegistration, RegistrationOperationStatus>(
                Method::Put,
                &format!("{}/registrations/{}/register", scope_id, registration_id),
                None,
                Some(registration.clone()),
                false,
            )
            .map_err(Error::from);
        Box::new(f)
    }

    fn get_operation_status(
        client: &Arc<RwLock<Client<S, DpsTokenSource<K>>>>,
        scope_id: &str,
        registration_id: &str,
        operation_id: &str,
    ) -> Box<Future<Item = Option<DeviceRegistrationResult>, Error = Error>> {
        let request = client.read().expect("RwLock read failure")
            .request::<(), RegistrationOperationStatus>(
                Method::Get,
                &format!(
                    "{}/registrations/{}/operations/{}",
                    scope_id, registration_id, operation_id
                ),
                None,
                None,
                false,
            )
            .map_err(Error::from)
            .map(
                |operation_status: Option<RegistrationOperationStatus>| ->
                Option<DeviceRegistrationResult> {
                    let status: Option<DeviceRegistrationResult> = operation_status.map_or_else(
                        || None,
                        |op| {
                            op.registration_state().map_or_else(|| None, |r| {
                                Some(r.clone())
                            })
                        },
                    );
                    status
                },
            );
        Box::new(request)
    }

    // The purpose of this function is to poll DPS till it sends either an error or the device
    // credentials back. This function calls get_operation_status on a timer which in turns calls
    // in to DPS. The way polling is implemented is by generating a stream of timer events and
    // calling get_operation_status on each timer event. Stream processing is aborted if either the
    // timer generates an error or if get_operation_status returns an error. All results from
    // get_operation_status are discarded, but for the one that returns the desired result. The
    // skip_while and take(1) implement discarding all but the desired result. Finally fold is
    // called on the desired result to format and return it from the function.
    fn get_device_registration_result(
        client: Arc<RwLock<Client<S, DpsTokenSource<K>>>>,
        scope_id: String,
        registration_id: String,
        operation_id: String,
        seconds_to_try: u64,
    ) -> Box<Future<Item = Option<DeviceRegistrationResult>, Error = Error>> {
        let chain = Interval::new(Instant::now(), Duration::from_secs(1))
            .take(seconds_to_try)
            .map_err(|_| Error::from(ErrorKind::TimerError))
            .and_then(move |_instant: Instant| {
                Self::get_operation_status(
                    &client.clone(),
                    &scope_id,
                    &registration_id,
                    &operation_id,
                )
            })
            .skip_while(
                |registration_result: &Option<DeviceRegistrationResult>| -> Result<bool, Error> {
                    // if registration_result.is_some() {
                    //     Ok(false)
                    // } else {
                    //     Ok(true)
                    // }
                    Ok(registration_result.is_none())
                },
            )
            .take(1)
            .fold(
                None,
                |_final_result: Option<DeviceRegistrationResult>,
                 result_from_service: Option<DeviceRegistrationResult>| {
                    future::ok::<Option<DeviceRegistrationResult>, Error>(result_from_service)
                },
            );
        Box::new(chain)
    }

    pub fn register_with_auth(
        client: &Arc<RwLock<Client<S, DpsTokenSource<K>>>>,
        scope_id: String,
        registration_id: String,
        key_store: &A,
        key_getter: G,
    ) -> Box<Future<Item = Option<RegistrationOperationStatus>, Error = Error>> {
        let registration = DeviceRegistration::new().with_registration_id(registration_id.clone());
        let client_inner = client.clone();
        let mut key_store_inner = key_store.clone();
        let r = client
            .read()
            .expect("RwLock read failure")
            .request::<DeviceRegistration, TpmRegistrationResult>(
                Method::Put,
                &format!("{}/registrations/{}/register", scope_id, registration_id),
                None,
                Some(registration.clone()),
                false,
            )
            .then(move |result| {
                match result {
                    Ok(_) => Either::B(future::err(Error::from(ErrorKind::Unexpected))),
                    Err(err) => {
                        // If request is returned with status unauthorized, extract the tpm
                        // challenge from the payload, generate a signature and re-issue the
                        // request
                        let body =
                            if let HttpErrorKind::ServiceError(status, ref body) = *err.kind() {
                                if status == StatusCode::Unauthorized {
                                    Some(body.clone())
                                } else {
                                    None
                                }
                            } else {
                                None
                            };

                        body.map(move |body| {
                            Self::get_tpm_challenge_key(body.as_str(), &key_getter)
                                .and_then(move |key| {
                                    Activate::activate_identity_key(
                                        &mut key_store_inner,
                                        key.clone(),
                                    ).map(|_| key)
                                })
                                .map(move |key| {
                                    Either::A(Self::get_operation_id(
                                        &client_inner.clone(),
                                        scope_id.as_str(),
                                        registration_id.as_str(),
                                        &registration,
                                        key.clone(),
                                    ))
                                })
                                .unwrap_or_else(|err| Either::B(future::err(err)))
                        }).unwrap_or_else(|| Either::B(future::err(Error::from(err))))
                    }
                }
            });
        Box::new(r)
    }

    pub fn register(&self) -> Box<Future<Item = (String, String), Error = Error>> {
        let mut key_store_status = self.key_store.clone();
        let client_with_token_status = self.client.clone();
        let scope_id = self.scope_id.clone();
        let scope_id_status = self.scope_id.clone();
        let registration_id = self.registration_id.clone();
        let registration_id_status = self.registration_id.clone();
        let key_getter = self.key_getter.clone();
        let key_getter_status = self.key_getter.clone();
        let seconds_to_try = self.seconds_to_try_operation_status;
        let r = Self::register_with_auth(
            &self.client,
            scope_id,
            registration_id,
            &self.key_store,
            key_getter,
        ).and_then(
            move |operation_status: Option<RegistrationOperationStatus>| {
                operation_status
                    .map(move |s| {
                        Either::A(Self::get_device_registration_result(
                            client_with_token_status,
                            scope_id_status,
                            registration_id_status,
                            s.operation_id().clone(),
                            seconds_to_try,
                        ))
                    })
                    .unwrap_or_else(|| Either::B(future::err(Error::from(ErrorKind::NotAssigned))))
            },
        )
            .and_then(move |operation_status: Option<DeviceRegistrationResult>| {
                operation_status
                    .ok_or_else(|| Error::from(ErrorKind::NotAssigned))
                    .and_then(|s| -> Result<(String, String), Error> {
                        let tpm_result_inner = s.clone();
                        let tpm_result = s.tpm();
                        tpm_result
                            .ok_or_else(|| Error::from(ErrorKind::NotAssigned))
                            .and_then(|r| -> Result<(), Error> {
                                r.authentication_key()
                                    .ok_or_else(|| Error::from(ErrorKind::NotAssigned))
                                    .and_then(|kb| -> Result<(), Error> {
                                        key_getter_status.get_key(kb).and_then(
                                            |k| -> Result<(), Error> {
                                                key_store_status
                                                    .activate_identity_key(k)
                                                    .and_then(|_| Ok(()))
                                            },
                                        )
                                    })
                            })
                            .and_then(|_| -> Result<(String, String), Error> {
                                get_device_info(&tpm_result_inner)
                            })
                    })
            });
        Box::new(r)
    }
}

pub fn get_device_info(
    registration_result: &DeviceRegistrationResult,
) -> Result<(String, String), Error> {
    Ok((
        registration_result
            .device_id()
            .cloned()
            .ok_or_else(|| Error::from(ErrorKind::NotAssigned))?,
        registration_result
            .assigned_hub()
            .cloned()
            .ok_or_else(|| Error::from(ErrorKind::NotAssigned))?,
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::cell::RefCell;
    use std::mem;
    use std::str;

    use hyper::StatusCode;
    use hyper::header::Authorization;
    use hyper::server::service_fn;
    use serde_json;
    use tokio_core::reactor::Core;
    use url::Url;

    use edgelet_core::Error as CoreError;

    #[derive(Clone)]
    struct TestKey {
        key: String,
    }
    impl Sign for TestKey {
        type Signature = TestSignature;
        fn sign(
            &self,
            _signature_algorithm: SignatureAlgorithm,
            _data: &[u8],
        ) -> Result<Self::Signature, CoreError> {
            Ok(TestSignature {
                val: format!("{} {}", "testsignature".to_string(), self.key.clone()),
            })
        }
    }

    struct TestSignature {
        val: String,
    }
    impl Signature for TestSignature {
        fn as_bytes(&self) -> &[u8] {
            self.val.as_bytes()
        }
    }

    #[derive(Clone)]
    struct TestKeyStore {
        activated_key: Option<TestKey>,
    }

    impl Activate for TestKeyStore {
        type Key = TestKey;

        fn activate_identity_key(&mut self, key: Self::Key) -> Result<(), Error> {
            self.activated_key = Some(key);
            Ok(())
        }
    }

    impl KeyStore for TestKeyStore {
        type Key = TestKey;

        fn get(&self, _identity: &str, _key_name: &str) -> Result<Self::Key, CoreError> {
            Ok(self.activated_key.clone().unwrap())
        }
    }

    #[derive(Clone)]
    struct TestKeyGetter {}

    impl NewKeyGetter for TestKeyGetter {
        type Key = TestKey;

        fn get_key<B: AsRef<[u8]>>(&self, key_bytes: B) -> Result<Self::Key, Error> {
            Ok(TestKey {
                key: str::from_utf8(key_bytes.as_ref()).unwrap().to_string(),
            })
        }
    }

    #[test]
    fn server_register_with_auth_success() {
        let mut core = Core::new().unwrap();
        let expected_uri = "https://global.azure-devices-provisioning.net/scope/registrations/reg/register?api-version=2017-11-15";
        let handler = move |req: Request| {
            let (method, uri, _httpversion, headers, _body) = req.deconstruct();
            assert_eq!(uri, expected_uri);
            assert_eq!(method, Method::Put);
            // If authorization header does not have the shared access signature, request one
            let auth = headers.get::<Authorization<String>>();
            match auth {
                None => {
                    let mut result = TpmRegistrationResult::new();
                    result.set_authentication_key("key".to_string());
                    future::ok(
                        Response::new()
                            .with_status(StatusCode::Unauthorized)
                            .with_body(serde_json::to_string(&result).unwrap().into_bytes()),
                    )
                }
                Some(_) => {
                    let mut result = RegistrationOperationStatus::new("something".to_string())
                        .with_status("assigning".to_string());
                    future::ok(
                        Response::new()
                            .with_status(StatusCode::Ok)
                            .with_body(serde_json::to_string(&result).unwrap().into_bytes()),
                    )
                }
            }
        };
        let client = Arc::new(RwLock::new(
            Client::new(
                service_fn(handler),
                None,
                "2017-11-15",
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            ).unwrap(),
        ));
        let task = DpsClient::register_with_auth(
            &client,
            "scope".to_string(),
            "reg".to_string(),
            &TestKeyStore {
                activated_key: None,
            },
            TestKeyGetter {},
        ).map(|result| match result {
            Some(op) => {
                assert_eq!(op.operation_id(), "something");
                assert_eq!(op.status().unwrap(), "assigning");
                ()
            }
            None => panic!("Unexpected"),
        });
        core.run(task).unwrap();
    }

    #[test]
    fn server_register_gets_404_fails() {
        let mut core = Core::new().unwrap();
        let handler = |_req: Request| future::ok(Response::new().with_status(StatusCode::NotFound));
        let client = Client::new(
            service_fn(handler),
            None,
            "2017-11-15",
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        ).unwrap();
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            TestKeyStore {
                activated_key: None,
            },
            TestKeyGetter {},
            5,
        ).unwrap();
        let task = dps.register().then(|result| {
            match result {
                Ok(_) => panic!("Excepted err got success"),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Http) {
                        panic!("Wrong error kind. Expected `Http` found {:?}", err);
                    }
                }
            }
            Ok(()) as Result<(), Error>
        });
        core.run(task).unwrap();
    }

    #[test]
    fn server_register_with_auth_gets_404_fails() {
        let mut core = Core::new().unwrap();
        let handler = |req: Request| {
            // If authorization header does not have the shared access signature, request one
            let auth = req.headers().get::<Authorization<String>>();
            match auth {
                None => {
                    let mut result = TpmRegistrationResult::new();
                    result.set_authentication_key("key".to_string());
                    future::ok(
                        Response::new()
                            .with_status(StatusCode::Unauthorized)
                            .with_body(serde_json::to_string(&result).unwrap().into_bytes()),
                    )
                }
                Some(_) => future::ok(Response::new().with_status(StatusCode::NotFound)),
            }
        };
        let client = Client::new(
            service_fn(handler),
            None,
            "2017-11-15",
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        ).unwrap();
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            TestKeyStore {
                activated_key: None,
            },
            TestKeyGetter {},
            5,
        ).unwrap();
        let task = dps.register().then(|result| {
            match result {
                Ok(_) => panic!("Excepted err got success"),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Http) {
                        panic!("Wrong error kind. Expected `Http` found {:?}", err);
                    }
                }
            }
            Ok(()) as Result<(), Error>
        });
        core.run(task).unwrap();
    }

    #[test]
    fn get_device_registration_result_success() {
        let mut core = Core::new().unwrap();
        let reg_op_status_vanilla = Response::new().with_status(StatusCode::Ok).with_body(
            serde_json::to_string(&RegistrationOperationStatus::new("operation".to_string()))
                .unwrap()
                .into_bytes(),
        );
        let reg_op_status_final = Response::new().with_status(StatusCode::Ok).with_body(
            serde_json::to_string(&RegistrationOperationStatus::new("operation".to_string())
                .with_registration_state(DeviceRegistrationResult::new(
                    "reg".to_string(),
                    "doesn't matter".to_string(),
                ))).unwrap()
                .into_bytes(),
        );
        let stream = RefCell::new(stream::iter_result(vec![
            Ok(reg_op_status_vanilla),
            Ok(reg_op_status_final),
            Err(Error::from(ErrorKind::Unexpected)),
        ]));
        let handler = move |_req: Request| {
            if let Async::Ready(opt) = stream.borrow_mut().poll().unwrap() {
                future::ok(opt.unwrap())
            } else {
                unimplemented!();
            }
        };
        let service = service_fn(handler);
        let client = Arc::new(RwLock::new(
            Client::new(
                service,
                None,
                "2017-11-15",
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            ).unwrap()
                .with_token_source(DpsTokenSource::new(
                    "scope_id".to_string(),
                    "reg".to_string(),
                    TestKey {
                        key: "key".to_string(),
                    },
                ))
                .clone(),
        ));
        let dps_operation =
            DpsClient::<_, _, TestKeyStore, TestKeyGetter>::get_device_registration_result(
                client,
                "scope_id".to_string(),
                "reg".to_string(),
                "operation".to_string(),
                5,
            );
        let task = dps_operation.map(|result| {
            match result {
                Some(r) => assert_eq!(*r.registration_id(), "reg".to_string()),
                None => panic!("Expected registration id"),
            }
            ()
        });
        core.run(task).unwrap();
    }

    #[test]
    fn get_device_registration_result_on_all_attempts_returns_none() {
        let mut core = Core::new().unwrap();
        let handler = |_req: Request| {
            future::ok(
                Response::new().with_status(StatusCode::Ok).with_body(
                    serde_json::to_string(&RegistrationOperationStatus::new(
                        "operation".to_string(),
                    )).unwrap()
                        .into_bytes(),
                ),
            )
        };
        let service = service_fn(handler);
        let client = Arc::new(RwLock::new(
            Client::new(
                service,
                None,
                "2017-11-15",
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            ).unwrap()
                .with_token_source(DpsTokenSource::new(
                    "scope_id".to_string(),
                    "reg".to_string(),
                    TestKey {
                        key: "key".to_string(),
                    },
                ))
                .clone(),
        ));
        let dps_operation =
            DpsClient::<_, _, TestKeyStore, TestKeyGetter>::get_device_registration_result(
                client,
                "scope_id".to_string(),
                "reg".to_string(),
                "operation".to_string(),
                5,
            );
        let task = dps_operation.map(|result| {
            match result {
                Some(_) => panic!("Shouldn't have passed because every attempt failed"),
                None => assert_eq!(true, true),
            }
            ()
        });

        core.run(task).unwrap();
    }

    #[test]
    fn get_operation_status_success() {
        let mut core = Core::new().unwrap();
        let expected_uri = "https://global.azure-devices-provisioning.net/scope_id/registrations/reg/operations/operation?api-version=2017-11-15";
        let handler = move |req: Request| {
            let (method, uri, _httpversion, _headers, _body) = req.deconstruct();
            assert_eq!(uri, expected_uri);
            assert_eq!(method, Method::Get);

            let operation_status: RegistrationOperationStatus =
                RegistrationOperationStatus::new("operation".to_string());
            let serializable = operation_status.with_registration_state(
                DeviceRegistrationResult::new("reg".to_string(), "doesn't matter".to_string()),
            );
            future::ok(
                Response::new()
                    .with_status(StatusCode::Ok)
                    .with_body(serde_json::to_string(&serializable).unwrap().into_bytes()),
            )
        };
        let client = Client::new(
            service_fn(handler),
            None,
            "2017-11-15",
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        ).unwrap();
        let dps_operation = DpsClient::<_, _, TestKeyStore, TestKeyGetter>::get_operation_status(
            &Arc::new(RwLock::new(client.clone())),
            "scope_id",
            "reg",
            "operation",
        );
        let task = dps_operation.map(|result| match result {
            Some(op) => {
                assert_eq!(*op.registration_id(), "reg".to_string());
                ()
            }
            None => panic!("Unexpected"),
        });
        core.run(task).unwrap();
    }

    #[test]
    fn get_operation_status_gets_404_fails() {
        let mut core = Core::new().unwrap();
        let handler = |_req: Request| future::ok(Response::new().with_status(StatusCode::NotFound));
        let client = Client::new(
            service_fn(handler),
            None,
            "2017-11-15",
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        ).unwrap();
        let dps_operation = DpsClient::<_, _, TestKeyStore, TestKeyGetter>::get_operation_status(
            &Arc::new(RwLock::new(client.clone())),
            "scope_id",
            "reg",
            "operation",
        );
        let task = dps_operation.then(|result| {
            match result {
                Ok(_) => panic!("Excepted err got success"),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Http) {
                        panic!("Wrong error kind. Expected `Http` found {:?}", err);
                    }
                }
            }
            Ok(()) as Result<(), Error>
        });
        core.run(task).unwrap();
    }

    #[test]
    fn get_device_info_success() {
        assert_eq!(
            get_device_info(&DeviceRegistrationResult::new(
                "reg".to_string(),
                "assigned".to_string()
            ).with_device_id("device".to_string())
                .with_assigned_hub("hub".to_string()))
                .unwrap(),
            ("device".to_string(), "hub".to_string())
        )
    }
}
