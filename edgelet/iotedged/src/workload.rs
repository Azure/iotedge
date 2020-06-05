// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{CertificateType, WorkloadConfig};
use std::sync::Arc;

#[derive(Debug, Clone)]
struct WorkloadConfigData {
    upstream_hostname: String,
    device_id: String,
    id_cert_max_duration: i64,
    srv_cert_max_duration: i64,
}

impl WorkloadConfigData {
    pub fn new(
        upstream_hostname: String,
        device_id: String,
        id_cert_max_duration: i64,
        srv_cert_max_duration: i64,
    ) -> Self {
        WorkloadConfigData {
            upstream_hostname,
            device_id,
            id_cert_max_duration,
            srv_cert_max_duration,
        }
    }

    pub fn upstream_hostname(&self) -> &str {
        &self.upstream_hostname
    }

    pub fn device_id(&self) -> &str {
        &self.device_id
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
        upstream_hostname: String,
        device_id: String,
        id_cert_max_duration: i64,
        srv_cert_max_duration: i64,
    ) -> Self {
        let w = WorkloadConfigData::new(
            upstream_hostname,
            device_id,
            id_cert_max_duration,
            srv_cert_max_duration,
        );
        WorkloadData { data: Arc::new(w) }
    }
}

impl WorkloadConfig for WorkloadData {
    fn upstream_hostname(&self) -> &str {
        self.data.upstream_hostname()
    }

    fn device_id(&self) -> &str {
        self.data.device_id()
    }

    fn get_cert_max_duration(&self, cert_type: CertificateType) -> i64 {
        match cert_type {
            CertificateType::Client => self.data.id_cert_max(),
            CertificateType::Server => self.data.server_cert_max(),
            _ => 0,
        }
    }
}
