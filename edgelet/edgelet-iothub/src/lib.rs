// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

extern crate base64;
#[cfg(test)]
extern crate bytes;
extern crate chrono;
extern crate failure;
extern crate futures;
#[cfg(test)]
extern crate hyper;
#[macro_use]
extern crate percent_encoding;
#[macro_use]
extern crate serde_derive;
#[cfg(test)]
extern crate serde_json;
#[cfg(test)]
extern crate tokio;
#[cfg(test)]
extern crate typed_headers;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;
extern crate iothubservice;

mod error;

use std::convert::AsRef;
use std::marker::PhantomData;
use std::sync::Arc;

use chrono::{DateTime, Utc};
use failure::{Fail, ResultExt};
use futures::future::{self, Either};
use futures::Future;
use percent_encoding::{percent_encode, PATH_SEGMENT_ENCODE_SET};
use url::form_urlencoded::Serializer as UrlSerializer;

use edgelet_core::crypto::{KeyIdentity, KeyStore, Sign, Signature, SignatureAlgorithm};
use edgelet_core::{AuthType, Identity, IdentityManager, IdentityOperation, IdentitySpec};
use edgelet_http::client::{ClientImpl, TokenSource};
use iothubservice::{
    AuthMechanism, AuthType as HubAuthType, DeviceClient, ErrorKind as HubErrorKind, Module,
    ModuleOperationReason as HubReason, SymmetricKey,
};

pub use error::{Error, ErrorKind, IdentityOperationReason};

const KEY_PRIMARY: &str = "primary";
const KEY_SECONDARY: &str = "secondary";

define_encode_set! {
    pub IOTHUB_ENCODE_SET = [PATH_SEGMENT_ENCODE_SET] | { '=' }
}

#[derive(Debug, PartialEq, Serialize)]
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
        self.hub_module.module_id().unwrap_or("")
    }

    fn managed_by(&self) -> &str {
        self.hub_module.managed_by().unwrap_or("")
    }

    fn generation_id(&self) -> &str {
        self.hub_module.generation_id().unwrap_or("")
    }

    fn auth_type(&self) -> AuthType {
        self.hub_module
            .authentication()
            .and_then(|auth_mechanism| auth_mechanism.type_())
            .map_or(AuthType::None, convert_auth_type)
    }
}

fn convert_auth_type(hub_auth_type: HubAuthType) -> AuthType {
    match hub_auth_type {
        HubAuthType::None => AuthType::None,
        HubAuthType::Sas => AuthType::Sas,
        HubAuthType::X509 => AuthType::X509,
    }
}

