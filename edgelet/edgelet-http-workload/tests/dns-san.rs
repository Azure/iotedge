// Copyright (c) Microsoft. All rights reserved.

// We disable this test on Windows in order to avoid having to add a dependency
// on OpenSSL.
#![cfg(not(windows))]
#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate chrono;
extern crate edgelet_core;
extern crate edgelet_hsm;
extern crate edgelet_http_workload;
extern crate edgelet_test_utils;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate native_tls;
extern crate openssl;
extern crate serde;
extern crate serde_json;
extern crate tempfile;
extern crate tokio;
extern crate tokio_tls;
extern crate workload;

use std::env;
use std::str;

use chrono::{Duration, Utc};
use futures::{Future, Stream};
use hyper::service::Service;
use hyper::{Body, Request};
use native_tls::{Certificate as TlsCertificate, Identity};
use openssl::pkcs12::Pkcs12;
use openssl::pkey::PKey;
use openssl::stack::Stack;
use openssl::x509::X509;
use serde::de::DeserializeOwned;
use tempfile::TempDir;
use tokio::io;
use tokio::net::{TcpListener, TcpStream};
use tokio::prelude::*;

use edgelet_core::crypto::MemoryKeyStore;
use edgelet_core::pid::Pid;
use edgelet_core::{
    Certificate, CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate,
    ModuleRuntimeState, ModuleStatus, WorkloadConfig, IOTEDGED_CA_ALIAS,
};
use edgelet_hsm::Crypto;
use edgelet_http_workload::WorkloadService;
use edgelet_test_utils::get_unused_tcp_port;
use edgelet_test_utils::module::{TestConfig, TestModule, TestRuntime};
use workload::models::{CertificateResponse, ServerCertificateRequest, TrustBundleResponse};

const MODULE_PID: i32 = 42;

/// The HSM lib expects this variable to be set with home directory of the daemon.
const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";
const COMMON_NAME: &str = "staycalm";

#[derive(Clone, Copy, Debug, Fail)]
pub enum Error {
    #[fail(display = "General error")]
    General,
}

#[derive(Clone)]
struct Config {
    hub_name: String,
    device_id: String,
    cert_max_duration: i64,
}

impl WorkloadConfig for Config {
    fn iot_hub_name(&self) -> &str {
        &self.hub_name
    }

    fn device_id(&self) -> &str {
        &self.device_id
    }

    fn get_cert_max_duration(&self, _cert_type: CertificateType) -> i64 {
        self.cert_max_duration
    }
}

fn init_crypto() -> Crypto {
    let crypto = Crypto::new().unwrap();

    // create the default issuing CA cert
    let edgelet_ca_props = CertificateProperties::new(
        3600,
        "test-iotedge-cn".to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);
    let workload_ca_cert = crypto.create_certificate(&edgelet_ca_props).unwrap();
    assert!(!workload_ca_cert.pem().unwrap().as_bytes().is_empty());

    crypto
}

fn request<T>(service: &mut WorkloadService, req: Request<Body>) -> T
where
    T: DeserializeOwned,
{
    let buffer = service
        .call(req)
        .wait()
        .unwrap()
        .into_body()
        .collect()
        .wait()
        .unwrap()
        .iter()
        .fold(vec![], |mut out, chunk| {
            out.extend_from_slice(&chunk);
            out
        });

    serde_json::from_slice(&buffer).unwrap()
}

fn get_trust_bundle(service: &mut WorkloadService) -> TrustBundleResponse {
    request(
        service,
        Request::builder()
            .method("GET")
            .uri("http://localhost/trust-bundle?api-version=2018-06-28")
            .body(Body::empty())
            .unwrap(),
    )
}

fn generate_server_cert(
    module_id: &str,
    generation_id: &str,
    service: &mut WorkloadService,
) -> CertificateResponse {
    let expiration = Utc::now() + Duration::hours(2);
    let sign_request =
        ServerCertificateRequest::new(COMMON_NAME.to_string(), expiration.to_rfc3339());
    let json = serde_json::to_string(&sign_request).unwrap();

    let mut req = Request::builder()
        .method("POST")
        .uri(format!(
            "http://localhost/modules/{}/genid/{}/certificate/server?api-version=2018-06-28",
            module_id, generation_id
        ))
        .header("Content-Type", "application/json")
        .body(Body::from(json))
        .unwrap();

    // set the correct Pid value on the request so that authorization works
    req.extensions_mut().insert(Pid::Value(MODULE_PID));

    request(service, req)
}

fn create_workload_service(module_id: &str) -> (WorkloadService, Crypto) {
    let key_store = MemoryKeyStore::new();
    let crypto = init_crypto();
    let runtime = TestRuntime::<Error>::new(Ok(TestModule::new(
        module_id.to_string(),
        TestConfig::new("img1".to_string()),
        Ok(ModuleRuntimeState::default()
            .with_status(ModuleStatus::Running)
            .with_pid(Pid::Value(MODULE_PID))),
    )));
    let config = Config {
        hub_name: "hub1".to_string(),
        device_id: "d1".to_string(),
        cert_max_duration: 10_000_000,
    };

    (
        WorkloadService::new(&key_store, crypto.clone(), &runtime, config)
            .wait()
            .unwrap(),
        crypto,
    )
}

