// Copyright (c) Microsoft. All rights reserved.

use std::cmp;
use std::sync::{Arc, Mutex};

use chrono::{DateTime, Utc};
use failure::{Fail, ResultExt};
use futures::future::{self, Future, IntoFuture};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};

use cert_client::client::CertificateClient;
use edgelet_core::{
    Certificate as CoreCertificate, CertificateProperties, CertificateType, KeyBytes,
    PrivateKey as CorePrivateKey,
};
use edgelet_http::Error as HttpError;
use edgelet_utils::ensure_not_empty_with_context;
use openssl::error::ErrorStack;
use workload::models::{CertificateResponse, PrivateKey as PrivateKeyResponse};

use crate::{
    error::{Error, ErrorKind, Result},
    IntoResponse,
};

mod identity;
mod server;

pub use self::identity::IdentityCertHandler;
pub use self::server::ServerCertHandler;

// 5 mins
const AZIOT_EDGE_CA_CERT_MIN_DURATION_SECS: i64 = 5 * 60;

// 180 days
const AZIOT_EDGE_CA_CERT_MAX_DURATION_SECS: u64 = 180 * 24 * 3600;

// Workload CA alias
const IOTEDGED_CA_ALIAS: &str = "iotedged-workload-ca";

// Workload CA CN
const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";

#[derive(Clone)]
struct EdgeCaCertificate {
    cert_id: String,
    key_id: String,
}

fn cert_to_response<T: CoreCertificate>(
    cert: &T,
    context: ErrorKind,
) -> Result<CertificateResponse> {
    let cert_buffer = match cert.pem() {
        Ok(cert_buffer) => cert_buffer,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let expiration = match cert.get_valid_to() {
        Ok(expiration) => expiration,
        Err(err) => return Err(Error::from(err.context(context))),
    };

    let private_key = match cert.get_private_key() {
        Ok(Some(CorePrivateKey::Ref(ref_))) => {
            PrivateKeyResponse::new("ref".to_string()).with_ref(ref_)
        }
        Ok(Some(CorePrivateKey::Key(KeyBytes::Pem(buffer)))) => {
            PrivateKeyResponse::new("key".to_string())
                .with_bytes(String::from_utf8_lossy(buffer.as_ref()).to_string())
        }
        Ok(None) => return Err(ErrorKind::BadPrivateKey.into()),
        Err(err) => return Err(Error::from(err.context(context))),
    };

    Ok(CertificateResponse::new(
        private_key,
        String::from_utf8_lossy(cert_buffer.as_ref()).to_string(),
        expiration.to_rfc3339(),
    ))
}

fn compute_validity(expiration: &str, max_duration_sec: i64, context: ErrorKind) -> Result<i64> {
    ensure_not_empty_with_context(expiration, || context.clone())?;

    let expiration = DateTime::parse_from_rfc3339(expiration).context(context)?;

    let secs = expiration
        .with_timezone(&Utc)
        .signed_duration_since(Utc::now())
        .num_seconds();

    Ok(cmp::min(secs, max_duration_sec))
}

fn refresh_cert(
    key_connector: &http_common::Connector,
    cert_client: Arc<Mutex<CertificateClient>>,
    alias: String,
    new_cert_props: &CertificateProperties,
    edge_ca: EdgeCaCertificate,
    context: &ErrorKind,
) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
    let context = context.clone();
    let key_connector = key_connector.clone();

    let response = generate_local_keypair()
        .map(|(privkey, pubkey)| {
            create_csr(new_cert_props, &privkey, &pubkey)
                .map(|csr| (privkey, csr))
                .map_err(|e| Error::from(e.context(context.clone())))
        })
        .map_err(|e| Error::from(e.context(context.clone())))
        .into_future()
        .flatten()
        .and_then(move |(new_cert_privkey, new_cert_csr)| {
            let edge_ca = edge_ca.clone();
            let context_copy = context.clone();
            prepare_edge_ca(&key_connector, &cert_client, &edge_ca, &context)
                .map_err(move |e| Error::from(e.context(context_copy)))
                .and_then(move |_| {
                    let key_client = {
                        let key_client = aziot_key_client::Client::new(
                            aziot_key_common_http::ApiVersion::V2020_09_01,
                            key_connector,
                        );
                        Arc::new(key_client)
                    };
                    key_client
                        .load_key_pair(edge_ca.key_id.as_str())
                        .map_err(|e| Error::from(e.context(context.clone())))
                        .and_then(move |aziot_edged_ca_key_pair_handle| -> Result<_> {
                            let context_copy = context.clone();
                            let response = cert_client
                                .lock()
                                .expect("certificate client lock error")
                                .create_cert(
                                    &alias,
                                    &new_cert_csr,
                                    Some((
                                        edge_ca.cert_id.as_str(),
                                        &aziot_edged_ca_key_pair_handle,
                                    )),
                                )
                                .map_err(move |e| Error::from(e.context(context_copy)))
                                .and_then(move |cert| {
                                    let context_copy = context.clone();

                                    let pk = new_cert_privkey
                                        .private_key_to_pem_pkcs8()
                                        .context(context.clone())?;
                                    let cert = Certificate::new(cert, pk);
                                    let cert = cert_to_response(&cert, context_copy.clone())?;
                                    let body = match serde_json::to_string(&cert) {
                                        Ok(body) => body,
                                        Err(err) => {
                                            return Err(Error::from(err.context(context_copy)))
                                        }
                                    };

                                    let response = Response::builder()
                                        .status(StatusCode::CREATED)
                                        .header(CONTENT_TYPE, "application/json")
                                        .header(CONTENT_LENGTH, body.len().to_string().as_str())
                                        .body(body.into())
                                        .context(context_copy)?;

                                    Ok(response)
                                });
                            Ok(response)
                        })
                })
        })
        .flatten()
        .or_else(|e| Ok(e.into_response()));

    Box::new(response)
}

