// Copyright (c) Microsoft. All rights reserved.

use std::fs::File;
use std::io::{Read, Write};
use std::marker::PhantomData;
use std::path::PathBuf;

use bytes::Bytes;
use failure::{Fail, ResultExt};
use futures::future::Either;
use futures::{future, Future, IntoFuture};
use log::info;
use serde_derive::{Deserialize, Serialize};
use serde_json;
use url::Url;

use dps::registration::{DpsAuthKind, DpsClient, DpsTokenSource};
use edgelet_core::crypto::{Activate, KeyIdentity, KeyStore, MemoryKey, MemoryKeyStore};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http_external_provisioning::ExternalProvisioningInterface;
use edgelet_utils::log_failure;
use hsm::TpmKey as HsmTpmKey;
use log::{debug, Level};
use sha2::{Digest, Sha256};

use crate::error::{Error, ErrorKind};

const EXTERNAL_PROVISIONING_KEY_SENTINEL: &str = "_SENTINEL_";

#[derive(Clone, Copy, Serialize, Deserialize, Debug, PartialEq)]
pub enum ReprovisioningStatus {
    DeviceDataNotUpdated,
    DeviceDataUpdated,
    InitialAssignment,
    DeviceDataMigrated,
    DeviceDataReset,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct SymmetricKeyCredential {
    #[serde(skip_serializing_if = "Option::is_none")]
    key: Option<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct X509Credential {
    identity_cert: String,
    identity_private_key: String,
}

#[derive(Clone, Copy, Serialize, Deserialize, Debug, PartialEq)]
pub enum AuthType {
    SymmetricKey(SymmetricKeyCredential),
    X509(X509Credential)
}

#[derive(Clone, Copy, Serialize, Deserialize, Debug, PartialEq)]
pub enum CredentialSource {
    Payload,
    Hsm
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Credentials {
    auth_type: AuthType,
    source: CredentialSource,
}

impl From<&str> for ReprovisioningStatus {
    fn from(s: &str) -> ReprovisioningStatus {
        // TODO: check with DPS substatus value for DeviceDataUpdated when it is implemented on service side
        match s {
            "deviceDataMigrated" => ReprovisioningStatus::DeviceDataMigrated,
            "deviceDataReset" => ReprovisioningStatus::DeviceDataReset,
            "initialAssignment" => ReprovisioningStatus::InitialAssignment,
            _ => {
                debug!("DPS provisioning result substatus {}", s);
                ReprovisioningStatus::InitialAssignment
            }
        }
    }
}

impl Default for ReprovisioningStatus {
    fn default() -> Self {
        ReprovisioningStatus::InitialAssignment
    }
}

#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct ProvisioningResult {
    device_id: String,
    hub_name: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    sha256_thumbprint: Option<String>,
    #[serde(skip)]
    reconfigure: ReprovisioningStatus,
    #[serde(skip_serializing_if = "Option::is_none")]
    credentials: Option<Credentials>,
}

impl ProvisioningResult {
    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn hub_name(&self) -> &str {
        &self.hub_name
    }

    pub fn reconfigure(&self) -> ReprovisioningStatus {
        self.reconfigure
    }

    pub fn credentials(&self) -> Option<&Credentials> {
        self.credentials.as_ref()
    }
}

pub trait Provision {
    type Hsm: Activate + KeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send>;
}

#[derive(Debug)]
pub struct ManualProvisioning {
    key: MemoryKey,
    device_id: String,
    hub: String,
}

impl ManualProvisioning {
    pub fn new(key: MemoryKey, device_id: String, hub: String) -> Self {
        ManualProvisioning {
            key,
            device_id,
            hub,
        }
    }
}

impl Provision for ManualProvisioning {
    type Hsm = MemoryKeyStore;

    fn provision(
        self,
        mut key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
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
                reconfigure: ReprovisioningStatus::DeviceDataNotUpdated,
                sha256_thumbprint: None,
                credentials: None,
            })
            .map_err(|err| Error::from(err.context(ErrorKind::Provision)));
        Box::new(result.into_future())
    }
}

