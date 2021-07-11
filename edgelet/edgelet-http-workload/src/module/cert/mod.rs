// Copyright (c) Microsoft. All rights reserved.

pub(crate) mod identity;
pub(crate) mod server;

#[derive(Debug, serde::Serialize)]
#[serde(tag = "type")]
pub(crate) enum PrivateKey {
    #[serde(rename = "ref")]
    Reference {
        #[serde(rename = "ref")]
        reference: String,
    },
    Bytes {
        bytes: String,
    },
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct CertificateResponse {
    #[serde(rename = "privateKey")]
    private_key: PrivateKey,

    certificate: String,
    expiration: String,
}

enum SubjectAltName {
    DNS(String),
    IP(String),
}

fn new_keys_and_csr(
    common_name: &str,
    subject_alt_names: Vec<SubjectAltName>,
    mut extensions: openssl::stack::Stack<openssl::x509::X509Extension>,
) -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
        Vec<u8>,
    ),
    openssl::error::ErrorStack,
> {
    let rsa = openssl::rsa::Rsa::generate(2048)?;
    let private_key = openssl::pkey::PKey::from_rsa(rsa)?;

    let public_key = private_key.public_key_to_pem()?;
    let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key)?;

    let mut csr = openssl::x509::X509Req::builder()?;
    csr.set_version(0)?;

    let mut names = openssl::x509::extension::SubjectAlternativeName::new();

    for name in subject_alt_names {
        match name {
            SubjectAltName::DNS(name) => names.dns(&name),
            SubjectAltName::IP(name) => names.ip(&name),
        };
    }

    let names = names.build(&csr.x509v3_context(None))?;
    extensions.push(names)?;

    csr.add_extensions(&extensions)?;

    csr.sign(&private_key, openssl::hash::MessageDigest::sha256())?;

    let csr = csr.build().to_pem()?;

    Ok((private_key, public_key, csr))
}
