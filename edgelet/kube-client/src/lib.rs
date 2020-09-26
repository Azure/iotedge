// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::use_self
)]

pub mod client;
pub mod config;
pub mod error;
pub mod kube;

pub use self::client::{Client, HttpClient};
pub use self::config::{get_config, Config, TokenSource, ValueToken};
pub use self::error::{Error, ErrorKind, RequestType};

#[cfg(test)]
#[allow(dead_code)]
mod tls {

    use failure::Fail;
    use openssl::asn1::Asn1Time;
    use openssl::error::ErrorStack;
    use openssl::hash::MessageDigest;
    use openssl::nid::Nid;
    use openssl::pkey::PKey;
    use openssl::rsa::Rsa;
    use openssl::x509::extension::{
        AuthorityKeyIdentifier, BasicConstraints, ExtendedKeyUsage, KeyUsage, SubjectKeyIdentifier,
    };
    use openssl::x509::{X509Name, X509};
    use std::fmt::{Display, Formatter};
    use std::io::Error as IoError;

    #[derive(Debug, Fail)]
    pub enum CertGeneratorError {
        ErrorStack(ErrorStack),
        Io(IoError),
    }

    impl Display for CertGeneratorError {
        fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
            match self {
                CertGeneratorError::ErrorStack(err) => write!(f, "{}", err),
                CertGeneratorError::Io(err) => write!(f, "{}", err),
            }
        }
    }

    impl From<ErrorStack> for CertGeneratorError {
        fn from(err: ErrorStack) -> Self {
            CertGeneratorError::ErrorStack(err)
        }
    }

    impl From<IoError> for CertGeneratorError {
        fn from(err: IoError) -> Self {
            CertGeneratorError::Io(err)
        }
    }

    #[derive(Default)]
    pub struct CertGenerator {
        common_name: Option<String>,
    }

    impl CertGenerator {
        pub fn common_name(&mut self, name: String) -> &Self {
            self.common_name = Some(name);
            self
        }

        pub fn generate(&self) -> Result<Vec<u8>, CertGeneratorError> {
            let rsa = Rsa::generate(2048)?;
            let pkey = PKey::from_rsa(rsa)?;

            let mut name = X509Name::builder()?;
            name.append_entry_by_nid(
                Nid::COMMONNAME,
                self.common_name
                    .as_ref()
                    .unwrap_or(&"localhost".to_string()),
            )?;
            let name = name.build();

            let mut builder = X509::builder()?;
            builder.set_version(2)?;
            builder.set_subject_name(&name)?;
            builder.set_issuer_name(&name)?;
            builder.set_not_before(Asn1Time::days_from_now(0)?.as_ref())?;
            builder.set_not_after(Asn1Time::days_from_now(365)?.as_ref())?;
            builder.set_pubkey(&pkey)?;

            let basic_constraints = BasicConstraints::new().critical().ca().build()?;
            builder.append_extension(basic_constraints)?;
            let key_usage = KeyUsage::new()
                .digital_signature()
                .key_encipherment()
                .build()?;
            builder.append_extension(key_usage)?;
            let ext_key_usage = ExtendedKeyUsage::new()
                .client_auth()
                .server_auth()
                .build()?;
            builder.append_extension(ext_key_usage)?;
            let subject_key_identifier =
                SubjectKeyIdentifier::new().build(&builder.x509v3_context(None, None))?;
            builder.append_extension(subject_key_identifier)?;
            let authority_key_identifier = AuthorityKeyIdentifier::new()
                .keyid(true)
                .build(&builder.x509v3_context(None, None))?;
            builder.append_extension(authority_key_identifier)?;

            builder.sign(&pkey, MessageDigest::sha256())?;

            let x509 = builder.build().to_pem()?;

            Ok(x509)
        }
    }
}
