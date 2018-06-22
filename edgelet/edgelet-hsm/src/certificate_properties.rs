// Copyright (c) Microsoft. All rights reserved.

use super::IOTEDGED_CA;
use edgelet_core::{
    CertificateProperties as CoreCertificateProperties, CertificateType as CoreCertificateType,
};
use hsm::{
    CertificateProperties as HsmCertificateProperties, CertificateType as HsmCertificateType,
};

fn convert_certificate_type(core: &CoreCertificateType) -> HsmCertificateType {
    match *core {
        CoreCertificateType::Ca => HsmCertificateType::Ca,
        CoreCertificateType::Server => HsmCertificateType::Server,
        CoreCertificateType::Client => HsmCertificateType::Client,
        CoreCertificateType::Unknown => HsmCertificateType::Unknown,
    }
}

/// Convert Certificate properties defined in edgelet-core to HSM specific Certificate properties
pub fn convert_properties(core: &CoreCertificateProperties) -> HsmCertificateProperties {
    HsmCertificateProperties::new(
        *core.validity_in_secs(),
        core.common_name().to_string(),
        convert_certificate_type(core.certificate_type()),
        IOTEDGED_CA.to_string(),
        core.alias().to_string(),
    )
}

#[cfg(test)]
mod tests {
    use super::super::IOTEDGED_CA;
    use edgelet_core::{
        CertificateProperties as CoreCertificateProperties, CertificateType as CoreCertificateType,
    };
    use hsm::{
        CertificateProperties as HsmCertificateProperties, CertificateType as HsmCertificateType,
    };

    fn check_conversion(core: &CoreCertificateProperties, hsm: HsmCertificateProperties) {
        assert_eq!(core.validity_in_secs(), hsm.validity_in_secs());
        assert_eq!(core.common_name(), hsm.common_name());
        match *core.certificate_type() {
            CoreCertificateType::Ca => assert_eq!(HsmCertificateType::Ca, *hsm.certificate_type()),
            CoreCertificateType::Server => {
                assert_eq!(HsmCertificateType::Server, *hsm.certificate_type())
            }
            CoreCertificateType::Client => {
                assert_eq!(HsmCertificateType::Client, *hsm.certificate_type())
            }
            CoreCertificateType::Unknown => {
                assert_eq!(HsmCertificateType::Unknown, *hsm.certificate_type())
            }
        }
        assert_eq!(IOTEDGED_CA, hsm.issuer_alias());
        assert_eq!(core.alias(), hsm.alias());

        assert_eq!(None, hsm.country());
        assert_eq!(None, hsm.state());
        assert_eq!(None, hsm.locality());
        assert_eq!(None, hsm.organization());
        assert_eq!(None, hsm.organization_unit());
    }

    #[test]
    fn test_conversion() {
        let common_name = "Common Name".to_string();
        let validity_in_secs = 1_000_000_000_000_u64;
        let types = vec![
            CoreCertificateType::Ca,
            CoreCertificateType::Server,
            CoreCertificateType::Client,
            CoreCertificateType::Unknown,
        ];
        let alias = "alias".to_string();
        for ct in types {
            let core_props = CoreCertificateProperties::new(
                validity_in_secs,
                common_name.clone(),
                ct,
                alias.clone(),
            );

            check_conversion(&core_props, super::convert_properties(&core_props));
        }
    }

}
