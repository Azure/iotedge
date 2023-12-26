// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Connect {
    pub workload_uri: url::Url,
    pub management_uri: url::Url,
}

impl Connect {
    pub fn workload_uri(&self) -> &url::Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &url::Url {
        &self.management_uri
    }
}

impl Default for Connect {
    fn default() -> Self {
        let workload_uri = std::env::var("IOTEDGE_CONNECT_WORKLOAD_URI")
            .unwrap_or_else(|_| "unix:///var/run/iotedge/workload.sock".to_string());
        let management_uri = std::env::var("IOTEDGE_CONNECT_MANAGEMENT_URI")
            .unwrap_or_else(|_| "unix:///var/run/iotedge/mgmt.sock".to_string());

        Connect {
            workload_uri: workload_uri.parse().expect("failed to parse workload uri"),
            management_uri: management_uri
                .parse()
                .expect("failed to parse management uri"),
        }
    }
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Listen {
    pub workload_uri: url::Url,
    pub management_uri: url::Url,
}

impl Listen {
    pub fn legacy_workload_uri(&self) -> &url::Url {
        &self.workload_uri
    }

    pub fn workload_mnt_uri(home_dir: &str) -> String {
        "unix://".to_string() + home_dir + "/mnt"
    }

    pub fn workload_uri(home_dir: &str, module_id: &str) -> Result<url::Url, url::ParseError> {
        url::Url::parse(&("unix://".to_string() + home_dir + "/mnt/" + module_id + ".sock"))
    }

    pub fn get_workload_systemd_socket_name() -> String {
        "aziot-edged.workload.socket".to_string()
    }

    pub fn get_management_systemd_socket_name() -> String {
        "aziot-edged.mgmt.socket".to_string()
    }

    pub fn management_uri(&self) -> &url::Url {
        &self.management_uri
    }
}

impl Default for Listen {
    fn default() -> Self {
        let workload_uri = std::env::var("IOTEDGE_LISTEN_WORKLOAD_URI")
            .unwrap_or_else(|_| "fd://aziot-edged.workload.socket".to_string());
        let management_uri = std::env::var("IOTEDGE_LISTEN_MANAGEMENT_URI")
            .unwrap_or_else(|_| "fd://aziot-edged.mgmt.socket".to_string());

        Listen {
            workload_uri: workload_uri.parse().expect("failed to parse workload uri"),
            management_uri: management_uri
                .parse()
                .expect("failed to parse management uri"),
        }
    }
}
