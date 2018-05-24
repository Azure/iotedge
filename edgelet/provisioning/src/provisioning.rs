// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;

use bytes::Bytes;

use futures::future::Either;
use futures::{future, Future};
use hyper::client::Service;
use hyper::{Error as HyperError, Request, Response};

use regex::RegexSet;
use url::Url;

use dps::registration::{DpsClient, DpsTokenSource};
use edgelet_core::crypto::{Activate, KeyStore, MemoryKey, MemoryKeyStore};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::Client as HttpClient;
use error::{Error, ErrorKind};
use hsm::TpmKey as HsmTpmKey;

static DEVICEID_KEY: &'static str = "DeviceId";
static HOSTNAME_KEY: &'static str = "HostName";
static SHAREDACCESSKEY_KEY: &'static str = "SharedAccessKey";

static DEVICEID_REGEX: &'static str = r"DeviceId=([A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128})";
static HOSTNAME_REGEX: &'static str = r"HostName=([a-zA-Z0-9_\-\.]+)";
static SHAREDACCESSKEY_REGEX: &'static str = r"SharedAccessKey=(.+)";

pub struct ProvisioningResult {
    device_id: String,
    hub_name: String,
}

impl ProvisioningResult {
    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn hub_name(&self) -> &str {
        &self.hub_name
    }
}

pub trait Provision {
    type Hsm: Activate + KeyStore;
    fn provision(
        &self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>>;
}

#[derive(Debug)]
pub struct ManualProvisioning {
    connection_string: String,
    hash_map: HashMap<String, String>,
}

impl ManualProvisioning {
    pub fn new(conn_string: &str) -> Result<Self, Error> {
        ensure_not_empty!(conn_string);
        let hash_map = ManualProvisioning::parse_conn_string(conn_string)?;
        Ok(ManualProvisioning {
            connection_string: conn_string.to_string(),
            hash_map,
        })
    }

    pub fn key(&self) -> Result<&str, Error> {
        self.hash_map
            .get(SHAREDACCESSKEY_KEY)
            .map(|s| s.as_str())
            .ok_or_else(|| Error::from(ErrorKind::NotFound))
    }

    fn parse_conn_string(conn_string: &str) -> Result<HashMap<String, String>, Error> {
        let mut hash_map = HashMap::new();
        let parts: Vec<&str> = conn_string.split(';').collect();
        let set = RegexSet::new(&[DEVICEID_REGEX, HOSTNAME_REGEX, SHAREDACCESSKEY_REGEX])?;
        let matches: Vec<_> = set.matches(conn_string).into_iter().collect();
        if matches != vec![0, 1, 2] {
            // Error if all three components are not provided
            return Err(Error::from(ErrorKind::Provision(
                "Invalid connection string".to_string(),
            )));
        }
        for p in parts {
            let s: Vec<&str> = p.split('=').collect();
            if set.is_match(p) {
                hash_map.insert(s[0].to_string(), s[1].to_string());
            } // Ignore extraneous component in the connection string
        }
        Ok(hash_map)
    }
}

impl Provision for ManualProvisioning {
    type Hsm = MemoryKeyStore;

    fn provision(
        &self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
        let r = self.key()
            .map(move |k| {
                key_activator
                    .clone()
                    .activate_identity_key(
                        "device".to_string(),
                        "primary".to_string(),
                        MemoryKey::new(k),
                    )
                    .map(|_| {
                        Either::A(future::ok(ProvisioningResult {
                            device_id: self.hash_map[DEVICEID_KEY].clone(),
                            hub_name: self.hash_map[HOSTNAME_KEY].clone(),
                        }))
                    })
                    .unwrap_or_else(|err| Either::B(future::err(Error::from(err))))
            })
            .unwrap_or_else(|err| Either::B(future::err(err)));
        Box::new(r)
    }
}

pub struct DpsProvisioning<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    client: HttpClient<S, DpsTokenSource<TpmKey>>,
    scope_id: String,
    registration_id: String,
    hsm_tpm_ek: HsmTpmKey,
    hsm_tpm_srk: HsmTpmKey,
}

