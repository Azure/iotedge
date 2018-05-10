// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
#[cfg(test)]
extern crate bytes;
extern crate chrono;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate percent_encoding;
extern crate serde;
#[macro_use]
extern crate serde_derive;
#[cfg(test)]
extern crate serde_json;
#[cfg(test)]
extern crate tokio_core;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;
extern crate edgelet_utils;
extern crate iothubservice;

mod error;

use std::convert::AsRef;
use std::rc::Rc;

use chrono::{DateTime, Utc};
use failure::ResultExt;
use futures::Future;
use futures::future;
use hyper::client::Service;
use hyper::{Error as HyperError, Request, Response};
use percent_encoding::{percent_encode, PATH_SEGMENT_ENCODE_SET};
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_core::crypto::{KeyStore, Sign, Signature, SignatureAlgorithm};
use edgelet_core::{Identity, IdentityManager, IdentitySpec};
use edgelet_http::client::TokenSource;
use iothubservice::{AuthMechanism, AuthType, DeviceClient, Module, SymmetricKey};

pub use error::{Error, ErrorKind};

const KEY_PRIMARY: &str = "primary";
const KEY_SECONDARY: &str = "secondary";

#[derive(Serialize)]
pub struct HubIdentity {
    hub_module: Module,
}

impl HubIdentity {
    pub fn new(hub_module: Module) -> HubIdentity {
        HubIdentity { hub_module }
    }

    pub fn hub_module(&self) -> &Module {
        &self.hub_module
    }
}

impl Identity for HubIdentity {
    fn module_id(&self) -> &str {
        self.hub_module
            .module_id()
            .map(|s| s.as_str())
            .unwrap_or("")
    }

    fn managed_by(&self) -> &str {
        self.hub_module
            .managed_by()
            .map(|s| s.as_str())
            .unwrap_or("")
    }

    fn generation_id(&self) -> &str {
        self.hub_module
            .generation_id()
            .map(|s| s.as_str())
            .unwrap_or("")
    }
}

struct State<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    key_store: K,
    client: DeviceClient<S, SasTokenSource<K::Key>>,
}

pub struct SasTokenSource<K>
where
    K: Sign + Clone,
{
    hub_id: String,
    device_id: String,
    key: K,
}

impl<K> SasTokenSource<K>
where
    K: Sign + Clone,
{
    pub fn new(hub_id: String, device_id: String, key: K) -> Self {
        SasTokenSource {
            hub_id,
            device_id,
            key,
        }
    }
}

impl<K> TokenSource for SasTokenSource<K>
where
    K: Sign + Clone,
{
    type Error = Error;

    fn get(&self, expiry: &DateTime<Utc>) -> Result<String, Error> {
        let expiry = expiry.timestamp().to_string();
        let audience = format!(
            "{}.azure-devices.net/devices/{}",
            self.hub_id, self.device_id
        );

        let resource_uri =
            percent_encode(audience.to_lowercase().as_bytes(), PATH_SEGMENT_ENCODE_SET).to_string();
        let sig_data = format!("{}\n{}", &resource_uri, expiry);

        let signature = self.key
            .sign(SignatureAlgorithm::HMACSHA256, sig_data.as_bytes())
            .map(|s| base64::encode(s.as_bytes()))
            //.context(ErrorKind::Token)
            .map_err(Error::from)?;

        let token = UrlSerializer::new(format!("sr={}", resource_uri))
            .append_pair("sig", &signature)
            .append_pair("se", &expiry)
            .finish();
        Ok(token)
    }
}

impl<K> Clone for SasTokenSource<K>
where
    K: Sign + Clone,
{
    fn clone(&self) -> Self {
        SasTokenSource {
            hub_id: self.hub_id.clone(),
            device_id: self.device_id.clone(),
            key: self.key.clone(),
        }
    }
}

pub struct HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    state: Rc<State<K, S>>,
}

impl<K, S> HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    pub fn new(
        key_store: K,
        client: DeviceClient<S, SasTokenSource<K::Key>>,
    ) -> HubIdentityManager<K, S> {
        HubIdentityManager {
            state: Rc::new(State { key_store, client }),
        }
    }

    fn get_key_pair(&self, id: &IdentitySpec) -> Result<(K::Key, K::Key), Error> {
        self.state
            .key_store
            .get(id.module_id(), KEY_PRIMARY)
            .and_then(|primary_key| {
                self.state
                    .key_store
                    .get(id.module_id(), KEY_SECONDARY)
                    .map(|secondary_key| (primary_key, secondary_key))
            })
            .context(ErrorKind::CannotGetKey(id.module_id().to_string()))
            .map_err(Error::from)
    }
}

impl<K, S> Clone for HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    fn clone(&self) -> Self {
        HubIdentityManager {
            state: self.state.clone(),
        }
    }
}

