// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{CertificateType, WorkloadConfig};
use std::sync::Arc;

#[derive(Debug, Clone)]
struct WorkloadConfigData {
    iot_hub_name: String,
    parent_hostname: Option<String>,
    device_id: String,
    edge_ca_id: String,
    id_cert_max_duration: i64,
    srv_cert_max_duration: i64,
}

impl WorkloadConfigData {
    pub fn new(
        iot_hub_name: String,
        parent_hostname: Option<String>,
        device_id: String,
        edge_ca_id: String,
        id_cert_max_duration: i64,
        srv_cert_max_duration: i64,
    ) -> Self {
        WorkloadConfigData {
            iot_hub_name,
            parent_hostname,
            device_id,
            edge_ca_id,
            id_cert_max_duration,
            srv_cert_max_duration,
        }
    }

    pub fn iot_hub_name(&self) -> &str {
        &self.iot_hub_name
    }

    pub fn parent_hostname(&self) -> Option<&str> {
        self.parent_hostname.as_deref()
    }

    pub fn device_id(&self) -> &str {
        &self.device_id
    }

    pub fn edge_ca_id(&self) -> &str {
        &self.edge_ca_id
    }

    pub fn id_cert_max(&self) -> i64 {
        self.id_cert_max_duration
    }

    pub fn server_cert_max(&self) -> i64 {
        self.srv_cert_max_duration
    }
}

#[derive(Debug, Clone)]
pub struct WorkloadData {
    data: Arc<WorkloadConfigData>,
}

impl WorkloadData {
    pub fn new(
        iot_hub_name: String,
        parent_hostname: Option<String>,
        device_id: String,
        edge_ca_id: String,
        id_cert_max_duration: i64,
        srv_cert_max_duration: i64,
    ) -> Self {
        let w = WorkloadConfigData::new(
            iot_hub_name,
            parent_hostname,
            device_id,
            edge_ca_id,
            id_cert_max_duration,
            srv_cert_max_duration,
        );
        WorkloadData { data: Arc::new(w) }
    }
}

impl WorkloadConfig for WorkloadData {
    fn iot_hub_name(&self) -> &str {
        self.data.iot_hub_name()
    }

    fn parent_hostname(&self) -> Option<&str> {
        self.data.parent_hostname()
    }

    fn device_id(&self) -> &str {
        self.data.device_id()
    }

    fn edge_ca_id(&self) -> &str {
        self.data.edge_ca_id()
    }

    fn get_cert_max_duration(&self, cert_type: CertificateType) -> i64 {
        match cert_type {
            CertificateType::Client => self.data.id_cert_max(),
            CertificateType::Server => self.data.server_cert_max(),
            _ => 0,
        }
    }
}
