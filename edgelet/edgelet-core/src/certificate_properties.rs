// Copyright (c) Microsoft. All rights reserved.

/// Enumerator for CERTIFICATE_TYPE
#[derive(Clone, Copy, Debug, PartialEq)]
pub enum CertificateType {
    Unknown,
    Client,
    Server,
    Ca,
}

/// Globally supported properties of certificates in the Edge.
#[derive(Debug, Clone)]
pub struct CertificateProperties {
    validity_in_secs: u64,
    common_name: String,
    certificate_type: CertificateType,
    issuer_alias: String,
    alias: String,
}

impl CertificateProperties {
    pub fn new(
        validity_in_secs: u64,
        common_name: String,
        certificate_type: CertificateType,
        issuer_alias: String,
        alias: String,
    ) -> Self {
        CertificateProperties {
            validity_in_secs,
            common_name,
            certificate_type,
            issuer_alias,
            alias,
        }
    }

    pub fn validity_in_secs(&self) -> &u64 {
        &self.validity_in_secs
    }

    pub fn with_validity_in_secs(mut self, validity_in_secs: u64) -> CertificateProperties {
        self.validity_in_secs = validity_in_secs;
        self
    }

    pub fn common_name(&self) -> &str {
        &self.common_name
    }

    pub fn with_common_name(mut self, common_name: String) -> CertificateProperties {
        self.common_name = common_name;
        self
    }

    pub fn certificate_type(&self) -> &CertificateType {
        &self.certificate_type
    }

    pub fn with_certificate_type(
        mut self,
        certificate_type: CertificateType,
    ) -> CertificateProperties {
        self.certificate_type = certificate_type;
        self
    }

    pub fn issuer_alias(&self) -> &str {
        &self.issuer_alias
    }

    pub fn with_issuer_alias(mut self, issuer_alias: String) -> CertificateProperties {
        self.issuer_alias = issuer_alias;
        self
    }

    pub fn alias(&self) -> &str {
        &self.alias
    }

    pub fn with_alias(mut self, alias: String) -> CertificateProperties {
        self.alias = alias;
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default() {
        let c = CertificateProperties::new(
            3600,
            "common_name".to_string(),
            CertificateType::Client,
            "issuer_alias".to_string(),
            "alias".to_string(),
        );

        assert_eq!(&3600, c.validity_in_secs());
        assert_eq!("common_name", c.common_name());
        assert_eq!(&CertificateType::Client, c.certificate_type());
        assert_eq!("issuer_alias", c.issuer_alias());
        assert_eq!("alias", c.alias());
    }

    #[test]
    fn test_default_with_settings() {
        let c = CertificateProperties::new(
            3600,
            "common_name".to_string(),
            CertificateType::Client,
            "issuer_alias".to_string(),
            "alias".to_string(),
        ).with_certificate_type(CertificateType::Ca)
            .with_common_name("bafflegab".to_string())
            .with_validity_in_secs(240)
            .with_issuer_alias("Abraham Lincoln".to_string())
            .with_alias("Andrew Johson".to_string());

        assert_eq!(&240, c.validity_in_secs());
        assert_eq!("bafflegab", c.common_name());
        assert_eq!(&CertificateType::Ca, c.certificate_type());
        assert_eq!("Abraham Lincoln", c.issuer_alias());
        assert_eq!("Andrew Johson", c.alias());
    }
}
