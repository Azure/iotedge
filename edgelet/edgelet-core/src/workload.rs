// Copyright (c) Microsoft. All rights reserved.

use certificate_properties::CertificateType;

#[derive(Debug, Clone)]

pub struct WorkloadConfigData {
    iot_hub_name: String,
    device_id: String,
    id_cert_max_duration: u64,
    srv_cert_max_duration: u64,
}
