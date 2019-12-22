// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use base64;
use bytes::Bytes;
use chrono::{DateTime, Utc};
use failure::{Fail, ResultExt};
use futures::future::Either;
use futures::{future, Future};
use hyper::{Method, StatusCode};
use log::{debug, info};
use percent_encoding::{define_encode_set, percent_encode, PATH_SEGMENT_ENCODE_SET};
use serde_json;
use tokio::prelude::*;
use tokio::timer::Interval;
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_core::crypto::{Activate, KeyIdentity, KeyStore, Sign, Signature, SignatureAlgorithm};
use edgelet_http::client::{Client, ClientImpl, TokenSource};
use edgelet_http::ErrorKind as HttpErrorKind;

use crate::error::{Error, ErrorKind};
use crate::model::{
    DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus, TpmAttestation,
    TpmRegistrationResult,
};

/// This is the interval at which to poll DPS for registration assignment status
const DPS_ASSIGNMENT_RETRY_INTERVAL_SECS: u64 = 10;

/// This is the number of seconds to wait for DPS to complete assignment to a hub
const DPS_ASSIGNMENT_TIMEOUT_SECS: u64 = 120;

define_encode_set! {
    pub IOTHUB_ENCODE_SET = [PATH_SEGMENT_ENCODE_SET] | { '=' }
}

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
            percent_encode(audience.to_lowercase().as_bytes(), IOTHUB_ENCODE_SET).to_string();
        let sig_data = format!("{}\n{}", &resource_uri, expiry);

        let signature = self
            .key
            .sign(SignatureAlgorithm::HMACSHA256, sig_data.as_bytes())
            .map(|s| base64::encode(s.as_bytes()))
            .context(ErrorKind::GetToken)?;

        let token = UrlSerializer::new(format!("sr={}", resource_uri))
            .append_pair("sig", &signature)
            .append_pair("se", &expiry)
            .append_pair("skn", "registration")
            .finish();
        Ok(token)
    }
}

pub enum DpsAuthKind {
    Tpm { ek: Bytes, srk: Bytes },
    SymmetricKey,
    X509,
}

pub struct DpsClient<C, K, A>
where
    C: ClientImpl,
    K: 'static + Sign + Clone,
    A: 'static + KeyStore<Key = K> + Activate<Key = K> + Clone,
{
    client: Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
    scope_id: String,
    registration_id: String,
    auth: DpsAuthKind,
    key_store: A,
}

