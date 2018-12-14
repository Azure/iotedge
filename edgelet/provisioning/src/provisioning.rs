// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fs::File;
use std::io::{Read, Write};
use std::path::PathBuf;

use base64;
use bytes::Bytes;

use failure::{Fail, ResultExt};
use futures::future::Either;
use futures::{future, Future, IntoFuture};
use regex::Regex;
use serde_json;
use url::Url;

use dps::registration::{DpsClient, DpsTokenSource};
use edgelet_core::crypto::{Activate, KeyIdentity, KeyStore, MemoryKey, MemoryKeyStore};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_utils::{ensure_not_empty_with_context, log_failure};
use error::{Error, ErrorKind};
use hsm::TpmKey as HsmTpmKey;
use log::Level;

const DEVICEID_KEY: &str = "DeviceId";
const HOSTNAME_KEY: &str = "HostName";
const SHAREDACCESSKEY_KEY: &str = "SharedAccessKey";

const DEVICEID_REGEX: &str = r"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$";
const HOSTNAME_REGEX: &str = r"^[a-zA-Z0-9_\-\.]+$";
const SHAREDACCESSKEY_REGEX: &str = r"^.+$";

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
    ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send>;
}

#[derive(Debug)]
pub struct ManualProvisioning {
    key: MemoryKey,
    device_id: String,
    hub: String,
}

