// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fs::File;
use std::io::{Read, Write};
use std::path::PathBuf;

use base64;
use bytes::Bytes;

use futures::future::Either;
use futures::{future, Future};
use hyper::client::Service;
use hyper::{Error as HyperError, Request, Response};
use regex::RegexSet;
use serde_json;
use url::Url;

use dps::registration::{DpsClient, DpsTokenSource};
use edgelet_core::crypto::{Activate, KeyIdentity, KeyStore, MemoryKey, MemoryKeyStore};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::Client as HttpClient;
use edgelet_utils::log_failure;
use error::{Error, ErrorKind};
use hsm::TpmKey as HsmTpmKey;
use log::Level;

static DEVICEID_KEY: &'static str = "DeviceId";
static HOSTNAME_KEY: &'static str = "HostName";
static SHAREDACCESSKEY_KEY: &'static str = "SharedAccessKey";

static DEVICEID_REGEX: &'static str = r"DeviceId=([A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128})";
static HOSTNAME_REGEX: &'static str = r"HostName=([a-zA-Z0-9_\-\.]+)";
static SHAREDACCESSKEY_REGEX: &'static str = r"SharedAccessKey=(.+)";

#[derive(Clone, Serialize, Deserialize)]
pub struct ProvisioningResult {
    device_id: String,
    hub_name: String,
    #[serde(skip)]
    reconfigure: bool,
}

impl ProvisioningResult {
    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn hub_name(&self) -> &str {
        &self.hub_name
    }

    pub fn reconfigure(&self) -> bool {
        self.reconfigure
    }
}

pub trait Provision {
    type Hsm: Activate + KeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>>;
}

#[derive(Debug)]
pub struct ManualProvisioning {
    key: MemoryKey,
    device_id: String,
    hub: String,
}

impl ManualProvisioning {
    pub fn new(conn_string: &str) -> Result<Self, Error> {
        ensure_not_empty!(conn_string, "The Connection string is empty or invalid. Please update the config.yaml and provide the IoTHub connection information.");
        let hash_map = ManualProvisioning::parse_conn_string(conn_string)?;

        let key_str = hash_map
            .get(SHAREDACCESSKEY_KEY)
            .map(|s| s.as_str())
            .ok_or_else(|| Error::from(ErrorKind::NotFound))?;
        let key = MemoryKey::new(base64::decode(key_str)?);

        let device_id = hash_map
            .get(DEVICEID_KEY)
            .ok_or_else(|| Error::from(ErrorKind::NotFound))?;

        let hub = hash_map
            .get(HOSTNAME_KEY)
            .ok_or_else(|| Error::from(ErrorKind::NotFound))?;

        let result = ManualProvisioning {
            key,
            device_id: device_id.to_owned(),
            hub: hub.to_owned(),
        };
        Ok(result)
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
        self,
        mut key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
        let ManualProvisioning {
            key,
            device_id,
            hub,
        } = self;

        info!(
            "Manually provisioning device \"{}\" in hub \"{}\"",
            &device_id, &hub
        );
        let result = key_activator
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), key)
            .map(|_| ProvisioningResult {
                device_id,
                hub_name: hub,
                reconfigure: false,
            })
            .map_err(Error::from);
        Box::new(future::result(result))
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
        let client = HttpClient::new(
            service,
            None as Option<DpsTokenSource<TpmKey>>,
            &api_version,
            endpoint,
        )?;

        let result = DpsProvisioning {
            client,
            scope_id,
            registration_id,
            hsm_tpm_ek,
            hsm_tpm_srk,
        };
        Ok(result)
    }
}

impl<S> Provision for DpsProvisioning<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    type Hsm = TpmKeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
        let d = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            Bytes::from(self.hsm_tpm_ek.as_ref()),
            Bytes::from(self.hsm_tpm_srk.as_ref()),
            key_activator,
        ).map(|c| {
            Either::A(
                c.register()
                    .map(|(device_id, hub_name)| {
                        info!(
                            "DPS registration assigned device \"{}\" in hub \"{}\"",
                            device_id, hub_name
                        );
                        ProvisioningResult {
                            device_id,
                            hub_name,
                            reconfigure: false,
                        }
                    })
                    .map_err(Error::from),
            )
        })
            .unwrap_or_else(|err| Either::B(future::err(Error::from(err))));

        Box::new(d)
    }
}

pub struct BackupProvisioning<P>
where
    P: 'static + Provision,
{
    underlying: P,
    path: PathBuf,
}

