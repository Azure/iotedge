// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use edgelet_hsm::{Crypto, HsmLock};
use edgelet_http::{PemCertificate};

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::{
    CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate,
    IOTEDGED_CA_ALIAS,
};
mod test_utils;
use test_utils::TestHSMEnvSetup;
use edgelet_test_utils::get_unused_tcp_port;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

#[test]
fn crypto_create_cert_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock).unwrap();

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
    let server_cert_alias = String::from("test-server-cert");
    let props = CertificateProperties::new(
        3600,
        "test-iotedge-client".to_string(),
        CertificateType::Client,
        client_cert_alias.clone(),
    );
    let client_cert = crypto.create_certificate(&props).unwrap();

    let props = CertificateProperties::new(
        3600,
        "localhost".to_string(),
        CertificateType::Server,
        "test-server-cert".to_string(),
    );
    let _server_cert = crypto.create_certificate(&props).unwrap();

    let _pem_cert = PemCertificate::from(&client_cert).unwrap();

    // cleanup
    crypto.destroy_certificate(client_cert_alias).unwrap();
    crypto.destroy_certificate(server_cert_alias).unwrap();
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn route1(
    _req: Request<Body>,
    _params: Parameters,
) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = Response::builder()
        .status(StatusCode::OK)
        .body("Hello World!".into())
        .unwrap();
    Box::new(future::ok(response))
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

fn test_server() {
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