impl ManualProvisioning {
    pub fn new(conn_string: &str) -> Result<Self, Error> {
        ensure_not_empty_with_context(&conn_string, || ErrorKind::InvalidConnString)?;

        let hash_map = ManualProvisioning::parse_conn_string(&conn_string)?;

        let key = hash_map.get(SHAREDACCESSKEY_KEY).ok_or(
            ErrorKind::ConnStringMissingRequiredParameter(SHAREDACCESSKEY_KEY),
        )?;
        let key_regex = Regex::new(SHAREDACCESSKEY_REGEX)
            .expect("This hard-coded regex is expected to be valid.");
        if !key_regex.is_match(&key) {
            return Err(Error::from(ErrorKind::ConnStringMalformedParameter(
                SHAREDACCESSKEY_REGEX,
            )));
        }
        let key = MemoryKey::new(base64::decode(&key).context(
            ErrorKind::ConnStringMalformedParameter(SHAREDACCESSKEY_REGEX),
        )?);

        let device_id = hash_map
            .get(DEVICEID_KEY)
            .ok_or(ErrorKind::ConnStringMissingRequiredParameter(DEVICEID_KEY))?;
        let device_id_regex =
            Regex::new(DEVICEID_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !device_id_regex.is_match(&device_id) {
            return Err(Error::from(ErrorKind::ConnStringMalformedParameter(
                DEVICEID_KEY,
            )));
        }

        let hub = hash_map
            .get(HOSTNAME_KEY)
            .ok_or(ErrorKind::ConnStringMissingRequiredParameter(HOSTNAME_KEY))?;
        let hub_regex =
            Regex::new(HOSTNAME_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !hub_regex.is_match(&hub) {
            return Err(Error::from(ErrorKind::ConnStringMalformedParameter(
                HOSTNAME_KEY,
            )));
        }

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
        for p in parts {
            let s: Vec<&str> = p.split('=').collect();
            match s[0] {
                SHAREDACCESSKEY_KEY | DEVICEID_KEY | HOSTNAME_KEY => {
                    hash_map.insert(s[0].to_string(), s[1].to_string());
                }
                _ => (), // Ignore extraneous component in the connection string
            }
        }
        Ok(hash_map)
    }
}

impl Provision for ManualProvisioning {
    type Hsm = MemoryKeyStore;

    fn provision(
        self,
        mut key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send> {
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
            .map_err(|err| Error::from(err.context(ErrorKind::Provision)));
        Box::new(result.into_future())
    }
}

pub struct DpsProvisioning<C>
where
    C: ClientImpl,
{
    client: HttpClient<C, DpsTokenSource<TpmKey>>,
    scope_id: String,
    registration_id: String,
    hsm_tpm_ek: HsmTpmKey,
    hsm_tpm_srk: HsmTpmKey,
}

impl<C> DpsProvisioning<C>
where
    C: ClientImpl,
{
    pub fn new(
        client_impl: C,
        endpoint: Url,
        scope_id: String,
        registration_id: String,
        api_version: String,
        hsm_tpm_ek: HsmTpmKey,
        hsm_tpm_srk: HsmTpmKey,
    ) -> Result<Self, Error> {
        let client = HttpClient::new(
            client_impl,
            None as Option<DpsTokenSource<TpmKey>>,
            api_version,
            endpoint,
        )
        .context(ErrorKind::DpsInitialization)?;

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

impl<C> Provision for DpsProvisioning<C>
where
    C: 'static + ClientImpl,
{
    type Hsm = TpmKeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send> {
        let c = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            Bytes::from(self.hsm_tpm_ek.as_ref()),
            Bytes::from(self.hsm_tpm_srk.as_ref()),
            key_activator,
        );

        let d = match c {
            Ok(c) => Either::A(
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
                    .map_err(|err| Error::from(err.context(ErrorKind::Provision))),
            ),
            Err(err) => Either::B(future::err(Error::from(err.context(ErrorKind::Provision)))),
        };

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
        let mut file = File::create(path).context(ErrorKind::CouldNotBackup)?;
        let buffer = serde_json::to_string(&prov_result).context(ErrorKind::CouldNotBackup)?;
        file.write_all(buffer.as_bytes())
            .context(ErrorKind::CouldNotBackup)?;
        Ok(())
    }

    fn restore(path: PathBuf) -> Result<ProvisioningResult, Error> {
        let mut file = File::open(path).context(ErrorKind::CouldNotRestore)?;
        let mut buffer = String::new();
        let _ = file
            .read_to_string(&mut buffer)
            .context(ErrorKind::CouldNotRestore)?;
        info!("Restoring device credentials from backup");
        let prov_result = serde_json::from_str(&buffer).context(ErrorKind::CouldNotRestore)?;
        Ok(prov_result)
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
    ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send> {
        let path = self.path.clone();
        let path_on_err = self.path.clone();
        Box::new(
            self.underlying
                .provision(key_activator)
                .and_then(move |mut prov_result| {
                    prov_result.reconfigure = true;
                    match Self::backup(&prov_result, path) {
                        Ok(_) => Either::A(future::ok(prov_result.clone())),
                        Err(err) => Either::B(future::err(err)),
                    }
                })
                .or_else(move |err| {
                    log_failure(Level::Warn, &err);
                    match Self::restore(path_on_err) {
                        Ok(prov_result) => Either::A(future::ok(prov_result)),
                        Err(err) => Either::B(future::err(err)),
                    }
                }),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use tempdir::TempDir;
    use tokio;

    use error::ErrorKind;

    struct TestProvisioning {}

    impl Provision for TestProvisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send> {
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
        ) -> Box<Future<Item = ProvisioningResult, Error = Error> + Send> {
            Box::new(future::err(Error::from(ErrorKind::Provision)))
        }
    }

    #[test]
    fn manual_get_credentials_success() {
        let provisioning =
            ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(provisioning.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task =
            provisioning
                .unwrap()
                .provision(memory_hsm.clone())
                .then(|result| match result {
                    Ok(result) => {
                        assert_eq!(result.hub_name, "test.com".to_string());
                        assert_eq!(result.device_id, "test".to_string());
                        Ok::<_, Error>(())
                    }
                    Err(err) => panic!("Unexpected {:?}", err),
                });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn manual_malformed_conn_string_gets_error() {
        let test = ManualProvisioning::new("HostName=test.com;DeviceId=test;");
        assert_eq!(test.is_err(), true);
    }

    #[test]
    fn connection_string_split_success() {
        let provisioning =
            ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(provisioning.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .unwrap()
            .provision(memory_hsm.clone())
            .then(|result| {
                let result = result.expect("Unexpected");
                assert_eq!(result.hub_name, "test.com".to_string());
                assert_eq!(result.device_id, "test".to_string());
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let provisioning1 =
            ManualProvisioning::new("DeviceId=test;SharedAccessKey=test;HostName=test.com");
        assert_eq!(provisioning1.is_ok(), true);
        let task1 = provisioning1
            .unwrap()
            .provision(memory_hsm.clone())
            .then(|result| {
                let result = result.expect("Unexpected");
                assert_eq!(result.hub_name, "test.com".to_string());
                assert_eq!(result.device_id, "test".to_string());
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task1)
            .unwrap();
    }

    #[test]
    fn connection_string_split_error() {
        let test1 = ManualProvisioning::new("DeviceId=test;SharedAccessKey=test");
        assert_eq!(test1.is_err(), true);
        let test2 = ManualProvisioning::new(
            "HostName=test.com;Extra=something;DeviceId=test;SharedAccessKey=test",
        );
        assert_eq!(test2.is_ok(), true);
        let memory_hsm = MemoryKeyStore::new();
        let task1 = test2.unwrap().provision(memory_hsm.clone()).then(|result| {
            let result = result.expect("Unexpected");
            assert_eq!(result.hub_name, "test.com".to_string());
            assert_eq!(result.device_id, "test".to_string());
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task1)
            .unwrap();
    }

    #[test]
    fn backup_success() {
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_clone = file_path.clone();
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper
            .provision(MemoryKeyStore::new())
            .then(|result| {
                let _ = result.expect("Unexpected");
                let result =
                    BackupProvisioning::<ManualProvisioning>::restore(file_path_clone).unwrap();
                assert_eq!(result.device_id(), "TestDevice");
                assert_eq!(result.hub_name(), "TestHub");
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn restore_success() {
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_clone = file_path.clone();
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(TestProvisioningWithError {}, file_path_clone);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                let prov_result = result.expect("Unexpected");
                assert_eq!(prov_result.device_id(), "TestDevice");
                assert_eq!(prov_result.hub_name(), "TestHub");
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task1)
            .unwrap();
    }

    #[test]
    fn restore_failure() {
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_wrong = tmp_dir.path().join("dps_backup_wrong.json");
        let prov_wrapper = BackupProvisioning::new(test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(TestProvisioningWithError {}, file_path_wrong);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                match result {
                    Ok(_) => panic!("Unexpected"),
                    Err(_) => assert_eq!(1, 1),
                }
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task1)
            .unwrap();
    }

    #[test]
    fn prov_result_serialize_skips_reconfigure_flag() {
        let json = serde_json::to_string(&ProvisioningResult {
            device_id: "something".to_string(),
            hub_name: "something".to_string(),
            reconfigure: true,
        })
        .unwrap();
        assert_eq!(
            "{\"device_id\":\"something\",\"hub_name\":\"something\"}",
            json
        );
        let result: ProvisioningResult = serde_json::from_str(&json).unwrap();
        assert_eq!(result.reconfigure, false)
    }
}