pub struct ExternalProvisioning<T, U>
{
    client: T,

    // ExternalProvisioning is not restricted to a single HSM implementation, so it uses
    // PhantomData to be generic on them.
    phantom: PhantomData<U>,
}

impl<T, U> ExternalProvisioning<T, U>
{
    pub fn new(client: T) -> Self {
        ExternalProvisioning {
            client,
            phantom: PhantomData,
        }
    }
}

impl<T, U> Provision for ExternalProvisioning<T, U>
where
    T: 'static + ExternalProvisioningInterface,
    U: 'static + Activate + KeyStore + Send,
{
    type Hsm = U;

    fn provision(
        self,
        mut key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        let result = self
            .client
            .get_device_provisioning_information()
            .map_err(|err| Error::from(err.context(ErrorKind::Provision)))
            .and_then(move |device_provisioning_info| {
                info!(
                    "External device registration information: Device \"{}\" in hub \"{}\"",
                    device_provisioning_info.device_id(),
                    device_provisioning_info.hub_name()
                );

                let credentials = device_provisioning_info.credentials();


                // Passing a sentinel value as key because in the external mode, the external provisioning
                // environment itself creates and activates the actual key. The sentinel is
                // simply ignored.
                key_activator
                    .activate_identity_key(
                        KeyIdentity::Device,
                        "primary".to_string(),
                        &Bytes::from(EXTERNAL_PROVISIONING_KEY_SENTINEL),
                    )
                    .context(ErrorKind::Provision)?;

                Ok(ProvisioningResult {
                    device_id: device_provisioning_info.device_id().to_string(),
                    hub_name: device_provisioning_info.hub_name().to_string(),
                    reconfigure: ReprovisioningStatus::DeviceDataNotUpdated,
                    sha256_thumbprint: None,
                })
            });

        Box::new(result)
    }
}

pub struct DpsTpmProvisioning<C>
where
    C: ClientImpl,
{
    client: HttpClient<C, DpsTokenSource<TpmKey>>,
    scope_id: String,
    registration_id: String,
    hsm_tpm_ek: HsmTpmKey,
    hsm_tpm_srk: HsmTpmKey,
}

impl<C> DpsTpmProvisioning<C>
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

        let result = DpsTpmProvisioning {
            client,
            scope_id,
            registration_id,
            hsm_tpm_ek,
            hsm_tpm_srk,
        };
        Ok(result)
    }
}

impl<C> Provision for DpsTpmProvisioning<C>
where
    C: 'static + ClientImpl,
{
    type Hsm = TpmKeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        let ek = Bytes::from(self.hsm_tpm_ek.as_ref());
        let srk = Bytes::from(self.hsm_tpm_srk.as_ref());
        let c = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            DpsAuthKind::Tpm { ek, srk },
            key_activator,
        );

        let d = match c {
            Ok(c) => Either::A(
                c.register()
                    .map(|(device_id, hub_name, _substatus)| {
                        info!(
                            "DPS registration assigned device \"{}\" in hub \"{}\"",
                            device_id, hub_name
                        );
                        ProvisioningResult {
                            device_id,
                            hub_name,
                            // Each time DPS provisions with TPM, it gets back a new device key. This results in obsolete
                            // module keys in IoTHub from the previous provisioning. We delete all containers
                            // after each DPS provisioning run so that IoTHub can be updated with new module
                            // keys when the deployment is executed by EdgeAgent.
                            reconfigure: ReprovisioningStatus::InitialAssignment,
                            sha256_thumbprint: None,
                            credentials: None,
                        }
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::Provision))),
            ),
            Err(err) => Either::B(future::err(Error::from(err.context(ErrorKind::Provision)))),
        };

        Box::new(d)
    }
}