impl<C, K, A> DpsClient<C, K, A>
where
    C: 'static + ClientImpl,
    K: 'static + Sign + Clone + Send + Sync,
    A: 'static + KeyStore<Key = K> + Activate<Key = K> + Clone + Send,
{
    pub fn new(
        client: Client<C, DpsTokenSource<K>>,
        scope_id: String,
        registration_id: String,
        auth: DpsAuthKind,
        key_store: A,
    ) -> Result<Self, Error> {
        Ok(DpsClient {
            client: Arc::new(RwLock::new(client)),
            scope_id,
            registration_id,
            auth,
            key_store,
        })
    }

    fn get_tpm_challenge_key(body: &str, key_store: &mut A) -> Result<K, Error> {
        let tpm_challenge: TpmRegistrationResult =
            serde_json::from_str(body).context(ErrorKind::GetTpmChallengeKey)?;

        let key_str = tpm_challenge
            .authentication_key()
            .ok_or_else(|| ErrorKind::InvalidTpmToken)?;
        let key_bytes = base64::decode(key_str).context(ErrorKind::GetTpmChallengeKey)?;

        debug!("Storing authentication key");
        key_store
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), key_bytes)
            .context(ErrorKind::GetTpmChallengeKey)?;

        Ok(key_store
            .get(&KeyIdentity::Device, "primary")
            .context(ErrorKind::GetTpmChallengeKey)?)
    }

    fn get_symmetric_challenge_key(key_store: &A) -> Result<K, Error> {
        debug!("Obtaining symmetric authentication key");
        Ok(key_store
            .get(&KeyIdentity::Device, "primary")
            .context(ErrorKind::GetSymmetricChallengeKey)?)
    }

    fn get_operation_id(
        client: &Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: &str,
        registration_id: &str,
        registration: &DeviceRegistration,
        token_source: Option<DpsTokenSource<K>>,
    ) -> Box<dyn Future<Item = Option<RegistrationOperationStatus>, Error = Error> + Send> {
        debug!(
            "Registration PUT, scope_id, \"{}\", registration_id \"{}\"",
            scope_id, registration_id
        );
        let cli = match token_source {
            Some(ts) => client
                .write()
                .expect("RwLock write failure")
                .clone()
                .with_token_source(ts),
            None => client.write().expect("RwLock write failure").clone(),
        };
        let future = cli
            .request::<DeviceRegistration, RegistrationOperationStatus>(
                Method::PUT,
                &format!("{}/registrations/{}/register", scope_id, registration_id),
                None,
                Some(registration.clone()),
                false,
            )
            .map_err(|err| Error::from(err.context(ErrorKind::GetOperationId)));
        Box::new(future)
    }

    fn get_operation_status(
        client: &Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: &str,
        registration_id: &str,
        operation_id: &str,
        token_source: Option<DpsTokenSource<K>>,
    ) -> Box<dyn Future<Item = Option<DeviceRegistrationResult>, Error = Error> + Send> {
        let c = if let Some(ts) = token_source {
            client
                .read()
                .expect("RwLock read failure")
                .clone()
                .with_token_source(ts)
        } else {
            client.read().expect("RwLock read failure").clone()
        };
        let request = c.request::<(), RegistrationOperationStatus>(
                Method::GET,
                &format!(
                    "{}/registrations/{}/operations/{}",
                    scope_id, registration_id, operation_id
                ),
                None,
                None,
                false,
            ).map_err(|err| Error::from(err.context(ErrorKind::GetOperationStatus)))
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

    // Return Ok(true) if we get no result, or the result is not complete.
    // The result is complete if we receive a status of anything other than "assigning"
    fn is_skippable_result(
        registration_result: &Option<DeviceRegistrationResult>,
    ) -> Result<bool, Error> {
        if let Some(r) = registration_result.as_ref() {
            debug!(
                "Device Registration Result: device {:?}, hub {:?}, status {:?}",
                r.device_id(),
                r.assigned_hub(),
                r.status()
            );
            Ok(r.status()
                .map_or_else(|| false, |status| status.eq_ignore_ascii_case("assigning")))
        } else {
            debug!("Not a device registration response");
            Ok(true)
        }
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
        client: Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: String,
        registration_id: String,
        operation_id: String,
        token_source: Option<DpsTokenSource<K>>,
        retry_count: u64,
    ) -> Box<dyn Future<Item = Option<DeviceRegistrationResult>, Error = Error> + Send> {
        debug!(
            "DPS registration result will retry {} times every {} seconds",
            retry_count, DPS_ASSIGNMENT_RETRY_INTERVAL_SECS
        );
        let chain = Interval::new(
            Instant::now(),
            Duration::from_secs(DPS_ASSIGNMENT_RETRY_INTERVAL_SECS),
        )
        .take(retry_count)
        .map_err(|err| Error::from(err.context(ErrorKind::GetDeviceRegistrationResult)))
        .and_then(move |_instant: Instant| {
            debug!("Ask DPS for registration status");
            Self::get_operation_status(
                &client.clone(),
                &scope_id,
                &registration_id,
                &operation_id,
                token_source.clone(),
            )
        })
        .skip_while(Self::is_skippable_result)
        .take(1)
        .fold(
            None,
            |_final_result: Option<DeviceRegistrationResult>,
             result_from_service: Option<DeviceRegistrationResult>| {
                debug!("{:?}", result_from_service);
                future::ok::<Option<DeviceRegistrationResult>, Error>(result_from_service)
            },
        );
        Box::new(chain)
    }

    fn register_with_x509_auth(
        client: &Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: &str,
        registration_id: String,
        _key_store: &A,
    ) -> Box<dyn Future<Item = Option<RegistrationOperationStatus>, Error = Error> + Send> {
        let cli = client.clone();
        let uri_path = format!("{}/registrations/{}/register", scope_id, registration_id);
        let registration = DeviceRegistration::new().with_registration_id(registration_id);
        let cli = cli.read().expect("RwLock read failure").clone();
        let f = cli
            .request::<DeviceRegistration, RegistrationOperationStatus>(
                Method::PUT,
                &uri_path,
                None,
                Some(registration),
                false,
            )
            .map_err(|err| Error::from(err.context(ErrorKind::RegisterWithX509IdentityCertificate)))
            .map(
                move |operation_status: Option<RegistrationOperationStatus>| {
                    debug!("{:?}", operation_status);
                    operation_status
                },
            )
            .into_future();
        Box::new(f)
    }

    fn register_with_symmetric_key_auth(
        client: &Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: String,
        registration_id: String,
        key_store: &A,
    ) -> Box<dyn Future<Item = Option<RegistrationOperationStatus>, Error = Error> + Send> {
        let cli = client.clone();
        let registration = DeviceRegistration::new().with_registration_id(registration_id.clone());
        let f = Self::get_symmetric_challenge_key(key_store)
            .map_err(|err| Error::from(err.context(ErrorKind::GetOperationStatusForSymmetricKey)))
            .into_future()
            .and_then(move |symmetric_key| {
                let token_source = DpsTokenSource::new(
                    scope_id.to_string(),
                    registration_id.to_string(),
                    symmetric_key,
                );
                let cli = cli.read().expect("RwLock read failure").clone();
                cli.with_token_source(token_source)
                    .request::<DeviceRegistration, RegistrationOperationStatus>(
                        Method::PUT,
                        &format!("{}/registrations/{}/register", scope_id, registration_id),
                        None,
                        Some(registration.clone()),
                        false,
                    )
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::RegisterWithSymmetricChallengeKey))
                    })
                    .map(
                        move |operation_status: Option<RegistrationOperationStatus>| {
                            debug!("{:?}", operation_status);
                            operation_status
                        },
                    )
            });
        Box::new(f)
    }

    fn register_with_tpm_auth(
        client: &Arc<RwLock<Client<C, DpsTokenSource<K>>>>,
        scope_id: String,
        registration_id: String,
        tpm_ek: &Bytes,
        tpm_srk: &Bytes,
        key_store: &A,
    ) -> Box<dyn Future<Item = Option<RegistrationOperationStatus>, Error = Error> + Send> {
        let tpm_attestation = TpmAttestation::new(base64::encode(&tpm_ek))
            .with_storage_root_key(base64::encode(&tpm_srk));
        let registration = DeviceRegistration::new()
            .with_registration_id(registration_id.clone())
            .with_tpm(tpm_attestation);
        let client_inner = client.clone();
        let mut key_store_inner = key_store.clone();
        let r = client
            .read()
            .expect("RwLock read failure")
            .request::<DeviceRegistration, TpmRegistrationResult>(
                Method::PUT,
                &format!("{}/registrations/{}/register", scope_id, registration_id),
                None,
                Some(registration.clone()),
                false,
            )
            .then(move |result| {
                match result {
                    Ok(_) => Either::B(future::err(Error::from(
                        ErrorKind::RegisterWithAuthUnexpectedlySucceeded,
                    ))),
                    Err(err) => {
                        // If request is returned with status unauthorized, extract the tpm
                        // challenge from the payload, generate a signature and re-issue the
                        // request
                        let body = if let HttpErrorKind::HttpWithErrorResponse(status, body) =
                            err.kind()
                        {
                            if *status == StatusCode::UNAUTHORIZED {
                                debug!(
                                    "Registration unauthorized, checking response for challenge {}",
                                    status,
                                );
                                Some(body.clone())
                            } else {
                                debug!("Unexpected registration status, {}", status);
                                None
                            }
                        } else {
                            debug!("Response error {:?}", err);
                            None
                        };

                        body.map_or_else(
                            || {
                                Either::B(future::err(Error::from(
                                    err.context(ErrorKind::RegisterWithAuthUnexpectedlyFailed),
                                )))
                            },
                            move |body| match Self::get_tpm_challenge_key(
                                body.as_str(),
                                &mut key_store_inner,
                            ) {
                                Ok(key) => {
                                    let token_source = DpsTokenSource::new(
                                        scope_id.to_string(),
                                        registration_id.to_string(),
                                        key,
                                    );
                                    Either::A(Self::get_operation_id(
                                        &client_inner.clone(),
                                        scope_id.as_str(),
                                        registration_id.as_str(),
                                        &registration,
                                        Some(token_source),
                                    ))
                                }
                                Err(err) => Either::B(future::err(err)),
                            },
                        )
                    }
                }
            });
        Box::new(r)
    }

    pub fn register(
        &self,
    ) -> Box<dyn Future<Item = (String, String, Option<String>), Error = Error> + Send> {
        let key_store = self.key_store.clone();
        let mut key_store_status = self.key_store.clone();
        let client_with_token_status = self.client.clone();
        let scope_id = self.scope_id.clone();
        let scope_id_status = self.scope_id.clone();
        let registration_id = self.registration_id.clone();
        let registration_id_status = self.registration_id.clone();
        info!(
            "Starting DPS registration with scope_id \"{}\", registration_id \"{}\"",
            scope_id, registration_id,
        );

        let mut use_tpm_auth = false;
        let mut use_x509_auth = false;
        let r = match &self.auth {
            DpsAuthKind::Tpm { ek, srk } => {
                use_tpm_auth = true;
                Self::register_with_tpm_auth(
                    &self.client,
                    scope_id.clone(),
                    registration_id.clone(),
                    &ek,
                    &srk,
                    &self.key_store,
                )
            }
            DpsAuthKind::SymmetricKey => Self::register_with_symmetric_key_auth(
                &self.client,
                scope_id.clone(),
                registration_id.clone(),
                &self.key_store,
            ),
            DpsAuthKind::X509 => {
                use_x509_auth = true;
                Self::register_with_x509_auth(
                    &self.client,
                    &scope_id,
                    registration_id.clone(),
                    &self.key_store,
                )
            }
        }
        .and_then(
            move |operation_status: Option<RegistrationOperationStatus>| {
                operation_status.map_or_else(
                    || {
                        Either::B(future::err(Error::from(
                            ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned,
                        )))
                    },
                    move |s| {
                        let retry_count =
                            (DPS_ASSIGNMENT_TIMEOUT_SECS / DPS_ASSIGNMENT_RETRY_INTERVAL_SECS) + 1;
                        let token_key: Result<Option<K>, ()> = if use_x509_auth {
                            Ok(None)
                        } else {
                            match key_store.get(&KeyIdentity::Device, "primary") {
                                Ok(id_key) => Ok(Some(id_key)),
                                Err(_err) => Err(()),
                            }
                        };
                        match token_key {
                            Ok(tk) => {
                                let ts = if let Some(k) = tk {
                                    Some(DpsTokenSource::new(
                                        scope_id.to_string(),
                                        registration_id.clone(),
                                        k,
                                    ))
                                } else {
                                    None
                                };
                                Either::A(Self::get_device_registration_result(
                                    client_with_token_status,
                                    scope_id_status,
                                    registration_id_status,
                                    s.operation_id().clone(),
                                    ts,
                                    retry_count,
                                ))
                            }
                            Err(_err) => Either::B(future::err(Error::from(
                                ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned,
                            ))),
                        }
                    },
                )
            },
        )
        .and_then(move |operation_status: Option<DeviceRegistrationResult>| {
            let s = operation_status.ok_or_else(|| {
                Error::from(ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned)
            })?;
            if use_tpm_auth {
                let tpm_result = s.tpm();
                let r = tpm_result.ok_or_else(|| {
                    Error::from(ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned)
                })?;
                let ks = r.authentication_key().ok_or_else(|| {
                    Error::from(ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned)
                })?;
                let kb =
                    base64::decode(ks).context(ErrorKind::RegisterWithAuthUnexpectedlyFailed)?;
                key_store_status
                    .activate_identity_key(KeyIdentity::Device, "primary".to_string(), kb)
                    .context(ErrorKind::RegisterWithAuthUnexpectedlyFailed)?;
            }
            get_device_info(&s)
        });
        Box::new(r)
    }
}

