// Copyright (c) Microsoft. All rights reserved.

pub mod clients;
pub mod route;
pub mod runtime;

mod settings;
pub use settings::Settings;

/// Generic test error. Most users of ModuleRuntime don't act on the error other
/// than passing it up the call stack, so it's fine to return any error.
fn test_error() -> std::io::Error {
    std::io::Error::new(std::io::ErrorKind::Other, "test error")
}

fn new_keys() -> (
    openssl::pkey::PKey<openssl::pkey::Private>,
    openssl::pkey::PKey<openssl::pkey::Public>,
) {
    let rsa = openssl::rsa::Rsa::generate(2048).unwrap();
    let private_key = openssl::pkey::PKey::from_rsa(rsa).unwrap();

    let public_key = private_key.public_key_to_pem().unwrap();
    let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key).unwrap();

    (private_key, public_key)
}

/// Generates a self-signed cert for testing.
///
/// The `customize` parameter is an optional function that can be used to
/// override the test defaults before the certificate is signed.
pub fn test_certificate(
    common_name: &str,
    customize: Option<fn(&mut openssl::x509::X509Builder)>,
) -> (
    openssl::x509::X509,
    openssl::pkey::PKey<openssl::pkey::Private>,
) {
    let mut name = openssl::x509::X509Name::builder().unwrap();
    name.append_entry_by_text("CN", common_name).unwrap();
    let name = name.build();

    let (private_key, public_key) = new_keys();

    let mut cert = openssl::x509::X509::builder().unwrap();

    cert.set_subject_name(&name).unwrap();
    cert.set_issuer_name(&name).unwrap();
    cert.set_pubkey(&public_key).unwrap();

    let not_before = openssl::asn1::Asn1Time::from_unix(0).unwrap();
    let not_after = openssl::asn1::Asn1Time::days_from_now(30).unwrap();

    cert.set_not_before(&not_before).unwrap();
    cert.set_not_after(&not_after).unwrap();

    if let Some(customize) = customize {
        customize(&mut cert);
    }

    cert.sign(&private_key, openssl::hash::MessageDigest::sha256())
        .unwrap();

    (cert.build(), private_key)
}