impl<K, S> IdentityManager for HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    type Identity = HubIdentity;
    type Error = Error;
    type CreateFuture = Box<Future<Item = Self::Identity, Error = Self::Error>>;
    type UpdateFuture = Box<Future<Item = Self::Identity, Error = Self::Error>>;
    type GetFuture = Box<Future<Item = Vec<Self::Identity>, Error = Self::Error>>;
    type DeleteFuture = Box<Future<Item = (), Error = Self::Error>>;

    fn create(&mut self, id: IdentitySpec) -> Self::CreateFuture {
        let result = self.get_key_pair(&id)
            .and_then(|(primary_key, secondary_key)| {
                let auth = AuthMechanism::default()
                    .with_type(AuthType::Sas)
                    .with_symmetric_key(
                        SymmetricKey::default()
                            .with_primary_key(base64::encode(primary_key.as_ref()))
                            .with_secondary_key(base64::encode(secondary_key.as_ref())),
                    );

                Ok(self.state
                    .client
                    .create_module(id.module_id(), Some(auth))
                    .map_err(Error::from)
                    .map(HubIdentity::new))
            });

        match result {
            Ok(f) => Box::new(f),
            Err(err) => Box::new(future::err(err)),
        }
    }

    fn update(&mut self, id: IdentitySpec) -> Self::UpdateFuture {
        let result = self.get_key_pair(&id)
            .and_then(|(primary_key, secondary_key)| {
                let auth = AuthMechanism::default()
                    .with_type(AuthType::Sas)
                    .with_symmetric_key(
                        SymmetricKey::default()
                            .with_primary_key(base64::encode(primary_key.as_ref()))
                            .with_secondary_key(base64::encode(secondary_key.as_ref())),
                    );

                Ok(self.state
                    .client
                    .update_module(id.module_id(), Some(auth))
                    .map_err(Error::from)
                    .map(HubIdentity::new))
            });

        match result {
            Ok(f) => Box::new(f),
            Err(err) => Box::new(future::err(err)),
        }
    }

    fn get(&self) -> Self::GetFuture {
        Box::new(
            self.state
                .client
                .list_modules()
                .map_err(Error::from)
                .map(|modules| modules.into_iter().map(HubIdentity::new).collect()),
        )
    }

    fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture {
        Box::new(
            self.state
                .client
                .delete_module(id.module_id())
                .map_err(Error::from),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use bytes::Bytes;
    use chrono::TimeZone;
    use futures::Stream;
    use hyper::header::ContentType;
    use hyper::server::service_fn;
    use hyper::{Method, Request, Response, StatusCode};
    use tokio_core::reactor::Core;
    use url::Url;

    use edgelet_core::crypto::{MemoryKey, MemoryKeyStore};
    use edgelet_http::client::Client;

    #[test]
    fn hub_identity_empty_prop() {
        let m1 = HubIdentity::new(Module::new());
        assert_eq!(m1.module_id(), "");
        assert_eq!(m1.managed_by(), "");
        assert_eq!(m1.generation_id(), "");
    }

    #[test]
    fn get_key_pair_succeeds() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert("m1", KEY_PRIMARY, MemoryKey::new("pkey"));
        key_store.insert("m1", KEY_SECONDARY, MemoryKey::new("skey"));

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request| Ok(Response::new().with_status(StatusCode::Ok));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let (pkey, skey) = identity_manager
            .get_key_pair(&IdentitySpec::new("m1"))
            .unwrap();

        assert_eq!(pkey.as_ref(), &Bytes::from("pkey"));
        assert_eq!(skey.as_ref(), &Bytes::from("skey"));
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_module() {
        let key_store = MemoryKeyStore::new();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request| Ok(Response::new().with_status(StatusCode::Ok));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager
            .get_key_pair(&IdentitySpec::new("m1"))
            .unwrap();
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_pkey() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert("m1", KEY_SECONDARY, MemoryKey::new("skey"));

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request| Ok(Response::new().with_status(StatusCode::Ok));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager
            .get_key_pair(&IdentitySpec::new("m1"))
            .unwrap();
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_skey() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert("m1", KEY_PRIMARY, MemoryKey::new("pkey"));

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request| Ok(Response::new().with_status(StatusCode::Ok));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager
            .get_key_pair(&IdentitySpec::new("m1"))
            .unwrap();
    }

    #[test]
    fn create_succeeds() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert("m1", KEY_PRIMARY, MemoryKey::new("pkey"));
        key_store.insert("m1", KEY_SECONDARY, MemoryKey::new("skey"));

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let expected_module = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_authentication(
                AuthMechanism::default()
                    .with_type(AuthType::Sas)
                    .with_symmetric_key(
                        SymmetricKey::default()
                            .with_primary_key(base64::encode(MemoryKey::new("pkey").as_ref()))
                            .with_secondary_key(base64::encode(MemoryKey::new("skey").as_ref())),
                    ),
            );
        let expected_module_result = expected_module
            .clone()
            .with_generation_id("g1".to_string())
            .with_managed_by("iotedge".to_string());

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Put);
            assert_eq!(req.path(), "/devices/d1/modules/m1");

            let expected_module_copy = expected_module.clone();
            req.body()
                .concat2()
                .and_then(|req_body| Ok(serde_json::from_slice::<Module>(&req_body).unwrap()))
                .and_then(move |module| {
                    assert_eq!(module, expected_module_copy);

                    Ok(Response::new()
                        .with_status(StatusCode::Ok)
                        .with_header(ContentType::json())
                        .with_body(
                            serde_json::to_string(&module
                                .with_generation_id("g1".to_string())
                                .with_managed_by("iotedge".to_string()))
                                .unwrap()
                                .into_bytes(),
                        ))
                })
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let mut identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.create(IdentitySpec::new("m1"));

        let hub_identity = Core::new().unwrap().run(task).unwrap();

        assert_eq!(hub_identity.hub_module(), &expected_module_result);
    }

    #[test]
    fn list_succeeds() {
        let m1pkey = "m1pkey";
        let m1skey = "m1skey";
        let m2pkey = "m2pkey";
        let m2skey = "m2skey";

        let mut key_store = MemoryKeyStore::new();
        key_store.insert("m1", KEY_PRIMARY, MemoryKey::new(m1pkey));
        key_store.insert("m1", KEY_SECONDARY, MemoryKey::new(m1skey));
        key_store.insert("m2", KEY_PRIMARY, MemoryKey::new(m2pkey));
        key_store.insert("m2", KEY_SECONDARY, MemoryKey::new(m2skey));

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let response_modules = vec![
            Module::default()
                .with_device_id("d1".to_string())
                .with_module_id("m1".to_string())
                .with_authentication(
                    AuthMechanism::default()
                        .with_type(AuthType::Sas)
                        .with_symmetric_key(
                            SymmetricKey::default()
                                .with_primary_key(base64::encode(MemoryKey::new(m1pkey).as_ref()))
                                .with_secondary_key(base64::encode(
                                    MemoryKey::new(m1skey).as_ref(),
                                )),
                        ),
                ),
            Module::default()
                .with_device_id("d1".to_string())
                .with_module_id("m2".to_string())
                .with_authentication(
                    AuthMechanism::default()
                        .with_type(AuthType::Sas)
                        .with_symmetric_key(
                            SymmetricKey::default()
                                .with_primary_key(base64::encode(MemoryKey::new(m2pkey).as_ref()))
                                .with_secondary_key(base64::encode(
                                    MemoryKey::new(m2skey).as_ref(),
                                )),
                        ),
                ),
        ];

        let expected_modules_result = response_modules
            .iter()
            .map(|module| {
                module
                    .clone()
                    .with_generation_id("g1".to_string())
                    .with_managed_by("iotedge".to_string())
            })
            .collect::<Vec<Module>>();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Get);
            assert_eq!(req.path(), "/devices/d1/modules");

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_header(ContentType::json())
                .with_body(
                    serde_json::to_string(&response_modules
                        .iter()
                        .map(|module| {
                            module
                                .clone()
                                .with_generation_id("g1".to_string())
                                .with_managed_by("iotedge".to_string())
                        })
                        .collect::<Vec<Module>>())
                        .unwrap()
                        .into_bytes(),
                ))
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.get();

        let hub_identities = Core::new().unwrap().run(task).unwrap();

        for hub_identity in hub_identities {
            assert_eq!(
                Some(hub_identity.hub_module()),
                expected_modules_result
                    .iter()
                    .find(|m| m == &hub_identity.hub_module())
            );
        }
    }

    #[test]
    fn delete_succeeds() {
        let key_store = MemoryKeyStore::new();

        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Delete);
            assert_eq!(req.path(), "/devices/d1/modules/m1");

            Ok(Response::new().with_status(StatusCode::Ok))
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(
            service_fn(handler),
            Some(token_source),
            api_version,
            host_name,
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let mut identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager
            .delete(IdentitySpec::new("m1"))
            .then(|result| Ok(assert_eq!(result.unwrap(), ())) as Result<(), Error>);

        Core::new().unwrap().run(task).unwrap();
    }

    #[test]
    fn token_source_success() {
        // arrange
        let hub_id = "Miyagley-Edge".to_string();
        let device_id = "miYagley1".to_string();
        let key = MemoryKey::new(base64::decode("key").unwrap());
        let token_source = SasTokenSource::new(hub_id, device_id, key);
        let expiry = Utc.ymd(2018, 4, 26).and_hms(20, 54, 15);

        // act
        let token = token_source.get(&expiry).unwrap();

        // assert
        let expected = concat!(
            "sr=miyagley-edge.azure-devices.net",
            "%2Fdevices%2Fmiyagley1&sig=ynXM1wWasX%2FGvvgnhV%2BLZ5",
            "rxWOiCWtyqHMG2Dcd9Pg8%3D&se=1524776055"
        );
        assert_eq!(expected, token);
    }
}
