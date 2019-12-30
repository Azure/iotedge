// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::use_self
)]

mod api;
pub mod app;
mod error;
pub mod logging;
mod proxy;
mod routine;
mod settings;
pub mod signal;

pub use error::{Error, ErrorKind, InitializeErrorReason};
pub use routine::Routine;
pub use settings::{ApiSettings, ServiceSettings, Settings};

use hyper::{Body, Response};

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

#[cfg(test)]
#[allow(dead_code)]
mod tls {
    use std::fs;
    use std::path::{Path, PathBuf};

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
        public_key: Option<PathBuf>,
        private_key: Option<PathBuf>,
        cert: Option<PathBuf>,
        common_name: Option<String>,
    }

    impl CertGenerator {
        pub fn private_key(&mut self, path: &Path) -> &Self {
            self.private_key = Some(path.to_path_buf());
            self
        }

        pub fn public_key(&mut self, path: &Path) -> &Self {
            self.public_key = Some(path.to_path_buf());
            self
        }

        pub fn cert(&mut self, path: &Path) -> &Self {
            self.cert = Some(path.to_path_buf());
            self
        }

        pub fn common_name(&mut self, name: String) -> &Self {
            self.common_name = Some(name);
            self
        }

        pub fn generate(&self) -> Result<X509, CertGeneratorError> {
            let rsa = Rsa::generate(2048)?;
            let pkey = PKey::from_rsa(rsa)?;

            if let Some(pkey_path) = &self.public_key {
                fs::write(pkey_path, pkey.public_key_to_pem()?)?;
            }

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

            let x509 = builder.build();

            if let Some(cert_path) = &self.cert {
                fs::write(cert_path, x509.to_pem()?)?;
            }

            Ok(x509)
        }
    }
}