fn run_echo_server(server_cert: Identity, port: u16) -> impl Future<Item = (), Error = ()> {
    let addr = format!("127.0.0.1:{}", port).parse().unwrap();
    let tcp = TcpListener::bind(&addr).unwrap();
    let tls_acceptor = tokio_tls::TlsAcceptor::from(
        native_tls::TlsAcceptor::builder(server_cert)
            .build()
            .unwrap(),
    );

    tcp.incoming()
        .for_each(move |socket| {
            let tls_accept = tls_acceptor
                .accept(socket)
                .and_then(move |tls| {
                    let (reader, writer) = tls.split();
                    let conn = io::copy(reader, writer)
                        .map(|_| ())
                        .map_err(|err| panic!("IO copy error: {:#?}", err));

                    tokio::spawn(conn);
                    Ok(())
                })
                .map_err(|err| panic!("TLS accept error: {:#?}", err));

            tokio::spawn(tls_accept);
            Ok(())
        })
        .map_err(|err| panic!("server error: {:#?}", err))
}

fn run_echo_client(
    service: &mut WorkloadService,
    port: u16,
    domain_name: &str,
) -> impl Future<Item = (), Error = ()> {
    const MESSAGE: &str = "Don't panic";

    let mut builder = native_tls::TlsConnector::builder();

    // add trust-bundle certs
    let trust_bundle = get_trust_bundle(service);
    let certs = X509::stack_from_pem(trust_bundle.certificate().as_bytes())
        .unwrap()
        .into_iter()
        .map(|cert| TlsCertificate::from_der(&cert.to_der().unwrap()).unwrap());
    for cert in certs {
        builder.add_root_certificate(cert);
    }
    let tls_connector = tokio_tls::TlsConnector::from(builder.build().unwrap());

    let addr = format!("127.0.0.1:{}", port).parse().unwrap();
    let domain_name = domain_name.to_string();
    TcpStream::connect(&addr)
        .and_then(move |socket| {
            // the "domain_name" passed below is used for TLS verification; the CN
            // on the cert *could be* different from "domain_name" but the connection should
            // still work because the name passed here is added to the server cert as a
            // DNS SAN entry
            tls_connector
                .connect(&domain_name, socket)
                .map_err(|err| panic!("TLS client connect error: {:#?}", err))
        })
        .and_then(|socket| io::write_all(socket, MESSAGE.as_bytes()))
        .and_then(|(socket, _)| io::read_exact(socket, vec![0_u8; MESSAGE.as_bytes().len()]))
        .map(|(_, buff)| assert_eq!(MESSAGE, str::from_utf8(&buff).unwrap()))
        .map_err(|err| panic!("TLS read error: {:#?}", err))
}

fn init_test(module_id: &str, generation_id: &str) -> (WorkloadService, Identity, TempDir, Crypto) {
    // setup the IOTEDGE_HOMEDIR folder where certs can be generated and stored
    let home_dir = TempDir::new().unwrap();
    env::set_var(HOMEDIR_KEY, &home_dir.path());
    println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());

    let (mut service, crypto) = create_workload_service(module_id);
    let cert = generate_server_cert(module_id, generation_id, &mut service);
    assert_eq!(cert.private_key().type_().as_str(), "key");

    // load all the certs returned by the workload API
    let mut certs = X509::stack_from_pem(cert.certificate().as_bytes()).unwrap();

    // the first cert is the server cert and the other certs are part of the CA
    // chain; we skip the server cert and build an OpenSSL cert stack with the
    // other certs
    let mut ca_certs = Stack::new().unwrap();
    for cert in certs.split_off(1) {
        ca_certs.push(cert).unwrap();
    }

    // load the private key for the server cert
    let key = PKey::private_key_from_pem(cert.private_key().bytes().unwrap().as_bytes()).unwrap();

    // build a PKCS12 cert archive that includes:
    //  - the server cert
    //  - the private key for the server cert
    //  - all the other certs that are part of the CA chain
    let server_cert = &certs[0];
    let mut builder = Pkcs12::builder();
    builder.ca(ca_certs);
    let pkcs_certs = builder.build("", "", &key, &server_cert).unwrap();

    // build a native TLS identity from the PKCS12 cert archive that can then be
    // used to setup a TLS server endpoint
    let identity = Identity::from_pkcs12(&pkcs_certs.to_der().unwrap(), "").unwrap();

    (service, identity, home_dir, crypto)
}

#[test]
fn dns_san_server() {
    const MODULE_ID: &str = "m1";
    const GENERATION_ID: &str = "g1";

    let (mut service, identity, home_dir, crypto) = init_test(MODULE_ID, GENERATION_ID);

    // start up a simple Echo server using this server cert
    let port = get_unused_tcp_port();
    println!("Test server listening on port {}", port);
    let server = run_echo_server(identity, port);
    let mut runtime = tokio::runtime::Runtime::new().unwrap();
    runtime.spawn(server);

    // run a test client that uses the module id for TLS domain name
    let client1 = run_echo_client(&mut service, port, MODULE_ID);
    runtime.block_on(client1).unwrap();

    // run a test client that uses the CN for TLS domain name
    // NOTE: Ideally, this should be a separate test, but there's some global
    // state in the HSM C library that does not get reset between multiple
    // tests in the same run and causes the test to fail.
    let client2 = run_echo_client(&mut service, port, COMMON_NAME);
    runtime.block_on(client2).unwrap();

    // cleanup
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
    home_dir.close().unwrap();
}