pub struct DpsSymmetricKeyProvisioning<C>
where
    C: ClientImpl,
{
    client: HttpClient<C, DpsTokenSource<MemoryKey>>,
    scope_id: String,
    registration_id: String,
}

impl<C> DpsSymmetricKeyProvisioning<C>
where
    C: ClientImpl,
{
    pub fn new(
        client_impl: C,
        endpoint: Url,
        scope_id: String,
        registration_id: String,
        api_version: String,
    ) -> Result<Self, Error> {
        let client = HttpClient::new(
            client_impl,
            None as Option<DpsTokenSource<MemoryKey>>,
            api_version,
            endpoint,
        )
        .context(ErrorKind::DpsInitialization)?;
        let result = DpsSymmetricKeyProvisioning {
            client,
            scope_id,
            registration_id,
        };
        Ok(result)
    }
}

impl<C> Provision for DpsSymmetricKeyProvisioning<C>
where
    C: 'static + ClientImpl,
{
    type Hsm = MemoryKeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        let c = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            DpsAuthKind::SymmetricKey,
            key_activator,
        );

        let d = match c {
            Ok(c) => Either::A(
                c.register()
                    .map(|(device_id, hub_name, substatus)| {
                        info!(
                            "DPS registration assigned device \"{}\" in hub \"{}\"",
                            device_id, hub_name
                        );
                        let reconfigure = substatus.map_or_else(
                            || ReprovisioningStatus::InitialAssignment,
                            |s| ReprovisioningStatus::from(s.as_ref()),
                        );
                        ProvisioningResult {
                            device_id,
                            hub_name,
                            reconfigure,
                            sha256_thumbprint: None,
                            credentials: None,
                        }
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::Provision))),
            ),
            Err(err) => Either::B(future::err(Error::from(err.context(ErrorKind::Provision)))),
        };
        Box::new(d)
    }
}

pub struct DpsX509Provisioning<C>
where
    C: ClientImpl,
{
    client: HttpClient<C, DpsTokenSource<MemoryKey>>,
    scope_id: String,
    registration_id: String,
}

impl<C> DpsX509Provisioning<C>
where
    C: ClientImpl,
{
    pub fn new(
        client_impl: C,
        endpoint: Url,
        scope_id: String,
        registration_id: String,
        api_version: String,
    ) -> Result<Self, Error> {
        let client = HttpClient::new(
            client_impl,
            None as Option<DpsTokenSource<MemoryKey>>,
            api_version,
            endpoint,
        )
        .context(ErrorKind::DpsInitialization)?;
        let result = DpsX509Provisioning {
            client,
            scope_id,
            registration_id,
        };
        Ok(result)
    }
}

