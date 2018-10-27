// Copyright (c) Microsoft. All rights reserved.

use certificate_properties::CertificateType;

pub trait WorkloadConfig {
    fn iot_hub_name(&self) -> &str;
    fn device_id(&self) -> &str;
    fn get_max_duration(&self, cert_type: CertificateType) -> i64;
}