fn generate_local_keypair() -> std::result::Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    ErrorStack,
> {
    let rsa = openssl::rsa::Rsa::generate(2048)?;
    let privkey = openssl::pkey::PKey::from_rsa(rsa)?;
    let pubkey = privkey.public_key_to_pem()?;
    let pubkey: openssl::pkey::PKey<openssl::pkey::Public> =
        openssl::pkey::PKey::public_key_from_pem(&pubkey)?;

    Ok((privkey, pubkey))
}

fn load_keypair(
    ca_key_pair_handle: &aziot_key_common::KeyHandle,
    key_engine: &mut openssl2::FunctionalEngine,
) -> std::result::Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    ErrorStack,
> {
    let (workload_ca_private_key, workload_ca_public_key) = {
        let workload_ca_key_pair_handle =
            std::ffi::CString::new(ca_key_pair_handle.0.clone()).unwrap();
        let workload_ca_public_key = key_engine
            .load_public_key(&workload_ca_key_pair_handle)
            .unwrap();
        let workload_ca_private_key = key_engine
            .load_private_key(&workload_ca_key_pair_handle)
            .unwrap();
        (workload_ca_private_key, workload_ca_public_key)
    };

    Ok((workload_ca_private_key, workload_ca_public_key))
}

fn create_csr(
    props: &CertificateProperties,
    private_key: &openssl::pkey::PKeyRef<openssl::pkey::Private>,
    public_key: &openssl::pkey::PKeyRef<openssl::pkey::Public>,
) -> std::result::Result<Vec<u8>, ErrorStack> {
    let mut csr = openssl::x509::X509Req::builder()?;

    csr.set_version(2)?;

    let mut subject_name = openssl::x509::X509Name::builder()?;
    subject_name.append_entry_by_text("CN", props.common_name())?;
    let subject_name = subject_name.build();
    csr.set_subject_name(&subject_name)?;

    csr.set_pubkey(&public_key)?;

    let mut extensions = openssl::stack::Stack::new()?;
    let mut extended_key_usage = openssl::x509::extension::ExtendedKeyUsage::new();
    let mut basic_constraints = openssl::x509::extension::BasicConstraints::new();
    let mut key_usage = openssl::x509::extension::KeyUsage::new();

    match *props.certificate_type() {
        edgelet_core::CertificateType::Client => {
            extended_key_usage.client_auth();

            extensions.push(extended_key_usage.build()?)?;
        }
        edgelet_core::CertificateType::Server => {
            extended_key_usage.server_auth();

            extensions.push(extended_key_usage.build()?)?;
        }
        edgelet_core::CertificateType::Ca => {
            basic_constraints.ca().critical().pathlen(0);
            key_usage.critical().digital_signature().key_cert_sign();

            extensions.push(basic_constraints.build()?)?;
            extensions.push(key_usage.build()?)?;
        }
        edgelet_core::CertificateType::Unknown => {}
    }

    if props.dns_san_entries().is_some() || props.ip_entries().is_some() {
        let mut subject_alt_name = openssl::x509::extension::SubjectAlternativeName::new();
        props.dns_san_entries().into_iter().flatten().for_each(|s| {
            subject_alt_name.dns(s);
        });
        props.ip_entries().into_iter().flatten().for_each(|s| {
            subject_alt_name.ip(s);
        });
        let san = subject_alt_name.build(&csr.x509v3_context(None))?;
        extensions.push(san)?;
    }

    csr.add_extensions(&extensions)?;

    csr.sign(&private_key, openssl::hash::MessageDigest::sha256())?;

    let csr = csr.build();
    let csr = csr.to_pem()?;

    Ok(csr)
}

