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

fn new_keys() -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    openssl::error::ErrorStack,
> {
    let rsa = openssl::rsa::Rsa::generate(2048)?;
    let private_key = openssl::pkey::PKey::from_rsa(rsa)?;

    let public_key = private_key.public_key_to_pem()?;
    let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key)?;

    Ok((private_key, public_key))
}

fn new_csr(
    common_name: &str,
    keys: (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    subject_alt_names: Vec<SubjectAltName>,
    mut extensions: openssl::stack::Stack<openssl::x509::X509Extension>,
) -> Result<Vec<u8>, openssl::error::ErrorStack> {
    let private_key = keys.0;
    let public_key = keys.1;

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

    Ok(csr)
}

async fn get_edge_ca(
    key_client: std::sync::Arc<futures_util::lock::Mutex<aziot_key_client_async::Client>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<aziot_cert_client_async::Client>>,
    edge_ca_cert: String,
    edge_ca_key: String,
    device_id: String,
) -> Result<aziot_key_common::KeyHandle, http_common::server::Error> {
    let key_handle = {
        let key_client = key_client.lock().await;

        key_client
            .create_key_pair_if_not_exists(&edge_ca_key, Some("rsa-2048:*"))
            .await
            .map_err(|err| edgelet_http::error::server_error("failed to retrieve edge ca key"))
    }?;

    let cert_client = cert_client.lock().await;

    // Renew the Edge CA certificate if it's close to expiry or not available.
    let renew_cert = match cert_client.get_cert(&edge_ca_cert).await {
        Ok(cert) => {
            let cert = openssl::x509::X509::from_pem(&cert)
                .map_err(|_| edgelet_http::error::server_error("failed to parse edge ca cert"))?;

            let current_time =
                openssl::asn1::Asn1Time::days_from_now(0).expect("current time must be valid");

            let diff = current_time.diff(&cert.not_after()).map_err(|_| {
                edgelet_http::error::server_error("failed to determine edge ca expiration time")
            })?;
            let diff = i64::from(diff.secs) + i64::from(diff.days) * 86400;

            // Renew certificate if it expires in the next 5 minutes.
            diff < 300
        }
        Err(_) => true,
    };

    if renew_cert {
        let common_name = format!("iotedged workload ca {}", device_id);
        let keys = edge_ca_keys()?;

        let extensions = edge_ca_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set edge ca csr extensions")
        })?;

        let csr = new_csr(&common_name, keys, Vec::new(), extensions)
            .map_err(|_| edgelet_http::error::server_error("failed to generate edge ca csr"))?;

        cert_client
            .create_cert(&edge_ca_cert, &csr, Some((&edge_ca_cert, &key_handle)))
            .await
            .map_err(|_| edgelet_http::error::server_error("failed to create edge ca cert"))?;
    }

    Ok(key_handle)
}

fn edge_ca_keys() -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    http_common::server::Error,
> {
    todo!()
}

fn edge_ca_extensions(
) -> Result<openssl::stack::Stack<openssl::x509::X509Extension>, openssl::error::ErrorStack> {
    let mut csr_extensions = openssl::stack::Stack::new()?;

    let mut key_usage = openssl::x509::extension::KeyUsage::new();
    key_usage.critical().digital_signature().key_cert_sign();

    let mut basic_constraints = openssl::x509::extension::BasicConstraints::new();
    basic_constraints.ca().critical().pathlen(0);

    let key_usage = key_usage.build()?;
    let basic_constraints = basic_constraints.build()?;

    csr_extensions.push(key_usage)?;
    csr_extensions.push(basic_constraints)?;

    Ok(csr_extensions)
}
