use std::{marker::PhantomData, path::Path};

use mqtt_util::client_io::Credentials;
use tracing::debug;

use crate::{
    bridge::{Bridge, BridgeError},
    client::MqttClientConfig,
    persist::{waking_state::ring_buffer::RingBuffer, PublicationStore, WakingMemoryStore},
    pump::Builder,
    settings::{ConnectionSettings, StorageSettings},
};

pub(crate) struct Yes;
pub(crate) struct No;

pub(crate) struct UpstreamBridgeBuilder<
    Kind,
    SystemAddressPresent,
    DeviceIdPresent,
    ConnectionSettingsPresent,
    StorageSettingsPresent,
> {
    bridge_name: String,
    system_address: String,
    device_id: String,
    connection_settings: Option<ConnectionSettings>,
    storage_settings: Option<StorageSettings>,
    // phantoms
    _kind: PhantomData<Kind>,
    _system_address_present: PhantomData<SystemAddressPresent>,
    _device_id_present: PhantomData<DeviceIdPresent>,
    _connection_settings_present: PhantomData<ConnectionSettingsPresent>,
    _storage_settings_present: PhantomData<StorageSettingsPresent>,
}

impl<
        Kind,
        SystemAddressPresent,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    > Default
    for UpstreamBridgeBuilder<
        Kind,
        SystemAddressPresent,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    >
{
    fn default() -> Self {
        Self {
            bridge_name: String::default(),
            system_address: String::default(),
            device_id: String::default(),
            connection_settings: None,
            storage_settings: None,
            // phantoms
            _kind: PhantomData,
            _system_address_present: PhantomData,
            _device_id_present: PhantomData,
            _connection_settings_present: PhantomData,
            _storage_settings_present: PhantomData,
        }
    }
}

pub(crate) fn bridge_builder<Kind>() -> UpstreamBridgeBuilder<Kind, No, No, No, No> {
    UpstreamBridgeBuilder::default()
}

impl<Kind, DeviceIdPresent, ConnectionSettingsPresent, StorageSettingsPresent>
    UpstreamBridgeBuilder<
        Kind,
        No,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    >
{
    pub(crate) fn with_system_address(
        self,
        system_address: String,
    ) -> UpstreamBridgeBuilder<
        Kind,
        Yes,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    > {
        UpstreamBridgeBuilder {
            bridge_name: self.bridge_name,
            system_address,
            device_id: self.device_id,
            connection_settings: self.connection_settings,
            storage_settings: self.storage_settings,
            // phantoms
            _kind: PhantomData,
            _system_address_present: PhantomData,
            _device_id_present: PhantomData,
            _connection_settings_present: PhantomData,
            _storage_settings_present: PhantomData,
        }
    }
}

impl<Kind, SystemAddressPresent, ConnectionSettingsPresent, StorageSettingsPresent>
    UpstreamBridgeBuilder<
        Kind,
        SystemAddressPresent,
        No,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    >
{
    pub(crate) fn with_device_id(
        self,
        device_id: String,
    ) -> UpstreamBridgeBuilder<
        Kind,
        SystemAddressPresent,
        Yes,
        ConnectionSettingsPresent,
        StorageSettingsPresent,
    > {
        UpstreamBridgeBuilder {
            bridge_name: self.bridge_name,
            system_address: self.system_address,
            device_id,
            connection_settings: self.connection_settings,
            storage_settings: self.storage_settings,
            // phantoms
            _kind: PhantomData,
            _system_address_present: PhantomData,
            _device_id_present: PhantomData,
            _connection_settings_present: PhantomData,
            _storage_settings_present: PhantomData,
        }
    }
}

impl<Kind, SystemAddressPresent, DeviceIdPresent, StorageSettingsPresent>
    UpstreamBridgeBuilder<Kind, SystemAddressPresent, DeviceIdPresent, No, StorageSettingsPresent>
{
    pub(crate) fn with_connection_settings(
        self,
        connection_settings: ConnectionSettings,
    ) -> UpstreamBridgeBuilder<
        Kind,
        SystemAddressPresent,
        DeviceIdPresent,
        Yes,
        StorageSettingsPresent,
    > {
        UpstreamBridgeBuilder {
            bridge_name: self.bridge_name,
            system_address: self.system_address,
            device_id: self.device_id,
            connection_settings: Some(connection_settings),
            storage_settings: self.storage_settings,
            // phantoms
            _kind: PhantomData,
            _system_address_present: PhantomData,
            _device_id_present: PhantomData,
            _connection_settings_present: PhantomData,
            _storage_settings_present: PhantomData,
        }
    }
}