impl<P> BackupProvisioning<P>
where
    P: 'static + Provision,
{
    pub fn new(provisioner: P, path: PathBuf) -> Self {
        BackupProvisioning {
            underlying: provisioner,
            path,
        }
    }

    fn backup(prov_result: &ProvisioningResult, path: PathBuf) -> Result<(), Error> {
        // create a file if it doesn't exist, else open it for writing
        let mut file = File::create(path)?;
        let buffer = serde_json::to_string(&prov_result)?;
        file.write_all(buffer.as_bytes())?;
        Ok(())
    }

    fn restore(path: PathBuf) -> Result<ProvisioningResult, Error> {
        let mut file = File::open(path)?;
        let mut buffer = String::new();
        file.read_to_string(&mut buffer)
            .map(|_| {
                info!("Restoring device credentials from backup");
                serde_json::from_str(&buffer).map_err(Error::from)
            })
            .map_err(|err| {
                log_failure(Level::Warn, &err);
                err
            })?
    }
}

impl<P> Provision for BackupProvisioning<P>
where
    P: 'static + Provision,
{
    type Hsm = P::Hsm;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
        let path = self.path.clone();
        let path_on_err = self.path.clone();
        Box::new(
            self.underlying
                .provision(key_activator)
                .and_then(move |mut prov_result| {
                    prov_result.reconfigure = true;
                    Self::backup(&prov_result, path)
                        .map(|_| Either::A(future::ok(prov_result.clone())))
                        .unwrap_or_else(|err| Either::B(future::err(err)))
                })
                .or_else(move |err| {
                    log_failure(Level::Warn, &err);
                    Self::restore(path_on_err)
                        .map(|prov_result| Either::A(future::ok(prov_result)))
                        .unwrap_or_else(|err| Either::B(future::err(err)))
                }),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use tempdir::TempDir;
    use tokio_core::reactor::Core;

    use error::ErrorKind;

    struct TestProvisioning {}

    impl Provision for TestProvisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
            Box::new(future::ok(ProvisioningResult {
                device_id: "TestDevice".to_string(),
                hub_name: "TestHub".to_string(),
                reconfigure: false,
            }))
        }
    }

    struct TestProvisioningWithError {}

    impl Provision for TestProvisioningWithError {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<Future<Item = ProvisioningResult, Error = Error>> {
            Box::new(future::err(Error::from(ErrorKind::Dps)))
        }
    }

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

    #[test]
    fn backup_success() {
        let mut core = Core::new().unwrap();
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_clone = file_path.clone();
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper
            .provision(MemoryKeyStore::new())
            .then(|result| {
                match result {
                    Ok(_) => {
                        let result = BackupProvisioning::<ManualProvisioning>::restore(
                            file_path_clone,
                        ).unwrap();
                        assert_eq!(result.device_id(), "TestDevice");
                        assert_eq!(result.hub_name(), "TestHub");
                    }
                    Err(_) => panic!("Unexpected"),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task).unwrap();
    }

    #[test]
    fn restore_success() {
        let mut core = Core::new().unwrap();
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_clone = file_path.clone();
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        core.run(task).unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(TestProvisioningWithError {}, file_path_clone);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                match result {
                    Ok(_) => {
                        let prov_result = result.unwrap();
                        assert_eq!(prov_result.device_id(), "TestDevice");
                        assert_eq!(prov_result.hub_name(), "TestHub");
                    }
                    Err(_) => panic!("Unexpected"),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task1).unwrap();
    }

    #[test]
    fn restore_failure() {
        let mut core = Core::new().unwrap();
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_wrong = tmp_dir.path().join("dps_backup_wrong.json");
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        core.run(task).unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(TestProvisioningWithError {}, file_path_wrong);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                match result {
                    Ok(_) => panic!("Unexpected"),
                    Err(_) => assert_eq!(1, 1),
                }
                Ok(()) as Result<(), Error>
            });
        core.run(task1).unwrap();
    }

    #[test]
    fn prov_result_serialize_skips_reconfigure_flag() {
        let json = serde_json::to_string(&ProvisioningResult {
            device_id: "something".to_string(),
            hub_name: "something".to_string(),
            reconfigure: true,
        }).unwrap();
        assert_eq!(
            "{\"device_id\":\"something\",\"hub_name\":\"something\"}",
            json
        );
        let result: ProvisioningResult = serde_json::from_str(&json).unwrap();
        assert_eq!(result.reconfigure, false)
    }
}
