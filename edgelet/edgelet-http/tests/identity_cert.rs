// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use edgelet_hsm::{Crypto, HsmLock};
use edgelet_http::client::{Client as HttpClient, TokenSource};
use edgelet_http::{Error, MaybeProxyClient, PemCertificate};
use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::{
    CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate, IOTEDGED_CA_ALIAS, GetIssuerAlias,
};
mod test_utils;
use chrono::{DateTime, Utc};
use edgelet_test_utils::{get_unused_tcp_port, run_tls_tcp_server};
use hyper::Method;
use test_utils::TestHSMEnvSetup;
use url::Url;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

struct StaticTokenSource {
    token: String,
}

impl TokenSource for StaticTokenSource {
    type Error = Error;
    fn get(&self, _expiry: &DateTime<Utc>) -> Result<String, Error> {
        Ok(self.token.clone())
    }
}

impl Clone for StaticTokenSource {
    fn clone(&self) -> Self {
        StaticTokenSource {
            token: self.token.clone(),
        }
    }
}

#[test]
fn http_client_identtity_cert_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock).unwrap();

    let issuer_alias = crypto
        .get_issuer_alias(CertificateIssuer::DeviceCa)
        .unwrap();
    assert!(!issuer_alias.is_empty());

    let issuer_ca = crypto.get_certificate(issuer_alias).unwrap();
    let ca_pem = PemCertificate::from(&issuer_ca).unwrap();

    // create the default issuing CA cert properties
    let edgelet_ca_props = CertificateProperties::new(
        3600,
        "test-iotedge-ca".to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    // act create the default issuing CA cert
    let _workload_ca_cert = crypto.create_certificate(&edgelet_ca_props).unwrap();

    let client_cert_alias = String::from("test-client-cert");
    let client_cn = "test-iotedge-client";
    let props = CertificateProperties::new(
        3600,
        client_cn.to_string(),
        CertificateType::Client,
        client_cert_alias.clone(),
    );
    let client_cert = crypto.create_certificate(&props).unwrap();

    let server_cert_alias = String::from("test-server-cert");
    let props = CertificateProperties::new(
        3600,
        "localhost".to_string(),
        CertificateType::Server,
        "test-server-cert".to_string(),
    );
    let server_cert = crypto.create_certificate(&props).unwrap();

    let server_pem = PemCertificate::from(&server_cert).unwrap();
    let client_pem = PemCertificate::from(&client_cert).unwrap();
    let port = get_unused_tcp_port();
    let server = run_tls_tcp_server("127.0.0.1", port, server_pem.get_identity().unwrap());
    let hyper_client = MaybeProxyClient::new_with_identity_cert(None, client_pem, Some(ca_pem)).unwrap();
    let url = Url::parse(&format!("https://localhost:{}", port)).unwrap();
    let token_source: Option<StaticTokenSource> = None;

    let http_client =
        HttpClient::new(hyper_client, token_source, "2019-01-01".to_string(), url).unwrap();

    let request = http_client.request::<(), ()>(Method::GET, "", None, None, false);

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(request).unwrap();

    // cleanup
    crypto.destroy_certificate(client_cert_alias).unwrap();
    crypto.destroy_certificate(server_cert_alias).unwrap();
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
}
