// Copyright (c) Microsoft. All rights reserved.

use std::fs::File;
use std::io::{Read, Write};
use std::marker::PhantomData;
use std::path::PathBuf;
use std::str::FromStr;

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
use edgelet_core::ProvisioningResult as CoreProvisioningResult;
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http_external_provisioning::ExternalProvisioningInterface;
use edgelet_utils::log_failure;
use external_provisioning::models::Credentials as ExternalProvisioningCredentials;
use hsm::TpmKey as HsmTpmKey;
use log::{debug, Level};
use sha2::{Digest, Sha256};

use crate::error::{Error, ErrorKind, ExternalProvisioningErrorReason};

#[derive(Clone, Copy, Serialize, Deserialize, Debug, PartialEq)]
pub enum ProvisioningStatus {
    Assigned,
    Assigning,
    Disabled,
    Failed,
    Unassigned,
}

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
    key: Option<Vec<u8>>,
}

impl SymmetricKeyCredential {
    pub fn new(key: Vec<u8>) -> Self {
        SymmetricKeyCredential { key: Some(key) }
    }

    pub fn key(&self) -> Option<&[u8]> {
        self.key.as_ref().map(AsRef::as_ref)
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct X509Credential {
    identity_cert: String,
    identity_private_key: String,
}

impl X509Credential {
    pub fn new(identity_cert: String, identity_private_key: String) -> Self {
        X509Credential {
            identity_cert,
            identity_private_key,
        }
    }

    pub fn identity_cert(&self) -> &str {
        self.identity_cert.as_str()
    }

    pub fn identity_private_key(&self) -> &str {
        self.identity_private_key.as_str()
    }
}

#[derive(Clone, Serialize, Deserialize, Debug)]
pub enum AuthType {
    SymmetricKey(SymmetricKeyCredential),
    X509(X509Credential),
}

#[derive(Clone, Copy, Serialize, Deserialize, Debug, PartialEq)]
pub enum CredentialSource {
    Payload,
    Hsm,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Credentials {
    auth_type: AuthType,
    source: CredentialSource,
}

impl Credentials {
    pub fn new(auth_type: AuthType, source: CredentialSource) -> Self {
        Credentials { auth_type, source }
    }

    pub fn auth_type(&self) -> &AuthType {
        &self.auth_type
    }

    pub fn source(&self) -> &CredentialSource {
        &self.source
    }
}

impl From<&str> for ReprovisioningStatus {
    fn from(s: &str) -> ReprovisioningStatus {
        // TODO: check with DPS substatus value for DeviceDataUpdated when it is implemented on service side
        match s {
            "deviceDataMigrated" => ReprovisioningStatus::DeviceDataMigrated,
            "deviceDataReset" => ReprovisioningStatus::DeviceDataReset,
            "initialAssignment" => ReprovisioningStatus::InitialAssignment,
            _ => {
                debug!("Provisioning result substatus {}", s);
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

impl FromStr for ProvisioningStatus {
    type Err = Error;

    fn from_str(s: &str) -> Result<ProvisioningStatus, Self::Err> {
        match s {
            "assigned" => Ok(ProvisioningStatus::Assigned),
            "assigning" => Ok(ProvisioningStatus::Assigning),
            "disabled" => Ok(ProvisioningStatus::Disabled),
            "failed" => Ok(ProvisioningStatus::Failed),
            "unassigned" => Ok(ProvisioningStatus::Unassigned),
            _ => Err(Error::from(ErrorKind::InvalidProvisioningStatus)),
        }
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
    pub fn new(
        device_id: &str,
        hub_name: &str,
        sha256_thumbprint: Option<&str>,
        reconfigure: ReprovisioningStatus,
        credentials: Option<Credentials>,
    ) -> Self {
        ProvisioningResult {
            device_id: device_id.to_owned(),
            hub_name: hub_name.to_owned(),
            sha256_thumbprint: sha256_thumbprint.map(&str::to_owned),
            reconfigure,
            credentials,
        }
    }

    pub fn reconfigure(&self) -> ReprovisioningStatus {
        self.reconfigure
    }

    pub fn credentials(&self) -> Option<&Credentials> {
        self.credentials.as_ref()
    }
}

impl CoreProvisioningResult for ProvisioningResult {
    fn device_id(&self) -> &str {
        &self.device_id
    }

    fn hub_name(&self) -> &str {
        &self.hub_name
    }
}

pub trait Provision {
    type Hsm: Activate + KeyStore;

    fn provision(
        &self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send>;

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send>;
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
        &self,
        mut key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        info!(
            "Manually provisioning device \"{}\" in hub \"{}\"",
            self.device_id, self.hub
        );
        let result = key_activator
            .activate_identity_key(KeyIdentity::Device, "primary".to_string(), self.key.clone())
            .map(|_| ProvisioningResult {
                device_id: self.device_id.to_string(),
                hub_name: self.hub.to_string(),
                reconfigure: ReprovisioningStatus::DeviceDataNotUpdated,
                sha256_thumbprint: None,
                credentials: None,
            })
            .map_err(|err| Error::from(err.context(ErrorKind::Provision)));
        Box::new(result.into_future())
    }

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        // No reprovision action is needed for the manual provisioning mode.
        Box::new(future::ok(()))
    }
}

pub struct ExternalProvisioning<T, U> {
    client: T,

    // ExternalProvisioning is not restricted to a single HSM implementation, so it uses
    // PhantomData to be generic on them.
    phantom: PhantomData<U>,
}

impl<T, U> ExternalProvisioning<T, U> {
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
        &self,
        key_activator: Self::Hsm,
    ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
        fn provision_symmetric_key<H>(
            external_provisioning_credentials: &ExternalProvisioningCredentials,
            mut key_activator: H,
        ) -> Result<Credentials, Error>
        where
            H: Activate + KeyStore,
        {
            match external_provisioning_credentials.source() {
                "payload" => {
                    external_provisioning_credentials.key().map_or_else(
                        || {
                            info!(
                                "A key is expected in the response with the 'symmetric-key' authentication type and the 'source' set to 'payload'.");
                            Err(Error::from(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::SymmetricKeyNotSpecified)))
                        },
                        |key| {
                            let decoded_key = base64::decode(&key).map_err(|_| {
                                Error::from(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::InvalidSymmetricKey))
                            })?;

                            key_activator
                                .activate_identity_key(KeyIdentity::Device, "primary".to_string(), &decoded_key)
                                .map_err(|err| Error::from(err.context(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::KeyActivation))))?;
                            Ok(Credentials {
                                auth_type: AuthType::SymmetricKey(
                                    SymmetricKeyCredential {
                                        key: Some(decoded_key),
                                    }),
                                source: CredentialSource::Payload,
                            })
                        })
                },
                "hsm" => Ok(Credentials {
                    auth_type: AuthType::SymmetricKey(
                        SymmetricKeyCredential {
                            key: None,
                        }),
                    source: CredentialSource::Hsm,
                }),
                _ => {
                    info!(
                        "Unexpected value of credential source \"{}\" received from external environment.",
                        external_provisioning_credentials.source()
                    );
                    Err(Error::from(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::InvalidCredentialSource)))
                }
            }
        }

        fn provision_x509(
            external_provisioning_credentials: &ExternalProvisioningCredentials,
        ) -> Result<Credentials, Error> {
            match external_provisioning_credentials.source() {
                "payload" => {
                    let identity_cert = external_provisioning_credentials
                        .identity_cert()
                        .ok_or_else(|| {
                            Error::from(ErrorKind::ExternalProvisioning(
                                ExternalProvisioningErrorReason::IdentityCertificateNotSpecified,
                            ))
                        })?;

                    let identity_private_key = external_provisioning_credentials
                        .identity_private_key()
                        .ok_or_else(|| {
                            Error::from(ErrorKind::ExternalProvisioning(
                                ExternalProvisioningErrorReason::IdentityPrivateKeyNotSpecified,
                            ))
                        })?;

                    Ok(Credentials {
                        auth_type: AuthType::X509(X509Credential {
                            identity_cert: identity_cert.to_string(),
                            identity_private_key: identity_private_key.to_string(),
                        }),
                        source: CredentialSource::Payload,
                    })
                }
                "hsm" => Ok(Credentials {
                    auth_type: AuthType::X509(X509Credential {
                        identity_cert: external_provisioning_credentials
                            .identity_cert()
                            .unwrap_or("")
                            .to_string(),
                        identity_private_key: external_provisioning_credentials
                            .identity_private_key()
                            .unwrap_or("")
                            .to_string(),
                    }),
                    source: CredentialSource::Hsm,
                }),
                _ => {
                    info!(
                        "Unexpected value of credential source \"{}\" received from external environment.",
                        external_provisioning_credentials.source()
                    );
                    Err(Error::from(ErrorKind::ExternalProvisioning(
                        ExternalProvisioningErrorReason::InvalidCredentialSource,
                    )))
                }
            }
        }

        let result = self
            .client
            .get_device_provisioning_information()
            .map_err(|err| Error::from(err.context(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::ProvisioningFailure))))
            .and_then(move |device_provisioning_info| {
                let provisioning_status = device_provisioning_info.status().map_or_else(
                    || Ok(ProvisioningStatus::Assigned),
                    |p| ProvisioningStatus::from_str(p),
                )?;
                let reconfigure = device_provisioning_info.substatus().map_or_else(
                    || ReprovisioningStatus::InitialAssignment,
                    ReprovisioningStatus::from,
                );
                info!(
                    "External device registration information: Device \"{}\" in hub \"{}\" with credential type \"{}\" and credential source \"{}\". Current status is \"{:?}\" with substatus \"{:?}\".",
                    device_provisioning_info.device_id(),
                    device_provisioning_info.hub_name(),
                    device_provisioning_info.credentials().auth_type(),
                    device_provisioning_info.credentials().source(),
                    provisioning_status,
                    reconfigure,
                );

                let credentials_info = device_provisioning_info.credentials();
                let credentials = match credentials_info.auth_type() {
                    "symmetric-key" => {
                        provision_symmetric_key(credentials_info, key_activator)
                    },
                    "x509" => {
                        provision_x509(credentials_info)
                    },
                    _ => {
                        info!(
                            "Unexpected value of authentication type \"{}\" received from external environment.",
                            credentials_info.source()
                        );
                        Err(Error::from(ErrorKind::ExternalProvisioning(ExternalProvisioningErrorReason::InvalidAuthenticationType)))
                    }
                }?;

                Ok(ProvisioningResult {
                    device_id: device_provisioning_info.device_id().to_string(),
                    hub_name: device_provisioning_info.hub_name().to_string(),
                    reconfigure,
                    sha256_thumbprint: None,
                    credentials: Some(credentials)
                })
            });

        Box::new(result)
    }

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        let result = self
            .client
            .reprovision_device()
            .map_err(|err| {
                Error::from(err.context(ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::ReprovisioningFailure,
                )))
            })
            .and_then(move |_| {
                info!("Reprovision device notification sent to external endpoint.");
                Ok(())
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
        &self,
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

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        // No reprovision action is needed for the DPS provisioning mode.
        Box::new(future::ok(()))
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
        &self,
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

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        // No reprovision action is needed for the DPS provisioning mode.
        Box::new(future::ok(()))
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
        &self,
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

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        // No reprovision action is needed for the DPS provisioning mode.
        Box::new(future::ok(()))
    }
}

pub struct BackupProvisioning<'a, P> {
    underlying: &'a P,
    path: PathBuf,
}