fn prepare_edge_ca(
    key_connector: &http_common::Connector,
    cert_client: &Arc<Mutex<CertificateClient>>,
    ca: &EdgeCaCertificate,
    context: &ErrorKind,
) -> Box<dyn Future<Item = (), Error = Error> + Send> {
    let key_connector_copy = key_connector.clone();
    let cert_client_copy = cert_client.clone();
    let context = context.clone();
    let ca = ca.clone();

    let fut = cert_client
        .lock()
        .expect("certificate client lock error")
        .get_cert(&ca.cert_id)
        .then(move |result| -> Result<_> {
            let ca_cert_key_handle = match result {
                Ok(cert) => {
                    // Check expiration
                    let cert = openssl::x509::X509::from_pem(cert.as_ref())
                        .map_err(|e| Error::from(e.context(context.clone())))?;

                    let epoch =
                        openssl::asn1::Asn1Time::from_unix(0).expect("unix epoch must be valid");

                    let diff = epoch
                        .diff(&cert.not_after())
                        .map_err(|e| Error::from(e.context(context.clone())))?;
                    let diff = i64::from(diff.secs) + i64::from(diff.days) * 86400;

                    if diff < AZIOT_EDGE_CA_CERT_MIN_DURATION_SECS {
                        // Recursively call generate_key_and_csr to renew CA cert
                        future::Either::A(future::Either::A(
                            create_edge_ca_certificate(
                                &key_connector_copy,
                                &cert_client_copy,
                                &ca,
                                &context,
                            )
                            .map_err(move |e| Error::from(e.context(context)))
                            .map(|_| ()),
                        ))
                    } else {
                        // Edge CA certificate keypair should exist if certificate exists
                        future::Either::A(future::Either::B(futures::future::ok(())))
                    }
                }
                Err(_e) => {
                    // Recursively call generate_key_and_csr to renew CA cert
                    future::Either::B(
                        create_edge_ca_certificate(
                            &key_connector_copy,
                            &cert_client_copy,
                            &ca,
                            &context,
                        )
                        .map_err(move |e| Error::from(e.context(context)))
                        .map(|_| ()),
                    )
                }
            };

            Ok(ca_cert_key_handle)
        })
        .flatten();

    Box::new(fut)
}

fn create_key_engine(
    key_connector: &http_common::Connector,
) -> std::result::Result<openssl2::FunctionalEngine, openssl2::Error> {
    let key_client = aziot_key_client::Client::new(
        aziot_key_common_http::ApiVersion::V2020_09_01,
        key_connector.clone(),
    );
    let key_client = Arc::new(key_client);

    aziot_key_openssl_engine::load(key_client)
}