impl<S> DpsProvisioning<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    pub fn new(
        service: S,
        endpoint: Url,
        scope_id: String,
        registration_id: String,
        api_version: &str,
        hsm_tpm_ek: HsmTpmKey,
        hsm_tpm_srk: HsmTpmKey,
    ) -> Result<DpsProvisioning<S>, Error> {
        Ok(DpsProvisioning {
            client: HttpClient::new(
                service,
                None as Option<DpsTokenSource<TpmKey>>,
                &api_version,
                endpoint,
            ).expect("Failed getting Http client"),
            scope_id,
            registration_id,
            hsm_tpm_ek,
            hsm_tpm_srk,
        })
    }
}

impl<S> Provision for DpsProvisioning<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    type Hsm = TpmKeyStore;

    fn provision(
        &self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
        let d = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            Bytes::from(self.hsm_tpm_ek.as_ref()),
            Bytes::from(self.hsm_tpm_srk.as_ref()),
            key_activator,
            30, /* seconds to retry pinging dps for status */
        ).map(|c| {
            Either::A(
                c.register()
                    .map(|(device_id, hub_name)| ProvisioningResult {
                        device_id,
                        hub_name,
                    })
                    .map_err(Error::from),
            )
        })
            .unwrap_or_else(|err| Either::B(future::err(Error::from(err))));

        Box::new(d)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tokio_core::reactor::Core;

    #[test]
    fn manual_get_credentials_success() {
        let mut core = Core::new().unwrap();
        let provisioning =
            ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(provisioning.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .unwrap()
            .provision(memory_hsm.clone())
            .then(|result| {
                match result {
                    Ok(result) => {
                        assert_eq!(result.hub_name, "test.com".to_string());
                        assert_eq!(result.device_id, "test".to_string());
                    }
                    Err(err) => panic!("Unexpected {:?}", err),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task).unwrap();
    }

    #[test]
    fn manual_malformed_conn_string_gets_error() {
        let test = ManualProvisioning::new("HostName=test.com;DeviceId=test;");
        assert_eq!(test.is_err(), true);
    }

    #[test]
    fn connection_string_split_success() {
        let mut core = Core::new().unwrap();
        let provisioning =
            ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(provisioning.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .unwrap()
            .provision(memory_hsm.clone())
            .then(|result| {
                match result {
                    Ok(result) => {
                        assert_eq!(result.hub_name, "test.com".to_string());
                        assert_eq!(result.device_id, "test".to_string());
                    }
                    Err(_) => panic!("Unexpected"),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task).unwrap();

        let provisioning1 =
            ManualProvisioning::new("DeviceId=test;SharedAccessKey=test;HostName=test.com");
        assert_eq!(provisioning1.is_ok(), true);
        let task1 = provisioning1
            .unwrap()
            .provision(memory_hsm.clone())
            .then(|result| {
                match result {
                    Ok(result) => {
                        assert_eq!(result.hub_name, "test.com".to_string());
                        assert_eq!(result.device_id, "test".to_string());
                    }
                    Err(_) => panic!("Unexpected"),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task1).unwrap();
    }

    #[test]
    fn connection_string_split_error() {
        let mut core = Core::new().unwrap();
        let test1 = ManualProvisioning::new("DeviceId=test;SharedAccessKey=test");
        assert_eq!(test1.is_err(), true);
        let test2 = ManualProvisioning::new(
            "HostName=test.com;Extra=something;DeviceId=test;SharedAccessKey=test",
        );
        assert_eq!(test2.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task1 = test2.unwrap().provision(memory_hsm.clone()).then(|result| {
            match result {
                Ok(result) => {
                    assert_eq!(result.hub_name, "test.com".to_string());
                    assert_eq!(result.device_id, "test".to_string());
                }
                Err(_) => panic!("Unexpected"),
            }
            Ok(()) as Result<(), Error>
        });
        core.run(task1).unwrap();
    }
}