fn get_device_info(
    registration_result: &DeviceRegistrationResult,
) -> Result<(String, String, Option<String>), Error> {
    Ok((
        registration_result
            .device_id()
            .ok_or_else(|| {
                Error::from(ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned)
            })?
            .to_string(),
        registration_result
            .assigned_hub()
            .ok_or_else(|| {
                Error::from(ErrorKind::RegisterWithAuthUnexpectedlyFailedOperationNotAssigned)
            })?
            .to_string(),
        registration_result.substatus().map(ToString::to_string),
    ))
}

#[cfg(test)]
mod tests {
    use std::sync::Mutex;

    use edgelet_core::crypto::{MemoryKey, MemoryKeyStore};
    use http;
    use hyper::{self, Body, Request, Response, StatusCode};
    use serde_json;
    use tokio;
    use url::Url;

    use super::*;
    use crate::DPS_API_VERSION;

    #[test]
    fn server_register_with_tpm_auth_success() {
        let expected_uri = format!("https://global.azure-devices-provisioning.net/scope/registrations/reg/register?api-version={}", DPS_API_VERSION);
        let handler = move |req: Request<Body>| {
            let (
                http::request::Parts {
                    method,
                    uri,
                    headers,
                    ..
                },
                _body,
            ) = req.into_parts();
            assert_eq!(uri, expected_uri.as_str());
            assert_eq!(method, Method::PUT);
            // If authorization header does not have the shared access signature, request one
            let auth = headers.get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    let mut result = TpmRegistrationResult::new();
                    result.set_authentication_key(base64::encode("key"));
                    let response = Response::builder()
                        .status(StatusCode::UNAUTHORIZED)
                        .body(serde_json::to_string(&result).unwrap().into())
                        .expect("could not build hyper::Response");
                    future::ok(response)
                }
                Some(_) => {
                    let result = RegistrationOperationStatus::new("something".to_string())
                        .with_status("assigning".to_string());
                    future::ok(Response::new(
                        serde_json::to_string(&result).unwrap().into(),
                    ))
                }
            }
        };
        let client = Arc::new(RwLock::new(
            Client::new(
                handler,
                None,
                DPS_API_VERSION.to_string(),
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            )
            .unwrap(),
        ));

        let task = DpsClient::register_with_tpm_auth(
            &client,
            "scope".to_string(),
            "reg".to_string(),
            &Bytes::from("ek".to_string().into_bytes()),
            &Bytes::from("srk".to_string().into_bytes()),
            &MemoryKeyStore::new(),
        )
        .map(|result| match result {
            Some(op) => {
                assert_eq!(op.operation_id(), "something");
                assert_eq!(op.status().unwrap(), "assigning");
            }
            None => panic!("Unexpected"),
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_with_sym_key_auth_success() {
        let expected_uri = "https://global.azure-devices-provisioning.net/scope/registrations/reg/register?api-version=2018-11-01";
        let handler = move |req: Request<Body>| {
            let (
                http::request::Parts {
                    method,
                    uri,
                    headers,
                    ..
                },
                _body,
            ) = req.into_parts();
            assert_eq!(uri, expected_uri);
            assert_eq!(method, Method::PUT);
            // If authorization header does not have the shared access signature, request one
            let auth = headers.get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    panic!("Expected header");
                }
                Some(_) => {
                    let result = RegistrationOperationStatus::new("something".to_string())
                        .with_status("assigning".to_string());
                    future::ok(Response::new(
                        serde_json::to_string(&result).unwrap().into(),
                    ))
                }
            }
        };
        let client = Arc::new(RwLock::new(
            Client::new(
                handler,
                None,
                DPS_API_VERSION.to_string(),
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            )
            .unwrap(),
        ));

        let mut key_store = MemoryKeyStore::new();
        key_store
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), "some key")
            .unwrap();

        let task = DpsClient::register_with_symmetric_key_auth(
            &client,
            "scope".to_string(),
            "reg".to_string(),
            &key_store,
        )
        .map(|result| match result {
            Some(op) => {
                assert_eq!(op.operation_id(), "something");
                assert_eq!(op.status().unwrap(), "assigning");
            }
            None => panic!("Unexpected"),
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_with_x509_auth_success() {
        let expected_uri = "https://global.azure-devices-provisioning.net/scope/registrations/reg/register?api-version=2018-11-01";
        let handler = move |req: Request<Body>| {
            let (
                http::request::Parts {
                    method,
                    uri,
                    headers,
                    ..
                },
                _body,
            ) = req.into_parts();
            assert_eq!(uri, expected_uri);
            assert_eq!(method, Method::PUT);
            // If authorization header does not have the shared access signature, request one
            let auth = headers.get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    let result = RegistrationOperationStatus::new("something".to_string())
                        .with_status("assigning".to_string());
                    future::ok(Response::new(
                        serde_json::to_string(&result).unwrap().into(),
                    ))
                }
                Some(_) => {
                    panic!("Did not expect authorization header");
                }
            }
        };
        let client = Arc::new(RwLock::new(
            Client::new(
                handler,
                None,
                DPS_API_VERSION.to_string(),
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            )
            .unwrap(),
        ));

        let empty_key_store = MemoryKeyStore::new();
        let task = DpsClient::register_with_x509_auth(
            &client,
            "scope",
            "reg".to_string(),
            &empty_key_store,
        )
        .map(|result| match result {
            Some(op) => {
                assert_eq!(op.operation_id(), "something");
                assert_eq!(op.status().unwrap(), "assigning");
            }
            None => panic!("Unexpected"),
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_tpm_auth_gets_404_fails() {
        let handler = |_req: Request<Body>| {
            let response = Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .expect("could not build hyper::Response");
            future::ok(response)
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();

        let ek = Bytes::from("ek".to_string().into_bytes());
        let srk = Bytes::from("srk".to_string().into_bytes());
        let auth = DpsAuthKind::Tpm { ek, srk };
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            MemoryKeyStore::new(),
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::RegisterWithAuthUnexpectedlyFailed => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `RegisterWithAuthUnexpectedlyFailed` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_sym_key_auth_gets_404_fails() {
        let handler = |_req: Request<Body>| {
            let response = Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .expect("could not build hyper::Response");
            future::ok(response)
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();

        let mut key_store = MemoryKeyStore::new();
        key_store
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), "some key")
            .unwrap();
        let auth = DpsAuthKind::SymmetricKey;
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            key_store,
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::RegisterWithSymmetricChallengeKey => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `RegisterWithSymmetricChallengeKey` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_x509_key_auth_gets_404_fails() {
        let handler = |_req: Request<Body>| {
            let response = Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .expect("could not build hyper::Response");
            future::ok(response)
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();

        let empty_key_store = MemoryKeyStore::new();
        let auth = DpsAuthKind::X509;
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            empty_key_store,
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::RegisterWithX509IdentityCertificate => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `RegisterWithX509IdentityCertificate` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_with_tpm_auth_gets_404_fails() {
        let handler = |req: Request<Body>| {
            // If authorization header does not have the shared access signature, request one
            let auth = req.headers().get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    let mut result = TpmRegistrationResult::new();
                    result.set_authentication_key("key".to_string());
                    let response = Response::builder()
                        .status(StatusCode::UNAUTHORIZED)
                        .body(serde_json::to_string(&result).unwrap().into())
                        .expect("could not build hyper::Response");
                    future::ok(response)
                }
                Some(_) => {
                    let response = Response::builder()
                        .status(StatusCode::NOT_FOUND)
                        .body(Body::empty())
                        .expect("could not build hyper::Response");
                    future::ok(response)
                }
            }
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();

        let ek = Bytes::from("ek".to_string().into_bytes());
        let srk = Bytes::from("srk".to_string().into_bytes());
        let auth = DpsAuthKind::Tpm { ek, srk };
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            MemoryKeyStore::new(),
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::GetOperationId => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `GetOperationId` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_with_sym_key_auth_gets_401_fails() {
        let handler = |req: Request<Body>| {
            // If authorization header does not have the shared access signature, request one
            let auth = req.headers().get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    panic!("Expected a SAS token in the auth header");
                }
                Some(_) => {
                    let response = Response::builder()
                        .status(StatusCode::UNAUTHORIZED)
                        .body(Body::empty())
                        .expect("could not build hyper::Response");
                    future::ok(response)
                }
            }
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();

        let mut key_store = MemoryKeyStore::new();
        key_store
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), "some key")
            .unwrap();
        let auth = DpsAuthKind::SymmetricKey;
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            key_store,
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::RegisterWithSymmetricChallengeKey => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `RegisterWithSymmetricChallengeKey` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn server_register_x509_key_auth_gets_401_fails() {
        let handler = |req: Request<Body>| {
            let auth = req.headers().get(hyper::header::AUTHORIZATION);
            match auth {
                None => {
                    let response = Response::builder()
                        .status(StatusCode::UNAUTHORIZED)
                        .body(Body::empty())
                        .expect("could not build hyper::Response");
                    future::ok(response)
                }
                Some(_) => {
                    panic!("Did not expect a SAS token in the auth header");
                }
            }
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();
        let empty_key_store = MemoryKeyStore::new();
        let auth = DpsAuthKind::X509;
        let dps = DpsClient::new(
            client,
            "scope".to_string(),
            "test".to_string(),
            auth,
            empty_key_store,
        )
        .unwrap();
        let task = dps.register().then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::RegisterWithX509IdentityCertificate => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `RegisterWithX509IdentityCertificate` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_device_registration_result_success() {
        let reg_op_status_vanilla = Response::new(
            serde_json::to_string(&RegistrationOperationStatus::new("operation".to_string()))
                .unwrap()
                .into(),
        );

        let reg_op_status_final = Response::new(
            serde_json::to_string(
                &RegistrationOperationStatus::new("operation".to_string()).with_registration_state(
                    DeviceRegistrationResult::new()
                        .with_registration_id("reg".to_string())
                        .with_status("doesn't matter".to_string()),
                ),
            )
            .unwrap()
            .into(),
        );

        let stream = Mutex::new(stream::iter_result(vec![
            Ok(reg_op_status_vanilla),
            Ok(reg_op_status_final),
            Err(Error::from(
                ErrorKind::RegisterWithAuthUnexpectedlySucceeded,
            )),
        ]));
        let handler = move |_req: Request<Body>| {
            if let Async::Ready(opt) = stream.lock().unwrap().poll().unwrap() {
                future::ok(opt.unwrap())
            } else {
                unimplemented!();
            }
        };
        let key = MemoryKey::new("key".to_string());
        let client = Arc::new(RwLock::new(
            Client::new(
                handler,
                None,
                DPS_API_VERSION.to_string(),
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            )
            .unwrap()
            .with_token_source(DpsTokenSource::new(
                "scope_id".to_string(),
                "reg".to_string(),
                key.clone(),
            )),
        ));
        let token_source = DpsTokenSource::new("scope_id".to_string(), "reg".to_string(), key);
        let dps_operation = DpsClient::<_, _, MemoryKeyStore>::get_device_registration_result(
            client,
            "scope_id".to_string(),
            "reg".to_string(),
            "operation".to_string(),
            Some(token_source),
            3,
        );
        let task = dps_operation.map(|result| match result {
            Some(r) => assert_eq!(*r.registration_id().unwrap(), "reg".to_string()),
            None => panic!("Expected registration id"),
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_device_registration_result_on_all_attempts_returns_none() {
        let handler = |_req: Request<Body>| {
            future::ok(Response::new(
                serde_json::to_string(&RegistrationOperationStatus::new("operation".to_string()))
                    .unwrap()
                    .into(),
            ))
        };
        let key = MemoryKey::new("key".to_string());
        let client = Arc::new(RwLock::new(
            Client::new(
                handler,
                None,
                DPS_API_VERSION.to_string(),
                Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
            )
            .unwrap()
            .with_token_source(DpsTokenSource::new(
                "scope_id".to_string(),
                "reg".to_string(),
                key.clone(),
            )),
        ));
        let token_source = DpsTokenSource::new("scope_id".to_string(), "reg".to_string(), key);
        let dps_operation = DpsClient::<_, _, MemoryKeyStore>::get_device_registration_result(
            client,
            "scope_id".to_string(),
            "reg".to_string(),
            "operation".to_string(),
            Some(token_source),
            3,
        );
        let task = dps_operation.map(|result| match result {
            Some(_) => panic!("Shouldn't have passed because every attempt failed"),
            None => assert_eq!(true, true),
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_operation_status_success() {
        let expected_uri = "https://global.azure-devices-provisioning.net/scope_id/registrations/reg/operations/operation?api-version=2018-11-01";
        let handler = move |req: Request<Body>| {
            let (http::request::Parts { method, uri, .. }, _body) = req.into_parts();
            assert_eq!(uri, expected_uri);
            assert_eq!(method, Method::GET);

            let operation_status: RegistrationOperationStatus =
                RegistrationOperationStatus::new("operation".to_string());
            let serializable = operation_status.with_registration_state(
                DeviceRegistrationResult::new()
                    .with_registration_id("reg".to_string())
                    .with_status("doesn't matter".to_string()),
            );
            future::ok(Response::new(
                serde_json::to_string(&serializable).unwrap().into(),
            ))
        };
        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();
        let key = MemoryKey::new("key".to_string());
        let token_source = DpsTokenSource::new("scope_id".to_string(), "reg".to_string(), key);
        let dps_operation = DpsClient::<_, _, MemoryKeyStore>::get_operation_status(
            &Arc::new(RwLock::new(client.clone())),
            "scope_id",
            "reg",
            "operation",
            Some(token_source),
        );
        let task = dps_operation.map(|result| match result {
            Some(op) => {
                assert_eq!(*op.registration_id().unwrap(), "reg".to_string());
            }
            None => panic!("Unexpected"),
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_operation_status_gets_404_fails() {
        let handler = |_req: Request<Body>| {
            let response = Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .expect("could not build hyper::Response");
            future::ok(response)
        };

        let client = Client::new(
            handler,
            None,
            DPS_API_VERSION.to_string(),
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        )
        .unwrap();
        let key = MemoryKey::new("key".to_string());
        let token_source = DpsTokenSource::new("scope_id".to_string(), "reg".to_string(), key);
        let dps_operation = DpsClient::<_, _, MemoryKeyStore>::get_operation_status(
            &Arc::new(RwLock::new(client)),
            "scope_id",
            "reg",
            "operation",
            Some(token_source),
        );
        let task = dps_operation.then(|result| match result {
            Ok(_) => panic!("Excepted err got success"),
            Err(err) => match err.kind() {
                ErrorKind::GetOperationStatus => Ok::<_, Error>(()),
                _ => panic!(
                    "Wrong error kind. Expected `GetOperationStatus` found {:?}",
                    err
                ),
            },
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_device_info_success() {
        assert_eq!(
            get_device_info(
                &DeviceRegistrationResult::new()
                    .with_registration_id("reg".to_string())
                    .with_status("assigned".to_string())
                    .with_device_id("device".to_string())
                    .with_assigned_hub("hub".to_string())
                    .with_substatus("initialAssignment".to_string())
            )
            .unwrap(),
            (
                "device".to_string(),
                "hub".to_string(),
                Some("initialAssignment".to_string())
            )
        )
    }
}
