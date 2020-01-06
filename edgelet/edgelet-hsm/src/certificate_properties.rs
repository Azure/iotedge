// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{
    CertificateIssuer as CoreCertificateIssuer, CertificateProperties as CoreCertificateProperties,
    CertificateType as CoreCertificateType, IOTEDGED_CA_ALIAS,
};
use hsm::{
    CertificateProperties as HsmCertificateProperties, CertificateType as HsmCertificateType,
};

fn convert_certificate_type(core: CoreCertificateType) -> HsmCertificateType {
    match core {
        CoreCertificateType::Ca => HsmCertificateType::Ca,
        CoreCertificateType::Server => HsmCertificateType::Server,
        CoreCertificateType::Client => HsmCertificateType::Client,
        CoreCertificateType::Unknown => HsmCertificateType::Unknown,
    }
}

/// Convert Certificate properties defined in edgelet-core to HSM specific Certificate properties
pub fn convert_properties(
    core: &CoreCertificateProperties,
    device_ca_alias: &str,
) -> HsmCertificateProperties {
    let issuer_ca = match core.issuer() {
        CoreCertificateIssuer::DeviceCa => device_ca_alias.to_string(),
        CoreCertificateIssuer::DefaultCa => IOTEDGED_CA_ALIAS.to_string(),
    };
    let no_sans: Vec<String> = vec![];
    HsmCertificateProperties::new(
        *core.validity_in_secs(),
        core.common_name().to_string(),
        convert_certificate_type(*core.certificate_type()),
        issuer_ca,
        core.alias().to_string(),
        core.san_entries().unwrap_or(&no_sans).to_vec(),
    )
}

#[cfg(test)]
mod tests {
    use edgelet_core::{
        CertificateIssuer as CoreCertificateIssuer,
        CertificateProperties as CoreCertificateProperties, CertificateType as CoreCertificateType,
        IOTEDGED_CA_ALIAS,
    };
    use hsm::{
        CertificateProperties as HsmCertificateProperties, CertificateType as HsmCertificateType,
    };

    fn check_conversion(core: &CoreCertificateProperties, hsm: &HsmCertificateProperties) {
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
        match core.issuer() {
            CoreCertificateIssuer::DefaultCa => assert_eq!(IOTEDGED_CA_ALIAS, hsm.issuer_alias()),
            CoreCertificateIssuer::DeviceCa => assert_eq!("device_ca_test", hsm.issuer_alias()),
        };
        assert_eq!(core.alias(), hsm.alias());

        let expected_sans = &[String::from("serif"), String::from("guile")];
        if core.san_entries().is_some() {
            assert_eq!(expected_sans, hsm.san_entries());
        } else {
            let no_sans: &[String] = &[];
            assert_eq!(no_sans, hsm.san_entries());
        }

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

            check_conversion(
                &core_props,
                &super::convert_properties(&core_props, "device_ca_test"),
            );
        }

        let core_props = CoreCertificateProperties::new(
            validity_in_secs,
            common_name.clone(),
            CoreCertificateType::Ca,
            alias.clone(),
        )
        .with_issuer(CoreCertificateIssuer::DeviceCa);
        check_conversion(
            &core_props,
            &super::convert_properties(&core_props, "device_ca_test"),
        );

        let input_sans: Vec<String> = vec![String::from("serif"), String::from("guile")];
        let core_props = CoreCertificateProperties::new(
            validity_in_secs,
            common_name,
            CoreCertificateType::Ca,
            alias,
        )
        .with_san_entries(input_sans);
        check_conversion(
            &core_props,
            &super::convert_properties(&core_props, "device_ca_test"),
        );
    }
}
