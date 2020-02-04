// Copyright (c) Microsoft. All rights reserved.

// We disable this test on Windows in order to avoid having to add a dependency
// on OpenSSL.
#![cfg(not(windows))]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::must_use_candidate)]

use std::env;
use std::str;

use chrono::{Duration, Utc};
use failure::Fail;
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
use edgelet_core::{
    AuthId, Certificate, CertificateIssuer, CertificateProperties, CertificateType,
    CreateCertificate, MakeModuleRuntime, ModuleRuntimeErrorReason, ModuleRuntimeState,
    ModuleStatus, WorkloadConfig, IOTEDGED_CA_ALIAS,
};
use edgelet_hsm::{Crypto, HsmLock};
use edgelet_http_workload::WorkloadService;
use edgelet_test_utils::crypto::TestHsm;
use edgelet_test_utils::module::{
    TestConfig, TestModule, TestProvisioningResult, TestRuntime, TestSettings,
};
use workload::models::{CertificateResponse, ServerCertificateRequest, TrustBundleResponse};

const MODULE_ID: &str = "m1";

/// The HSM lib expects this variable to be set with home directory of the daemon.
const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";
const COMMON_NAME: &str = "staycalm";

#[derive(Clone, Copy, Debug, Fail)]
pub enum Error {
    #[fail(display = "General error")]
    General,
    #[fail(display = "Not found error")]
    NotFound,
}

impl<'a> From<&'a Error> for ModuleRuntimeErrorReason {
    fn from(err: &'a Error) -> Self {
        match err {
            Error::General => Self::Other,
            Error::NotFound => Self::NotFound,
        }
    }
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
    let crypto = Crypto::new(HsmLock::new(), 1000).unwrap();

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

    // set the correct AuthId value on the request so that authorization works
    req.extensions_mut().insert(AuthId::Value(MODULE_ID.into()));

    request(service, req)
}

fn create_workload_service(module_id: &str) -> (WorkloadService, Crypto) {
    let key_store = MemoryKeyStore::new();
    let crypto = init_crypto();
    let runtime = TestRuntime::<Error, _>::make_runtime(
        TestSettings::new(),
        TestProvisioningResult::new(),
        TestHsm::default(),
    )
    .wait()
    .unwrap()
    .with_module(Ok(TestModule::new(
        module_id.to_string(),
        TestConfig::new("img1".to_string()),
        Ok(ModuleRuntimeState::default().with_status(ModuleStatus::Running)),
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

fn run_echo_server(server_cert: Identity) -> (impl Future<Item = (), Error = ()>, u16) {
    let tcp = TcpListener::bind(
        &"127.0.0.1:0"
            .parse()
            .expect("hard-coded address is a valid SocketAddr"),
    )
    .unwrap();
    let port = tcp
        .local_addr()
        .expect("could not get local address of bound TCP listener")
        .port();
    let tls_acceptor = tokio_tls::TlsAcceptor::from(
        native_tls::TlsAcceptor::builder(server_cert)
            .build()
            .unwrap(),
    );

    let server = tcp
        .incoming()
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
        .map_err(|err| panic!("server error: {:#?}", err));
    (server, port)
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
#[cfg_attr(target_os = "macos", ignore)] // TODO: remove when macOS security framework supports opening pcks12 file with empty password
fn dns_san_server() {
    const GENERATION_ID: &str = "g1";

    let (mut service, identity, home_dir, crypto) = init_test(MODULE_ID, GENERATION_ID);

    // start up a simple Echo server using this server cert
    let (server, port) = run_echo_server(identity);
    println!("Test server listening on port {}", port);
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
