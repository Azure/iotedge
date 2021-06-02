// Copyright (c) Microsoft. All rights reserved.

/// Enumerator for `CERTIFICATE_TYPE`
#[derive(Clone, Copy, Debug, PartialEq)]
pub enum CertificateType {
    Unknown,
    Client,
    Server,
    Ca,
}

/// Enumerator for `CERTIFICATE_ISSUER`
#[derive(Clone, Copy, Debug, PartialEq)]
pub enum CertificateIssuer {
    DefaultCa,
    DeviceCa,
}

/// Globally supported properties of certificates in the Edge.
#[derive(Debug, Clone)]
pub struct CertificateProperties {
    common_name: String,
    certificate_type: CertificateType,
    alias: String,
    issuer: CertificateIssuer,
    dns_san_entries: Option<Vec<String>>,
    ip_entries: Option<Vec<String>>,
}

impl CertificateProperties {
    pub fn new(common_name: String, certificate_type: CertificateType, alias: String) -> Self {
        CertificateProperties {
            common_name,
            certificate_type,
            alias,
            issuer: CertificateIssuer::DefaultCa,
            dns_san_entries: None,
            ip_entries: None,
        }
    }

    pub fn common_name(&self) -> &str {
        &self.common_name
    }

    pub fn with_common_name(mut self, common_name: String) -> Self {
        self.common_name = common_name;
        self
    }

    pub fn certificate_type(&self) -> &CertificateType {
        &self.certificate_type
    }

    pub fn with_certificate_type(mut self, certificate_type: CertificateType) -> Self {
        self.certificate_type = certificate_type;
        self
    }

    pub fn alias(&self) -> &str {
        &self.alias
    }

    pub fn with_alias(mut self, alias: String) -> Self {
        self.alias = alias;
        self
    }

    pub fn issuer(&self) -> &CertificateIssuer {
        &self.issuer
    }

    pub fn with_issuer(mut self, issuer: CertificateIssuer) -> Self {
        self.issuer = issuer;
        self
    }

    pub fn dns_san_entries(&self) -> Option<&[String]> {
        self.dns_san_entries.as_ref().map(AsRef::as_ref)
    }

    pub fn with_dns_san_entries(mut self, entries: Vec<String>) -> Self {
        self.dns_san_entries = Some(entries);
        self
    }

    pub fn ip_entries(&self) -> Option<&[String]> {
        self.ip_entries.as_ref().map(AsRef::as_ref)
    }

    pub fn with_ip_entries(mut self, entries: Vec<String>) -> Self {
        self.ip_entries = Some(entries);
        self
    }
}

#[cfg(test)]
mod tests {
    use super::{CertificateIssuer, CertificateProperties, CertificateType};

    #[test]
    fn test_default() {
        let c = CertificateProperties::new(
            "common_name".to_string(),
            CertificateType::Client,
            "alias".to_string(),
        );

        assert_eq!("common_name", c.common_name());
        assert_eq!(&CertificateType::Client, c.certificate_type());
        assert_eq!("alias", c.alias());
        assert_eq!(&CertificateIssuer::DefaultCa, c.issuer());
        assert_eq!(true, c.dns_san_entries().is_none());
    }

    #[test]
    fn test_default_with_settings() {
        let input_sans = vec![String::from("serif"), String::from("sar")];
        let c = CertificateProperties::new(
            "common_name".to_string(),
            CertificateType::Client,
            "alias".to_string(),
        )
        .with_certificate_type(CertificateType::Ca)
        .with_common_name("bafflegab".to_string())
        .with_alias("Andrew Johnson".to_string())
        .with_issuer(CertificateIssuer::DeviceCa)
        .with_dns_san_entries(input_sans.clone());
        assert_eq!("bafflegab", c.common_name());
        assert_eq!(&CertificateType::Ca, c.certificate_type());
        assert_eq!("Andrew Johnson", c.alias());
        assert_eq!(&CertificateIssuer::DeviceCa, c.issuer());
        assert_eq!(&*input_sans, c.dns_san_entries().unwrap());
    }
}