impl<C> Provision for DpsX509Provisioning<C>
where
    C: 'static + ClientImpl,
{
    type Hsm = MemoryKeyStore;

    fn provision(
        self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        let c = DpsClient::new(
            self.client.clone(),
            self.scope_id.clone(),
            self.registration_id.clone(),
            DpsAuthKind::X509,
            key_activator,
        );

        let d = match c {
            Ok(c) => Either::A(
                c.register()
                    .map(|(device_id, hub_name, substatus)| {
                        info!(
                            "DPS registration assigned device \"{}\" in hub \"{}\"",
                            device_id, hub_name
                        );
                        let reconfigure = substatus.map_or_else(
                            || ReprovisioningStatus::InitialAssignment,
                            |s| ReprovisioningStatus::from(s.as_ref()),
                        );
                        // note DPS does not send the SHA2 thumbprint currently
                        // future DPS APIs will possibly support this
                        ProvisioningResult {
                            device_id,
                            hub_name,
                            reconfigure,
                            sha256_thumbprint: None,
                            credentials: None,
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

    fn diff_with_backup_inner(
        path: PathBuf,
        prov_result: &ProvisioningResult,
    ) -> Result<bool, serde_json::Error> {
        match Self::restore(path) {
            Ok(restored_prov_result) => {
                let buffer = serde_json::to_string(&restored_prov_result)?;
                let buffer = Sha256::digest_str(&buffer);
                let buffer = base64::encode(&buffer);

                let s = serde_json::to_string(prov_result)?;
                let s = Sha256::digest_str(&s);
                let encoded = base64::encode(&s);
                if encoded == buffer {
                    Ok(false)
                } else {
                    Ok(true)
                }
            }
            Err(err) => {
                log_failure(Level::Debug, &err);
                Ok(true)
            }
        }
    }

    fn diff_with_backup(path: PathBuf, prov_result: &ProvisioningResult) -> bool {
        match Self::diff_with_backup_inner(path, prov_result) {
            Ok(result) => result,
            Err(err) => {
                log_failure(Level::Debug, &err);
                true
            }
        }
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
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        let path = self.path.clone();
        let restore_path = self.path.clone();
        let path_on_err = self.path.clone();
        Box::new(
            self.underlying
                .provision(key_activator)
                .and_then(move |mut prov_result| {
                    debug!("Provisioning result {:?}", prov_result);
                    let reconfigure = match prov_result.reconfigure {
                        ReprovisioningStatus::DeviceDataUpdated => {
                            if Self::diff_with_backup(restore_path, &prov_result) {
                                info!("Provisioning credentials were changed.");
                                ReprovisioningStatus::InitialAssignment
                            } else {
                                info!("No changes to device reprovisioning.");
                                ReprovisioningStatus::DeviceDataNotUpdated
                            }
                        }
                        _ => ReprovisioningStatus::InitialAssignment,
                    };

                    prov_result.reconfigure = reconfigure;
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

    use edgelet_config::{Manual, ParseManualDeviceConnectionStringError};
    use external_provisioning::models::DeviceProvisioningInfo;
    use failure::Fail;
    use std::fmt::{self, Display};
    use tempdir::TempDir;
    use tokio;

    use crate::error::ErrorKind;

    struct TestProvisioning {}

    impl Provision for TestProvisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
            Box::new(future::ok(ProvisioningResult {
                device_id: "TestDevice".to_string(),
                hub_name: "TestHub".to_string(),
                reconfigure: ReprovisioningStatus::DeviceDataUpdated,
                sha256_thumbprint: None,
                credentials: None,
            }))
        }
    }

    struct TestReprovisioning {}

    impl Provision for TestReprovisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
            Box::new(future::ok(ProvisioningResult {
                device_id: "TestDevice".to_string(),
                hub_name: "TestHubUpdated".to_string(),
                reconfigure: ReprovisioningStatus::DeviceDataUpdated,
                sha256_thumbprint: None,
                credentials: None,
            }))
        }
    }

    struct TestProvisioningWithError {}

    impl Provision for TestProvisioningWithError {
        type Hsm = MemoryKeyStore;

        fn provision(
            self,
            _key_activator: Self::Hsm,
        ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
            Box::new(future::err(Error::from(ErrorKind::Provision)))
        }
    }

    fn parse_connection_string(
        s: &str,
    ) -> Result<ManualProvisioning, ParseManualDeviceConnectionStringError> {
        let (key, device_id, hub) = Manual::new(s.to_string()).parse_device_connection_string()?;
        Ok(ManualProvisioning::new(key, device_id, hub))
    }

    #[test]
    fn manual_get_credentials_success() {
        let provisioning =
            parse_connection_string("HostName=test.com;DeviceId=test;SharedAccessKey=test")
                .unwrap();
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
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
        let test = parse_connection_string("HostName=test.com;DeviceId=test;");
        assert!(test.is_err());
    }

    #[test]
    fn connection_string_split_success() {
        let provisioning =
            parse_connection_string("HostName=test.com;DeviceId=test;SharedAccessKey=test")
                .unwrap();
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm.clone()).then(|result| {
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
            parse_connection_string("DeviceId=test;SharedAccessKey=test;HostName=test.com")
                .unwrap();
        let task1 = provisioning1.provision(memory_hsm.clone()).then(|result| {
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
        let test1 = parse_connection_string("DeviceId=test;SharedAccessKey=test");
        assert!(test1.is_err());

        let test2 = parse_connection_string(
            "HostName=test.com;Extra=something;DeviceId=test;SharedAccessKey=test",
        )
        .unwrap();
        let memory_hsm = MemoryKeyStore::new();
        let task1 = test2.provision(memory_hsm.clone()).then(|result| {
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
    fn device_updated_no_change() {
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

        let prov_wrapper_err = BackupProvisioning::new(TestProvisioning {}, file_path_clone);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                let prov_result = result.expect("Unexpected");
                assert_eq!(prov_result.device_id(), "TestDevice");
                assert_eq!(prov_result.hub_name(), "TestHub");
                assert_eq!(
                    prov_result.reconfigure(),
                    ReprovisioningStatus::DeviceDataNotUpdated
                );
                Ok::<_, Error>(())
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task1)
            .unwrap();
    }

    #[test]
    fn device_updated_credential_change() {
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

        let prov_wrapper_err = BackupProvisioning::new(TestReprovisioning {}, file_path_clone);
        let task1 = prov_wrapper_err
            .provision(MemoryKeyStore::new())
            .then(|result| {
                let prov_result = result.expect("Unexpected");
                assert_eq!(prov_result.device_id(), "TestDevice");
                assert_eq!(prov_result.hub_name(), "TestHubUpdated");
                assert_eq!(
                    prov_result.reconfigure(),
                    ReprovisioningStatus::InitialAssignment
                );
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
            reconfigure: ReprovisioningStatus::DeviceDataNotUpdated,
            sha256_thumbprint: None,
            credentials: None,
        })
        .unwrap();
        assert_eq!(
            "{\"device_id\":\"something\",\"hub_name\":\"something\"}",
            json
        );
        let result: ProvisioningResult = serde_json::from_str(&json).unwrap();
        assert_eq!(result.reconfigure, ReprovisioningStatus::InitialAssignment)
    }

    struct TestExternalProvisioningInterface {
        pub error: Option<TestError>,
    }

    #[derive(Debug, Fail)]
    struct TestError {}

    impl Display for TestError {
        fn fmt(&self, f: &mut fmt::Formatter<'_>) -> std::fmt::Result {
            write!(f, "test error")
        }
    }

    impl ExternalProvisioningInterface for TestExternalProvisioningInterface {
        type Error = TestError;

        type DeviceProvisioningInformationFuture =
            Box<dyn Future<Item = DeviceProvisioningInfo, Error = Self::Error> + Send>;

        fn get_device_provisioning_information(&self) -> Self::DeviceProvisioningInformationFuture {
            match self.error.as_ref() {
                None => Box::new(
                    Ok(DeviceProvisioningInfo::new(
                        "TestHub".to_string(),
                        "TestDevice".to_string(),
                    ))
                    .into_future(),
                ),
                Some(_s) => Box::new(Err(TestError {}).into_future()),
            }
        }
    }

    #[test]
    fn external_get_provisioning_info_success() {
        let provisioning =
            ExternalProvisioning::new(TestExternalProvisioningInterface { error: None });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm.clone())
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name, "TestHub".to_string());
                    assert_eq!(result.device_id, "TestDevice".to_string());
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
    fn external_get_provisioning_info_failure() {
        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: Some(TestError {}),
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm.clone())
            .then(|result| match result {
                Ok(_) => panic!("Expected a failure."),
                Err(err) => match err.kind() {
                    ErrorKind::Provision => Ok::<_, Error>(()),
                    _ => panic!("Expected `Provision` but got {:?}", err),
                },
            });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }
}
