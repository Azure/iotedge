// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::use_self
)]

mod constants;
mod convert;
mod error;
mod module;
mod runtime;
mod settings;

pub use error::{Error, ErrorKind};
pub use module::KubeModule;
pub use runtime::KubeModuleRuntime;
pub use settings::Settings;

#[cfg(test)]
mod tests {
    use crate::settings::Settings;
    use config::{Config, File, FileFormat};
    use json_patch::merge;
    use serde_json::{self, json, Value as JsonValue};

    pub fn make_settings(merge_json: Option<JsonValue>) -> Settings {
        let mut config = Config::default();
        let mut config_json = json!({
            "provisioning": {
                "source": "manual",
                "device_connection_string": "HostName=moo.azure-devices.net;DeviceId=boo;SharedAccessKey=boo"
            },
            "agent": {
                "name": "edgeAgent",
                "type": "docker",
                "env": {},
                "config": {
                    "image": "mcr.microsoft.com/azureiotedge-agent:1.0",
                    "auth": {}
                }
            },
            "hostname": "default1",
            "connect": {
                "management_uri": "http://localhost:35000",
                "workload_uri": "http://localhost:35001"
            },
            "listen": {
                "management_uri": "http://localhost:35000",
                "workload_uri": "http://localhost:35001"
            },
            "homedir": "/var/lib/iotedge",
            "namespace": "default",
            "use_pvc": true,
            "iot_hub_hostname": "iotHub",
            "device_id": "device1",
            "proxy_image": "proxy:latest",
            "proxy_config_path": "/etc/traefik",
            "proxy_config_map_name": "device1-iotedged-proxy-config",
            "image_pull_policy": "IfNotPresent",
            "service_account_name": "iotedge",
            "device_hub_selector": "",
        });

        if let Some(merge_json) = merge_json {
            merge(&mut config_json, &merge_json);
        }

        config
            .merge(File::from_str(&config_json.to_string(), FileFormat::Json))
            .unwrap();

        config.try_into().unwrap()
    }
}