fn create_edge_ca_certificate(
    key_connector: &http_common::Connector,
    cert_client: &Arc<Mutex<CertificateClient>>,
    ca: &EdgeCaCertificate,
    context: &ErrorKind,
) -> Box<dyn Future<Item = (), Error = Error> + Send> {
    let ca = ca.clone();
    let cert_client = cert_client.clone();
    let context = context.clone();

    let edgelet_ca_props = CertificateProperties::new(
        AZIOT_EDGE_CA_CERT_MAX_DURATION_SECS,
        IOTEDGED_COMMONNAME.to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    );

    let key_client = {
        let key_client = aziot_key_client::Client::new(
            aziot_key_common_http::ApiVersion::V2020_09_01,
            key_connector.clone(),
        );
        Arc::new(key_client)
    };

    //generate new key in Key Service, generate csr for Edge CA certificate, import into Cert Service
    let fut = create_key_engine(&key_connector)
        .map_err(|e| Error::from(e.context(context.clone())))
        .and_then(|mut key_engine| {
            key_client
                .create_key_pair_if_not_exists(ca.key_id.as_str(), Some("rsa-2048:*"))
                .map_err(|e| Error::from(e.context(context.clone())))
                .and_then(|ca_key_pair_handle| {
                    load_keypair(&ca_key_pair_handle, &mut key_engine)
                        .map_err(|e| Error::from(e.context(context.clone())))
                        .and_then(|(privkey, pubkey)| {
                            create_csr(&edgelet_ca_props, &privkey, &pubkey)
                                .map_err(|e| Error::from(e.context(context.clone())))
                        })
                })
        })
        .into_future()
        .and_then(move |new_cert_csr| {
            cert_client
                .lock()
                .expect("certificate client lock error")
                .create_cert(ca.cert_id.as_str(), &new_cert_csr, None)
                .map_err(|e| Error::from(e.context(context)))
                .map(|_| ())
        });

    Box::new(fut)
}

#[derive(Debug)]
pub struct Certificate {
    pem: Vec<u8>,
    private_key: Vec<u8>,
}

impl Certificate {
    pub fn new(pem: Vec<u8>, private_key: Vec<u8>) -> Certificate {
        Certificate { pem, private_key }
    }
}

impl CoreCertificate for Certificate {
    type Buffer = Vec<u8>;
    type KeyBuffer = Vec<u8>;

    fn pem(&self) -> std::result::Result<Self::Buffer, edgelet_core::Error> {
        Ok(self.pem.clone())
    }

    fn get_private_key(
        &self,
    ) -> std::result::Result<Option<CorePrivateKey<Self::KeyBuffer>>, edgelet_core::Error> {
        Ok(Some(CorePrivateKey::Key(KeyBytes::Pem(
            self.private_key.clone(),
        ))))
    }

    fn get_valid_to(&self) -> std::result::Result<DateTime<Utc>, edgelet_core::Error> {
        fn parse_openssl_time(
            time: &openssl::asn1::Asn1TimeRef,
        ) -> chrono::ParseResult<chrono::DateTime<chrono::Utc>> {
            // openssl::asn1::Asn1TimeRef does not expose any way to convert the ASN1_TIME to a Rust-friendly type
            //
            // Its Display impl uses ASN1_TIME_print, so we convert it into a String and parse it back
            // into a chrono::DateTime<chrono::Utc>
            let time = time.to_string();
            let time = chrono::NaiveDateTime::parse_from_str(&time, "%b %e %H:%M:%S %Y GMT")?;
            Ok(chrono::DateTime::<chrono::Utc>::from_utc(time, chrono::Utc))
        }

        let cert = openssl::x509::X509::from_pem(&self.pem)
            .map_err(|_| edgelet_core::Error::from(edgelet_core::ErrorKind::CertificateCreate))?;
        let not_after = parse_openssl_time(cert.not_after())
            .map_err(|_| edgelet_core::Error::from(edgelet_core::ErrorKind::ParseSince))?;
        Ok(not_after)
    }

    fn get_common_name(&self) -> std::result::Result<String, edgelet_core::Error> {
        unimplemented!()
    }
}