struct State<K, C, D>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    C: ClientImpl,
    D: 'static + Sign + Clone,
{
    key_store: K,
    client: DeviceClient<C, SasTokenSource<D>>,
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
        let audience = format!("{}/devices/{}", self.hub_id, self.device_id);

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

pub struct HubIdentityManager<K, C, D>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    C: ClientImpl,
    D: 'static + Sign + Clone,
{
    state: Arc<State<K, C, D>>,
    phantom: PhantomData<D>,
}

impl<K, C, D> HubIdentityManager<K, C, D>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    C: ClientImpl,
    D: 'static + Sign + Clone,
{
    pub fn new(key_store: K, client: DeviceClient<C, SasTokenSource<D>>) -> Self {
        HubIdentityManager {
            state: Arc::new(State { key_store, client }),
            phantom: PhantomData,
        }
    }

    fn get_key_pair(&self, id: &str, generation_id: &str) -> Result<(K::Key, K::Key), Error> {
        self.state
            .key_store
            .get(
                &KeyIdentity::Module(id.to_string()),
                &build_key_name(KEY_PRIMARY, generation_id),
            )
            .and_then(|primary_key| {
                self.state
                    .key_store
                    .get(
                        &KeyIdentity::Module(id.to_string()),
                        &build_key_name(KEY_SECONDARY, generation_id),
                    )
                    .map(|secondary_key| (primary_key, secondary_key))
            })
            .context(ErrorKind::CannotGetKey(id.to_string()))
            .map_err(Error::from)
    }
}

fn build_key_name(key_name: &str, generation_id: &str) -> String {
    format!("{}{}", key_name, generation_id)
}

impl<K, C, D> Clone for HubIdentityManager<K, C, D>
where
    K: KeyStore,
    K::Key: AsRef<[u8]> + Clone,
    C: ClientImpl,
    D: 'static + Sign + Clone,
{
    fn clone(&self) -> Self {
        HubIdentityManager {
            state: self.state.clone(),
            phantom: PhantomData,
        }
    }
}

impl<K, C, D> IdentityManager for HubIdentityManager<K, C, D>
where
    K: 'static + KeyStore + Send + Sync,
    K::Key: AsRef<[u8]> + Clone + Send,
    C: 'static + ClientImpl,
    D: 'static + Sign + Clone + Send + Sync,
{
    type Identity = HubIdentity;
    type Error = Error;
    type CreateFuture = Box<Future<Item = Self::Identity, Error = Self::Error> + Send>;
    type UpdateFuture = Box<Future<Item = Self::Identity, Error = Self::Error> + Send>;
    type ListFuture = Box<Future<Item = Vec<Self::Identity>, Error = Self::Error> + Send>;
    type GetFuture = Box<Future<Item = Option<Self::Identity>, Error = Self::Error> + Send>;
    type DeleteFuture = Box<Future<Item = (), Error = Self::Error> + Send>;

    fn create(&mut self, id: IdentitySpec) -> Self::CreateFuture {
        // This code first creates a module in the hub with the auth type
        // set as "None" in order to have a generation identifier generated for
        // the module by the hub. Once we have a generation ID we use it to
        // derive the keys for the module which we then proceed to update in
        // the hub.
        let idman = self.clone();
        let module_id = id.module_id().to_string();
        Box::new(
            self.state
                .client
                .create_module(
                    module_id.clone(),
                    Some(AuthMechanism::default().with_type(HubAuthType::None)),
                    id.managed_by(),
                )
                .then(|module| {
                    let module = module.with_context(|_| {
                        ErrorKind::IdentityOperation(IdentityOperation::CreateIdentity(
                            module_id.clone(),
                        ))
                    })?;

                    if let (Some(module_id2), Some(generation_id)) =
                        (module.module_id(), module.generation_id())
                    {
                        idman.get_key_pair(module_id2, generation_id).map(
                            |(primary_key, secondary_key)| {
                                (primary_key, secondary_key, idman, module_id)
                            },
                        )
                    } else {
                        Err(Error::from(ErrorKind::CreateIdentityWithReason(
                            module_id,
                            IdentityOperationReason::InvalidHubResponse,
                        )))
                    }
                })
                .and_then(move |(primary_key, secondary_key, idman, module_id)| {
                    let auth = AuthMechanism::default()
                        .with_type(HubAuthType::Sas)
                        .with_symmetric_key(
                            SymmetricKey::default()
                                .with_primary_key(base64::encode(primary_key.as_ref()))
                                .with_secondary_key(base64::encode(secondary_key.as_ref())),
                        );

                    idman
                        .state
                        .client
                        .update_module(id.module_id().to_string(), Some(auth), id.managed_by())
                        .map_err(|err| {
                            Error::from(err.context(ErrorKind::CreateIdentityWithReason(
                                module_id,
                                IdentityOperationReason::InvalidHubResponse,
                            )))
                        })
                        .map(HubIdentity::new)
                }),
        )
    }

    fn update(&mut self, id: IdentitySpec) -> Self::UpdateFuture {
        let module_id = id.module_id().to_string();

        let result = if let Some(generation_id) = id.generation_id() {
            match self.get_key_pair(&module_id, generation_id) {
                Ok((primary_key, secondary_key)) => {
                    let auth = AuthMechanism::default()
                        .with_type(HubAuthType::Sas)
                        .with_symmetric_key(
                            SymmetricKey::default()
                                .with_primary_key(base64::encode(primary_key.as_ref()))
                                .with_secondary_key(base64::encode(secondary_key.as_ref())),
                        );

                    Either::A(
                        self.state
                            .client
                            .update_module(module_id.clone(), Some(auth), id.managed_by())
                            .map_err(|err| {
                                Error::from(err.context(ErrorKind::IdentityOperation(
                                    IdentityOperation::UpdateIdentity(module_id),
                                )))
                            })
                            .map(HubIdentity::new),
                    )
                }

                Err(err) => Either::B(future::err(err)),
            }
        } else {
            Either::B(future::err(Error::from(
                ErrorKind::UpdateIdentityWithReason(
                    module_id,
                    IdentityOperationReason::MissingGenerationId,
                ),
            )))
        };

        Box::new(result)
    }

    fn list(&self) -> Self::ListFuture {
        Box::new(
            self.state
                .client
                .list_modules()
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::IdentityOperation(
                        IdentityOperation::ListIdentities,
                    )))
                })
                .map(|modules| modules.into_iter().map(HubIdentity::new).collect()),
        )
    }

    fn get(&self, id: IdentitySpec) -> Self::GetFuture {
        let module_id = id.module_id().to_string();

        Box::new(self.state.client.get_module_by_id(module_id.clone()).then(
            |module| match module {
                Ok(module) => Ok(Some(HubIdentity::new(module))),
                Err(err) => {
                    if let HubErrorKind::GetModuleWithReason(_, HubReason::ModuleNotFound) =
                        err.kind()
                    {
                        Ok(None)
                    } else {
                        Err(Error::from(err.context(ErrorKind::IdentityOperation(
                            IdentityOperation::GetIdentity(module_id),
                        ))))
                    }
                }
            },
        ))
    }

    fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture {
        let module_id = id.module_id().to_string();

        Box::new(self.state.client.delete_module(&module_id).map_err(|err| {
            Error::from(err.context(ErrorKind::IdentityOperation(
                IdentityOperation::DeleteIdentity(module_id),
            )))
        }))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use bytes::Bytes;
    use chrono::TimeZone;
    use futures::Stream;
    use hyper::{self, Body, Method, Request, Response, StatusCode};
    use typed_headers::{mime, ContentType, HeaderMapExt};
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
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_PRIMARY, "g1"),
            MemoryKey::new("pkey"),
        );
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_SECONDARY, "g1"),
            MemoryKey::new("skey"),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request<Body>| Ok(Response::new(Body::empty()));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let (pkey, skey) = identity_manager.get_key_pair("m1", "g1").unwrap();

        assert_eq!(pkey.as_ref(), &Bytes::from("pkey"));
        assert_eq!(skey.as_ref(), &Bytes::from("skey"));
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_module() {
        let key_store = MemoryKeyStore::new();
        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request<Body>| Ok(Response::new(Body::empty()));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager.get_key_pair("m1", "g1").unwrap();
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_pkey() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_SECONDARY, "g1"),
            MemoryKey::new("skey"),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request<Body>| Ok(Response::new(Body::empty()));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager.get_key_pair("m1", "g1").unwrap();
    }

    #[test]
    #[should_panic(expected = "KeyStore could not fetch keys for module")]
    fn get_key_pair_fails_for_no_skey() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_PRIMARY, "g1"),
            MemoryKey::new("pkey"),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let handler = |_req: Request<Body>| Ok(Response::new(Body::empty()));
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        identity_manager.get_key_pair("m1", "g1").unwrap();
    }

    #[test]
    fn create_succeeds() {
        let mut key_store = MemoryKeyStore::new();
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_PRIMARY, "g1"),
            MemoryKey::new("pkey"),
        );
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            &format!("{}{}", KEY_SECONDARY, "g1"),
            MemoryKey::new("skey"),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let expected_module1 = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_authentication(AuthMechanism::default().with_type(HubAuthType::None));
        let expected_module2 = expected_module1.clone().with_authentication(
            AuthMechanism::default()
                .with_type(HubAuthType::Sas)
                .with_symmetric_key(
                    SymmetricKey::default()
                        .with_primary_key(base64::encode(MemoryKey::new("pkey").as_ref()))
                        .with_secondary_key(base64::encode(MemoryKey::new("skey").as_ref())),
                ),
        );
        let expected_module_result = expected_module2
            .clone()
            .with_generation_id("g1".to_string())
            .with_managed_by("iotedge".to_string());

        let handler = move |req: Request<Body>| {
            assert_eq!(req.method(), &Method::PUT);
            assert_eq!(req.uri().path(), "/devices/d1/modules/m1");

            // if the request has an If-Match header then this is an update
            // module request
            let mut is_update = false;
            if let Some(header) = req.headers().get(hyper::header::IF_MATCH) {
                assert_eq!(header, "*");
                is_update = true;
            }

            let expected_module_copy = if is_update {
                expected_module2.clone()
            } else {
                expected_module1.clone()
            };

            req.into_body()
                .concat2()
                .and_then(|req_body| Ok(serde_json::from_slice::<Module>(&req_body).unwrap()))
                .and_then(move |module| {
                    assert_eq!(module, expected_module_copy);

                    let mut response = Response::new(
                        serde_json::to_string(
                            &module
                                .with_generation_id("g1".to_string())
                                .with_managed_by("iotedge".to_string()),
                        )
                        .unwrap()
                        .into(),
                    );
                    response
                        .headers_mut()
                        .typed_insert(&ContentType(mime::APPLICATION_JSON));
                    Ok(response)
                })
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let mut identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.create(IdentitySpec::new("m1".to_string()));

        let hub_identity = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        assert_eq!(hub_identity.hub_module(), &expected_module_result);
    }

    #[test]
    fn list_succeeds() {
        let m1pkey = "m1pkey";
        let m1skey = "m1skey";
        let m2pkey = "m2pkey";
        let m2skey = "m2skey";

        let mut key_store = MemoryKeyStore::new();
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            KEY_PRIMARY,
            MemoryKey::new(m1pkey),
        );
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            KEY_SECONDARY,
            MemoryKey::new(m1skey),
        );
        key_store.insert(
            &KeyIdentity::Module("m2".to_string()),
            KEY_PRIMARY,
            MemoryKey::new(m2pkey),
        );
        key_store.insert(
            &KeyIdentity::Module("m2".to_string()),
            KEY_SECONDARY,
            MemoryKey::new(m2skey),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response_modules = vec![
            Module::default()
                .with_device_id("d1".to_string())
                .with_module_id("m1".to_string())
                .with_authentication(
                    AuthMechanism::default()
                        .with_type(HubAuthType::Sas)
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
                        .with_type(HubAuthType::Sas)
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

        let handler = move |req: Request<Body>| {
            assert_eq!(req.method(), &Method::GET);
            assert_eq!(req.uri().path(), "/devices/d1/modules");

            let mut response = Response::new(
                serde_json::to_string(
                    &response_modules
                        .iter()
                        .map(|module| {
                            module
                                .clone()
                                .with_generation_id("g1".to_string())
                                .with_managed_by("iotedge".to_string())
                        })
                        .collect::<Vec<Module>>(),
                )
                .unwrap()
                .into(),
            );
            response
                .headers_mut()
                .typed_insert(&ContentType(mime::APPLICATION_JSON));
            Ok(response)
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.list();

        let hub_identities = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

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
    fn get_succeeds() {
        let m1pkey = "m1pkey";
        let m1skey = "m1skey";

        let mut key_store = MemoryKeyStore::new();
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            KEY_PRIMARY,
            MemoryKey::new(m1pkey),
        );
        key_store.insert(
            &KeyIdentity::Module("m1".to_string()),
            KEY_SECONDARY,
            MemoryKey::new(m1skey),
        );

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();
        let response_module = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_authentication(
                AuthMechanism::default()
                    .with_type(HubAuthType::Sas)
                    .with_symmetric_key(
                        SymmetricKey::default()
                            .with_primary_key(base64::encode(MemoryKey::new(m1pkey).as_ref()))
                            .with_secondary_key(base64::encode(MemoryKey::new(m1skey).as_ref())),
                    ),
            );

        let expected_module_result = response_module
            .clone()
            .with_generation_id("g1".to_string())
            .with_managed_by("iotedge".to_string());

        let handler = move |req: Request<Body>| {
            assert_eq!(req.method(), &Method::GET);
            assert_eq!(req.uri().path(), "/devices/d1/modules/m1");

            let mut response = Response::new(
                serde_json::to_string(
                    &response_module
                        .clone()
                        .with_generation_id("g1".to_string())
                        .with_managed_by("iotedge".to_string()),
                )
                .unwrap()
                .into(),
            );
            response
                .headers_mut()
                .typed_insert(&ContentType(mime::APPLICATION_JSON));
            Ok(response)
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.get(IdentitySpec::new("m1".to_string()));

        let hub_identity = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap()
            .unwrap();
        assert_eq!(hub_identity.hub_module(), &expected_module_result);
    }

    #[test]
    fn get_module_not_found() {
        let key_store = MemoryKeyStore::new();

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();

        let handler = move |req: Request<Body>| {
            assert_eq!(req.method(), &Method::GET);
            assert_eq!(req.uri().path(), "/devices/d1/modules/m1");

            let response = Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .expect("could not build hyper::Response");
            Ok(response)
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager.get(IdentitySpec::new("m1".to_string()));

        let hub_identity = tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
        assert_eq!(None, hub_identity);
    }

    #[test]
    fn delete_succeeds() {
        let key_store = MemoryKeyStore::new();

        let api_version = "2018-04-10".to_string();
        let host_name = Url::parse("http://localhost").unwrap();

        let handler = move |req: Request<Body>| {
            assert_eq!(req.method(), &Method::DELETE);
            assert_eq!(req.uri().path(), "/devices/d1/modules/m1");

            Ok(Response::new(Body::empty()))
        };
        let token_source = SasTokenSource::new(
            "hub".to_string(),
            "device".to_string(),
            MemoryKey::new("device"),
        );
        let client = Client::new(handler, Some(token_source), api_version, host_name).unwrap();
        let device_client = DeviceClient::new(client, "d1".to_string()).unwrap();

        let mut identity_manager = HubIdentityManager::new(key_store, device_client);
        let task = identity_manager
            .delete(IdentitySpec::new("m1".to_string()))
            .then(|result: Result<(), _>| result);

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn token_source_success() {
        // arrange
        let hub_id = "Miyagley-Edge.azure-devices.net".to_string();
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

    #[test]
    fn token_source_success_equals() {
        // arrange
        let hub_id = "Mi=yagley-Edge.azure-devices.net".to_string();
        let device_id = "miYagley1".to_string();
        let key = MemoryKey::new(base64::decode("key").unwrap());
        let token_source = SasTokenSource::new(hub_id, device_id, key);
        let expiry = Utc.ymd(2018, 4, 26).and_hms(20, 54, 15);

        // act
        let token = token_source.get(&expiry).unwrap();

        // assert
        let expected = concat!(
            "sr=mi%3Dyagley-edge.azure-devices.net",
            "%2Fdevices%2Fmiyagley1&sig=R6NGzTl9wTWWI2zqrOhL81kH1H",
            "Pi%2F15XDxc%2FjLVMHcY%3D&se=1524776055",
        );
        assert_eq!(expected, token);
    }
}
