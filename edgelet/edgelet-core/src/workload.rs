// Copyright (c) Microsoft. All rights reserved.

use crate::certificate_properties::CertificateType;

/// Trait to obtain configuration data needed by any implementation of the workload interface
/// for module identity and certificate management.
pub trait WorkloadConfig {
    fn iot_hub_name(&self) -> &str;
    fn parent_hostname(&self) -> Option<&str>;
    fn device_id(&self) -> &str;
    fn edge_ca_cert(&self) -> &str;
    fn edge_ca_key(&self) -> &str;
    fn trust_bundle_cert(&self) -> &str;
    fn get_cert_max_duration(&self, cert_type: CertificateType) -> i64;
}
