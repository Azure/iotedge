// Copyright (c) Microsoft. All rights reserved.

use base64;
use chrono::{DateTime, Utc};
use futures::future::Either;
use futures::{future, Future};
use hyper::client::Service;
use hyper::{Error as HyperError, Method, Request, Response, StatusCode};
use percent_encoding::{percent_encode, PATH_SEGMENT_ENCODE_SET};
use serde_json;
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_core::crypto::{KeyStore, MemoryKey, MemoryKeyStore, Sign, Signature,
                           SignatureAlgorithm};
use edgelet_http::ErrorKind as HttpErrorKind;
use edgelet_http::client::{Client, TokenSource};
use error::{Error, ErrorKind};
use model::{DeviceRegistration, TpmRegistrationResult};

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

pub struct DpsClient<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    client: Client<S, DpsTokenSource<MemoryKey>>,
    scope_id: String,
    registration_id: String,
    key_store: MemoryKeyStore,
}

impl<S> DpsClient<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    pub fn new(
        client: Client<S, DpsTokenSource<MemoryKey>>,
        scope_id: String,
        registration_id: String,
        key_store: MemoryKeyStore,
    ) -> Result<DpsClient<S>, Error> {
        Ok(DpsClient {
            client,
            scope_id,
            registration_id,
            key_store,
        })
    }

    fn get_tpm_challenge_key(body: &str) -> Result<MemoryKey, Error> {
        serde_json::from_str(body).map_err(Error::from).and_then(
            |tpm_challenge: TpmRegistrationResult| {
                tpm_challenge
                    .authentication_key()
                    .ok_or_else(|| Error::from(ErrorKind::InvalidTpmToken))
                    .and_then(|key_str| Ok(MemoryKey::new(key_str)))
            },
        )
    }

    fn register_with_auth(
        client: Client<S, DpsTokenSource<MemoryKey>>,
        scope_id: &str,
        registration_id: &str,
        registration: &DeviceRegistration,
        key_store: &MemoryKeyStore,
    ) -> Box<Future<Item = Option<i32>, Error = Error>> {
        Box::new(
            key_store
                .get("dps", "auth")
                .map_err(Error::from)
                .map(|key| {
                    let token_source =
                        DpsTokenSource::new(scope_id.to_string(), registration_id.to_string(), key);

                    let f = client
                        .with_token_source(token_source)
                        .request::<DeviceRegistration, i32>(
                            Method::Put,
                            &format!("{}/registrations/{}/register", scope_id, registration_id),
                            None,
                            Some(registration.clone()),
                            false,
                        )
                        .map_err(Error::from);

                    Either::A(f)
                })
                .unwrap_or_else(|err| Either::B(future::err(err))),
        )
    }

    pub fn register(&self) -> Box<Future<Item = Option<i32>, Error = Error>> {
        let registration =
            DeviceRegistration::new().with_registration_id(self.registration_id.clone());
        let mut key_store = self.key_store.clone();
        let client_with_token = self.client.clone();
        let scope_id = self.scope_id.clone();
        let registration_id = self.registration_id.clone();
        let r = self.client
            .request::<DeviceRegistration, TpmRegistrationResult>(
                Method::Put,
                &format!(
                    "{}/registrations/{}/register",
                    self.scope_id, self.registration_id
                ),
                None,
                Some(registration.clone()),
                false,
            )
            .then(move |result| {
                match result {
                    Ok(_) => Either::B(future::err(Error::from(ErrorKind::EmptyResponse))),
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

                        body.map(|body| {
                            Self::get_tpm_challenge_key(body.as_str())
                                .map(|key| {
                                    key_store.insert("dps", "auth", key.clone());
                                    Either::A(Self::register_with_auth(
                                        client_with_token,
                                        scope_id.as_str(),
                                        registration_id.as_str(),
                                        &registration,
                                        &key_store,
                                    ))
                                })
                                .unwrap_or_else(|err| Either::B(future::err(err)))
                        }).unwrap_or_else(|| Either::B(future::err(Error::from(err))))
                    }
                }
            });
        Box::new(r)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use hyper::StatusCode;
    use hyper::header::Authorization;
    use hyper::server::service_fn;
    use serde_json;
    use tokio_core::reactor::Core;
    use url::Url;

    #[test]
    fn tpm_challenge_success() {
        let mut core = Core::new().unwrap();
        let keystore = MemoryKeyStore::new();
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
                Some(_) => {
                    let mut result = 20;
                    future::ok(
                        Response::new()
                            .with_status(StatusCode::Ok)
                            .with_body(serde_json::to_string(&result).unwrap().into_bytes()),
                    )
                }
            }
        };
        let client = Client::new(
            service_fn(handler),
            None,
            "2017-11-15",
            Url::parse("https://global.azure-devices-provisioning.net/").unwrap(),
        ).unwrap();
        let dps =
            DpsClient::new(client, "scope".to_string(), "test".to_string(), keystore).unwrap();
        let task = dps.register().then(|result| {
            match result {
                Ok(n) => {
                    assert_eq!(n, Some(20));
                }
                Err(_) => {
                    panic!("Unexpected");
                }
            };
            result
        });
        core.run(task).unwrap();
    }
}
