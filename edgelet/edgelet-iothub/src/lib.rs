// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
#[cfg(test)]
extern crate bytes;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
#[cfg(test)]
extern crate serde_json;
#[cfg(test)]
extern crate tokio_core;
#[cfg(test)]
extern crate url;

extern crate edgelet_core;
extern crate edgelet_utils;
extern crate iothubservice;

mod error;

use std::convert::AsRef;

use failure::ResultExt;
use futures::Future;
use futures::future;
use hyper::{Error as HyperError, Request, Response};
use hyper::client::Service;

use edgelet_core::{Identity, IdentityManager, IdentitySpec};
use edgelet_core::crypto::KeyStore;
use iothubservice::{AuthMechanism, AuthType, DeviceClient, Module, SymmetricKey};

use error::{Error, ErrorKind, Result};

const KEY_PRIMARY: &str = "primary";
const KEY_SECONDARY: &str = "secondary";

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

pub struct HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]>,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    key_store: K,
    client: DeviceClient<S>,
}

impl<K, S> HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]>,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    pub fn new(key_store: K, client: DeviceClient<S>) -> HubIdentityManager<K, S> {
        HubIdentityManager { key_store, client }
    }

    fn get_key_pair(&self, id: &IdentitySpec) -> Result<(K::Key, K::Key)> {
        self.key_store
            .get(id.module_id(), KEY_PRIMARY)
            .and_then(|primary_key| {
                self.key_store
                    .get(id.module_id(), KEY_SECONDARY)
                    .map(|secondary_key| (primary_key, secondary_key))
            })
            .context(ErrorKind::CannotGetKey(id.module_id().to_string()))
            .map_err(Error::from)
    }
}

impl<K, S> IdentityManager for HubIdentityManager<K, S>
where
    K: KeyStore,
    K::Key: AsRef<[u8]>,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    type Identity = HubIdentity;
    type Error = Error;
    type CreateFuture = Box<Future<Item = Self::Identity, Error = Self::Error>>;
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

                Ok(self.client
                    .create_module(id.module_id(), Some(auth))
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
            self.client
                .list_modules()
                .map_err(Error::from)
                .map(|modules| modules.into_iter().map(HubIdentity::new).collect()),
        )
    }

    fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture {
        Box::new(
            self.client
                .delete_module(id.module_id())
                .map_err(Error::from),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use bytes::Bytes;
    use futures::Stream;
    use hyper::{Method, Request, Response, StatusCode};
    use hyper::header::ContentType;
    use hyper::server::service_fn;
    use tokio_core::reactor::Core;
    use url::Url;

    use edgelet_core::crypto::{MemoryKey, MemoryKeyStore};
    use iothubservice::Client;

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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
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
        let client = Client::new(service_fn(handler), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let mut identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager
            .delete(IdentitySpec::new("m1"))
            .then(|result| Ok(assert_eq!(result.unwrap(), ())) as Result<()>);

        Core::new().unwrap().run(task).unwrap();
    }
}
