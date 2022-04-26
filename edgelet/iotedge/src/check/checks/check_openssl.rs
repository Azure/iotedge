use anyhow::Context;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct CheckOpenssl {}

#[async_trait::async_trait]
impl Checker for CheckOpenssl {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-openssl",
            description: "IoT Edge can create self signed certs with Openssl",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckOpenssl {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        // Generate keys
        let rsa = openssl::rsa::Rsa::generate(2048).with_context(|| format!("Unable to generate RSA keys"))?;
        let private_key = openssl::pkey::PKey::from_rsa(rsa).with_context(|| format!("Unable to create private key"))?;
        let public_key = private_key.public_key_to_pem().with_context(|| format!("Unable to create public key to pem"))?;
        let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key).with_context(|| format!("Unable to create public key from pem"))?;
        
        // Create common name
        let mut name = openssl::x509::X509Name::builder().unwrap();
        name.append_entry_by_text("CN", "test_common_name").with_context(|| format!("Unable to append common Name"))?;
        let common_name = name.build();

        // Create cert
        let mut cert = openssl::x509::X509::builder().with_context(|| format!("Unable to build X509 cert"))?;
        cert.set_subject_name(&common_name).with_context(|| format!("Unable to set subject name for X509 cert"))?;
        cert.set_issuer_name(&common_name).with_context(|| format!("Unable to set issue name for X509 cert"))?;
        cert.set_pubkey(&public_key).with_context(|| format!("Unable to set public key for X509 cert"))?;

        // Set expiration
        let not_before = openssl::asn1::Asn1Time::from_unix(0).with_context(|| format!("Unable to create not before expiration date for self signed cert"))?;
        let not_after = openssl::asn1::Asn1Time::days_from_now(30).with_context(|| format!("Unable to create not after expiration date for self signed cert"))?;
        cert.set_not_before(&not_before).with_context(|| format!("Unable to set expiration date for self signed cert"))?;
        cert.set_not_after(&not_after).with_context(|| format!("Unable to set expiration date for self signed cert"))?;

        cert.sign(&private_key, openssl::hash::MessageDigest::sha256()).with_context(|| format!("Unable to create self signed cert"))?;

        Ok(CheckResult::Ok)
    }
}