impl<'a, P: 'a> BackupProvisioning<'a, P> {
    pub fn new(provisioner: &'a P, path: PathBuf) -> Self {
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
        let mut prov_result: ProvisioningResult =
            serde_json::from_str(&buffer).context(ErrorKind::CouldNotRestore)?;
        prov_result.reconfigure = ReprovisioningStatus::DeviceDataNotUpdated;
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

impl<'a, P: 'a> Provision for BackupProvisioning<'a, P>
where
    P: Provision,
{
    type Hsm = P::Hsm;

    fn provision(
        &self,
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

    fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
        panic!("A reprovisioning operation is not expected for `BackupProvisioning`")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use edgelet_core::{Error as CoreError, ManualDeviceConnectionString};
    use external_provisioning::models::{Credentials, DeviceProvisioningInfo};
    use failure::Fail;
    use std::fmt::{self, Display};
    use tempdir::TempDir;
    use tokio;

    use crate::error::ErrorKind;

    struct TestProvisioning {}

    impl Provision for TestProvisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            &self,
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

        fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
            Box::new(future::ok(()))
        }
    }

    struct TestReprovisioning {}

    impl Provision for TestReprovisioning {
        type Hsm = MemoryKeyStore;

        fn provision(
            &self,
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

        fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
            Box::new(future::ok(()))
        }
    }

    struct TestProvisioningWithError {}

    impl Provision for TestProvisioningWithError {
        type Hsm = MemoryKeyStore;

        fn provision(
            &self,
            _key_activator: Self::Hsm,
        ) -> Box<dyn Future<Item = ProvisioningResult, Error = Error> + Send> {
            Box::new(future::err(Error::from(ErrorKind::Provision)))
        }

        fn reprovision(&self) -> Box<dyn Future<Item = (), Error = Error> + Send> {
            Box::new(future::err(Error::from(ErrorKind::Reprovision)))
        }
    }

    fn parse_connection_string(s: &str) -> Result<ManualProvisioning, CoreError> {
        let (key, device_id, hub) =
            ManualDeviceConnectionString::new(s.to_string()).parse_device_connection_string()?;
        Ok(ManualProvisioning::new(key, device_id, hub))
    }

    #[test]
    fn manual_get_credentials_success() {
        let provisioning =
            parse_connection_string("HostName=test.com;DeviceId=test;SharedAccessKey=test")
                .unwrap();
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
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
        let task1 = provisioning1.provision(memory_hsm).then(|result| {
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
        let task1 = test2.provision(memory_hsm).then(|result| {
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
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
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
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(&TestProvisioningWithError {}, file_path_clone);
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
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err = BackupProvisioning::new(&TestProvisioning {}, file_path_clone);
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
    fn provisioning_restore_no_reconfigure() {
        let test_provisioner = TestProvisioning {};
        let tmp_dir = TempDir::new("backup").unwrap();
        let file_path = tmp_dir.path().join("dps_backup.json");
        let file_path_clone = file_path.clone();
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(&TestProvisioningWithError {}, file_path_clone);
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
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err = BackupProvisioning::new(&TestReprovisioning {}, file_path_clone);
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
        let prov_wrapper = BackupProvisioning::new(&test_provisioner, file_path);
        let task = prov_wrapper.provision(MemoryKeyStore::new());
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();

        let prov_wrapper_err =
            BackupProvisioning::new(&TestProvisioningWithError {}, file_path_wrong);
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
        pub provisioning_info: DeviceProvisioningInfo,
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

        type ReprovisionDeviceFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

        fn get_device_provisioning_information(&self) -> Self::DeviceProvisioningInformationFuture {
            match self.error.as_ref() {
                None => Box::new(Ok(self.provisioning_info.clone()).into_future()),
                Some(_s) => Box::new(Err(TestError {}).into_future()),
            }
        }

        fn reprovision_device(&self) -> Self::ReprovisionDeviceFuture {
            match self.error.as_ref() {
                None => Box::new(Ok(()).into_future()),
                Some(_s) => Box::new(Err(TestError {}).into_future()),
            }
        }
    }

    #[test]
    fn external_get_provisioning_info_symmetric_key_payload_success() {
        let mut credentials = Credentials::new("symmetric-key".to_string(), "payload".to_string());
        credentials.set_key("cGFzczEyMzQ=".to_string());
        let mut provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        provisioning_info.set_status("assigned".to_string());
        provisioning_info.set_substatus("initialAssignment".to_string());

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name, "TestHub".to_string());
                    assert_eq!(result.device_id, "TestDevice".to_string());

                    if let Some(credentials) = result.credentials() {
                        if let AuthType::SymmetricKey(symmetric_key) = credentials.auth_type() {
                            if let Some(key) = &symmetric_key.key {
                                assert_eq!(base64::encode(key), "cGFzczEyMzQ=");
                            } else {
                                panic!("A key was expected in the response.")
                            }
                        } else {
                            panic!("Unexpected authentication type.")
                        }
                    } else {
                        panic!("No credentials found. This is unexpected")
                    }

                    assert_eq!(ReprovisioningStatus::InitialAssignment, result.reconfigure);

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
    fn external_get_provisioning_info_symmetric_key_hsm_success() {
        let credentials = Credentials::new("symmetric-key".to_string(), "hsm".to_string());
        let mut provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        provisioning_info.set_status("assigned".to_string());
        provisioning_info.set_substatus("garbage".to_string()); // testing if unsupported value for status resolves to a default instead.

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name, "TestHub".to_string());
                    assert_eq!(result.device_id, "TestDevice".to_string());

                    if let Some(credentials) = result.credentials() {
                        if let AuthType::SymmetricKey(symmetric_key) = credentials.auth_type() {
                            if symmetric_key.key.is_some() {
                                panic!("No key was expected in the response.")
                            }
                        } else {
                            panic!("Unexpected authentication type.")
                        }
                    } else {
                        panic!("No credentials found. This is unexpected")
                    }

                    assert_eq!(ReprovisioningStatus::InitialAssignment, result.reconfigure);

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
    fn external_get_provisioning_info_invalid_provisioning_status_failure() {
        let credentials = Credentials::new("symmetric-key".to_string(), "hsm".to_string());
        let mut provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        provisioning_info.set_status("garbage".to_string()); // testing if unsupported value for status resolves to a default instead.
        provisioning_info.set_substatus("initialAssignment".to_string()); // testing if unsupported value for status resolves to a default instead.

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::InvalidProvisioningStatus
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_invalid_credentials_source() {
        let credentials = Credentials::new("symmetric-key".to_string(), "xyz".to_string());
        let provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::InvalidCredentialSource
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_invalid_authentication_type() {
        let credentials = Credentials::new("xyz".to_string(), "payload".to_string());
        let provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::InvalidAuthenticationType
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_failure() {
        let credentials = Credentials::new("symmetric-key".to_string(), "payload".to_string());
        let provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: Some(TestError {}),
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::ProvisioningFailure
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_x509_payload_success() {
        let mut credentials = Credentials::new("x509".to_string(), "payload".to_string());

        let hub_name = "TestHub";
        let device_id = "TestDevice";
        let identity_cert_val = "cGFzczEyMzQ=";
        let identity_private_key_val = "SGVsbG8=";

        credentials.set_identity_cert(identity_cert_val.to_string());
        credentials.set_identity_private_key(identity_private_key_val.to_string());

        let mut provisioning_info =
            DeviceProvisioningInfo::new(hub_name.to_string(), device_id.to_string(), credentials);

        provisioning_info.set_status("assigned".to_string());
        provisioning_info.set_substatus("deviceDataMigrated".to_string());

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name.as_str(), hub_name);
                    assert_eq!(result.device_id.as_str(), device_id);

                    if let Some(credentials) = result.credentials() {
                        assert_eq!(credentials.source(), &CredentialSource::Payload);

                        if let AuthType::X509(x509) = credentials.auth_type() {
                            assert_eq!(x509.identity_cert(), identity_cert_val);
                            assert_eq!(x509.identity_private_key(), identity_private_key_val);
                        } else {
                            panic!("Unexpected authentication type.")
                        }
                    } else {
                        panic!("No credentials found. This is unexpected")
                    }

                    assert_eq!(ReprovisioningStatus::DeviceDataMigrated, result.reconfigure);

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
    fn external_get_provisioning_info_x509_payload_no_identity_cert_failure() {
        let mut credentials = Credentials::new("x509".to_string(), "payload".to_string());

        let hub_name = "TestHub";
        let device_id = "TestDevice";
        let identity_private_key_val = "SGVsbG8=";

        credentials.set_identity_private_key(identity_private_key_val.to_string());

        let provisioning_info =
            DeviceProvisioningInfo::new(hub_name.to_string(), device_id.to_string(), credentials);

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::IdentityCertificateNotSpecified
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_x509_payload_no_identity_private_key_failure() {
        let mut credentials = Credentials::new("x509".to_string(), "payload".to_string());

        let hub_name = "TestHub";
        let device_id = "TestDevice";
        let identity_cert_val = "cGFzczEyMzQ=";

        credentials.set_identity_cert(identity_cert_val.to_string());

        let provisioning_info =
            DeviceProvisioningInfo::new(hub_name.to_string(), device_id.to_string(), credentials);

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning.provision(memory_hsm).then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::IdentityPrivateKeyNotSpecified
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_get_provisioning_info_x509_hsm_identity_specified_success() {
        let mut credentials = Credentials::new("x509".to_string(), "hsm".to_string());

        let hub_name = "TestHub";
        let device_id = "TestDevice";
        let identity_cert_val = "/certs/identity_cert.pem";
        let identity_private_key_val = "/certs/identity_private_key.pem";

        credentials.set_identity_cert(identity_cert_val.to_string());
        credentials.set_identity_private_key(identity_private_key_val.to_string());

        let provisioning_info =
            DeviceProvisioningInfo::new(hub_name.to_string(), device_id.to_string(), credentials);

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name, hub_name);
                    assert_eq!(result.device_id, device_id);

                    if let Some(credentials) = result.credentials() {
                        assert_eq!(credentials.source(), &CredentialSource::Hsm);

                        if let AuthType::X509(x509) = credentials.auth_type() {
                            assert_eq!(x509.identity_cert(), identity_cert_val);
                            assert_eq!(x509.identity_private_key(), identity_private_key_val);
                        } else {
                            panic!("Unexpected authentication type.")
                        }
                    } else {
                        panic!("No credentials found. This is unexpected")
                    }

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
    fn external_get_provisioning_info_x509_hsm_identity_not_specified_success() {
        let credentials = Credentials::new("x509".to_string(), "hsm".to_string());

        let hub_name = "TestHub";
        let device_id = "TestDevice";

        let provisioning_info =
            DeviceProvisioningInfo::new(hub_name.to_string(), device_id.to_string(), credentials);

        let provisioning = ExternalProvisioning::new(TestExternalProvisioningInterface {
            error: None,
            provisioning_info,
        });
        let memory_hsm = MemoryKeyStore::new();
        let task = provisioning
            .provision(memory_hsm)
            .then(|result| match result {
                Ok(result) => {
                    assert_eq!(result.hub_name.as_str(), hub_name);
                    assert_eq!(result.device_id.as_str(), device_id);

                    if let Some(credentials) = result.credentials() {
                        assert_eq!(credentials.source(), &CredentialSource::Hsm);

                        if let AuthType::X509(x509) = credentials.auth_type() {
                            assert_eq!(x509.identity_cert(), "");
                            assert_eq!(x509.identity_private_key(), "");
                        } else {
                            panic!("Unexpected authentication type.")
                        }
                    } else {
                        panic!("No credentials found. This is unexpected")
                    }

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
    fn external_reprovision_device_success() {
        let mut credentials = Credentials::new("symmetric-key".to_string(), "payload".to_string());
        credentials.set_key("cGFzczEyMzQ=".to_string());
        let provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        let provisioning: ExternalProvisioning<_, MemoryKeyStore> =
            ExternalProvisioning::new(TestExternalProvisioningInterface {
                error: None,
                provisioning_info,
            });

        let task = provisioning.reprovision();
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn external_reprovision_device_failure() {
        let mut credentials = Credentials::new("symmetric-key".to_string(), "payload".to_string());
        credentials.set_key("cGFzczEyMzQ=".to_string());
        let provisioning_info = DeviceProvisioningInfo::new(
            "TestHub".to_string(),
            "TestDevice".to_string(),
            credentials,
        );

        let provisioning: ExternalProvisioning<_, MemoryKeyStore> =
            ExternalProvisioning::new(TestExternalProvisioningInterface {
                error: Some(TestError {}),
                provisioning_info,
            });

        let task = provisioning.reprovision().then(|result| {
            assert_eq!(
                result.unwrap_err().kind(),
                &ErrorKind::ExternalProvisioning(
                    ExternalProvisioningErrorReason::ReprovisioningFailure
                )
            );
            Ok::<_, Error>(())
        });
        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }
}