impl<SystemAddressPresent, DeviceIdPresent, ConnectionSettingsPresent>
    UpstreamBridgeBuilder<
        RingBuffer,
        SystemAddressPresent,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        No,
    >
{
    pub(crate) fn with_storage_settings(
        self,
        storage_settings: StorageSettings,
    ) -> UpstreamBridgeBuilder<
        RingBuffer,
        SystemAddressPresent,
        DeviceIdPresent,
        ConnectionSettingsPresent,
        Yes,
    > {
        UpstreamBridgeBuilder {
            bridge_name: self.bridge_name,
            system_address: self.system_address,
            device_id: self.device_id,
            connection_settings: self.connection_settings,
            storage_settings: Some(storage_settings),
            // phantoms
            _kind: PhantomData,
            _system_address_present: PhantomData,
            _device_id_present: PhantomData,
            _connection_settings_present: PhantomData,
            _storage_settings_present: PhantomData,
        }
    }
}

impl UpstreamBridgeBuilder<WakingMemoryStore, Yes, Yes, Yes, No> {
    pub fn build(self) -> Result<Bridge<WakingMemoryStore>, BridgeError> {
        let connection_settings = self.connection_settings.unwrap();
        let system_address = self.system_address;
        let device_id = self.device_id;
        debug!("creating bridge {}...", connection_settings.name());

        let (local_pump, remote_pump) = Builder::<WakingMemoryStore>::default()
            .with_local(|pump| {
                pump.with_config(MqttClientConfig::new(
                    system_address.clone(),
                    connection_settings.keep_alive(),
                    connection_settings.clean_session(),
                    Credentials::Anonymous(format!(
                        "{}/{}/$bridge",
                        device_id,
                        connection_settings.name()
                    )),
                ))
                .with_rules(connection_settings.forwards());
            })
            .with_remote(|pump| {
                pump.with_config(MqttClientConfig::new(
                    connection_settings.address(),
                    connection_settings.keep_alive(),
                    connection_settings.clean_session(),
                    connection_settings.credentials().clone(),
                ))
                .with_rules(connection_settings.subscriptions());
            })
            .with_store(|_suffix| {
                const BATCH_SIZE: usize = 10;
                PublicationStore::new_memory(BATCH_SIZE)
            })
            .build()?;

        debug!("created bridge {}...", connection_settings.name());

        Ok(Bridge {
            local_pump,
            remote_pump,
        })
    }
}

impl UpstreamBridgeBuilder<RingBuffer, Yes, Yes, Yes, Yes> {
    pub fn build(self) -> Result<Bridge<RingBuffer>, BridgeError> {
        let connection_settings = self.connection_settings.unwrap();
        let storage_settings = self.storage_settings.unwrap();
        let system_address = self.system_address;
        let device_id = self.device_id;
        debug!("creating bridge {}...", connection_settings.name());

        let (local_pump, remote_pump) = Builder::<RingBuffer>::default()
            .with_local(|pump| {
                pump.with_config(MqttClientConfig::new(
                    system_address.clone(),
                    connection_settings.keep_alive(),
                    connection_settings.clean_session(),
                    Credentials::Anonymous(format!(
                        "{}/{}/$bridge",
                        device_id,
                        connection_settings.name()
                    )),
                ))
                .with_rules(connection_settings.forwards());
            })
            .with_remote(|pump| {
                pump.with_config(MqttClientConfig::new(
                    connection_settings.address(),
                    connection_settings.keep_alive(),
                    connection_settings.clean_session(),
                    connection_settings.credentials().clone(),
                ))
                .with_rules(connection_settings.subscriptions());
            })
            .with_store(move |suffix| {
                PublicationStore::new_ring_buffer(
                    &Path::new(
                        &(storage_settings.file_name().to_owned() + "." + &device_id + "." + suffix),
                    ),
                    storage_settings.max_file_size(),
                    storage_settings.flush_options(),
                    storage_settings.batch_size(),
                )
            })
            .build()?;

        debug!("created bridge {}...", connection_settings.name());

        Ok(Bridge {
            local_pump,
            remote_pump,
        })
    }
}

#[cfg(test)]
mod tests {
    use crate::{persist::WakingMemoryStore, settings::BridgeSettings};

    use super::*;

    #[tokio::test]
    async fn it_builds_successfully_when_in_memory() {
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let builder = bridge_builder::<WakingMemoryStore>();
        let result = builder
            .with_system_address("system_address".to_owned())
            .with_device_id("device_id".to_owned())
            .with_connection_settings(settings.upstream().unwrap().clone())
            .build();
        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn it_builds_successfully_when_ring_buffer() {
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let builder = bridge_builder::<RingBuffer>();
        let result = builder
            .with_system_address("system_address".to_owned())
            .with_device_id("device_id".to_owned())
            .with_connection_settings(settings.upstream().unwrap().clone())
            .with_storage_settings(settings.storage().unwrap().clone())
            .build();
        assert!(result.is_ok());
    }
}
