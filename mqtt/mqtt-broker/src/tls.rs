use std::{
    fs,
    path::{Path, PathBuf},
};

use chrono::{DateTime, NaiveDateTime, ParseResult, Utc};
use openssl::{
    asn1::Asn1TimeRef,
    pkcs12::Pkcs12,
    pkey::{PKey, Private},
    x509::X509,
};

/// Identity certificate that holds server certificate, along with its corresponding private key
/// and chain of certificates to a trusted root.
#[derive(Debug)]
pub struct ServerCertificate {
    private_key: PKey<Private>,
    certificate: X509,
    chain: Option<Vec<X509>>,
    ca: Option<X509>,
    not_before: DateTime<Utc>,
    not_after: DateTime<Utc>,
}

impl ServerCertificate {
    pub fn from_pem_pair<S, K>(
        certificate: S,
        private_key: K,
    ) -> Result<Self, ServerCertificateError>
    where
        S: AsRef<[u8]>,
        K: AsRef<[u8]>,
    {
        // load all the certs returned by the workload API
        let mut chain = X509::stack_from_pem(certificate.as_ref())?;

        // the first cert is the server cert and the other certs are part of
        // the CA chain; we skip the server cert and build an OpenSSL cert
        // stack with the other certs
        let certificate = chain.remove(0);

        // load the private key for the server cert
        let private_key = PKey::private_key_from_pem(private_key.as_ref())?;

        // the root of the server cert is the CA, and we expect the client cert
        // to be signed by this same CA.
        let ca = chain.last().cloned();

        let not_before = parse_openssl_time(certificate.not_before())?;
        let not_after = parse_openssl_time(certificate.not_after())?;

        let identity = Self {
            private_key,
            certificate,
            chain: Some(chain),
            ca,
            not_before,
            not_after,
        };

        Ok(identity)
    }

    pub fn from_pkcs12(path: &Path) -> Result<Self, ServerCertificateError> {
        let cert_buffer =
            fs::read(&path).map_err(|e| ServerCertificateError::ReadFile(path.to_path_buf(), e))?;

        let pksc12 = Pkcs12::from_der(&cert_buffer)?;
        let parts = pksc12.parse("")?;

        let chain = parts
            .chain
            .map(|chain| chain.into_iter().collect::<Vec<_>>());
        let ca = chain.as_ref().and_then(|chain| chain.last().cloned());

        let not_before = parse_openssl_time(parts.cert.not_before())?;
        let not_after = parse_openssl_time(parts.cert.not_after())?;

        let identity = Self {
            private_key: parts.pkey,
            certificate: parts.cert,
            chain,
            ca,
            not_before,
            not_after,
        };
        Ok(identity)
    }

    pub fn into_parts(self) -> (PKey<Private>, X509, Option<Vec<X509>>, Option<X509>) {
        (self.private_key, self.certificate, self.chain, self.ca)
    }

    pub fn not_before(&self) -> DateTime<Utc> {
        self.not_before
    }

    pub fn not_after(&self) -> DateTime<Utc> {
        self.not_after
    }
}

/// Coverts `openssl::asn1::Asn1TimeRef` into `chrono::DateTime<chrono::Utc>`.
///
/// `openssl::asn1::Asn1TimeRef` does not expose any way to convert the `ASN1_TIME` to a Rust-friendly type.
/// Its Display impl uses `ASN1_TIME_print`, so we convert it into a String and parse it back
/// into a `chrono::DateTime<chrono::Utc>`
pub fn parse_openssl_time(time: &Asn1TimeRef) -> ParseResult<DateTime<Utc>> {
    let time = time.to_string();
    let time = NaiveDateTime::parse_from_str(&time, "%b %e %H:%M:%S %Y GMT")?;
    Ok(DateTime::<Utc>::from_utc(time, Utc))
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateError {
    #[error("unable to read file content {0}")]
    ReadFile(PathBuf, #[source] std::io::Error),

    #[error(transparent)]
    OpenSSL(#[from] openssl::error::ErrorStack),

    #[error(transparent)]
    Asn1Time(#[from] chrono::ParseError),
}

#[cfg(test)]
mod tests {
    // use chrono::{offset::TimeZone, Utc};
    // use futures_util::StreamExt;
    // use openssl::x509::X509;
    // use tokio::{
    //     io::{AsyncReadExt, AsyncWriteExt},
    //     net::{TcpListener, TcpStream},
    // };
    // use tokio_native_tls::{TlsAcceptor, TlsConnector};

    // use super::{parse_openssl_time, ServerCertificate};

    // const PRIVATE_KEY: &str = include_str!("../test/tls/pkey.pem");

    // const CERTIFICATE: &str = include_str!("../test/tls/cert.pem");

    // #[tokio::test]
    // async fn it_converts_into_identity() {
    //     const MESSAGE: &[u8] = b"it works!";

    //     let identity = ServerCertificate::from_pem_pair(CERTIFICATE, PRIVATE_KEY).unwrap();

    //     let port = run_echo_server(identity).await;
    //     let buffer = run_echo_client(port, MESSAGE).await;

    //     assert_eq!(MESSAGE, buffer.as_slice());
    // }

    // async fn run_echo_server(identity: ServerCertificate) -> u16 {
    //     // TODO fix test
    //     let pkcs12 = native_tls::Identity::from_pkcs12(identity.as_ref(), "").unwrap();
    //     let acceptor = TlsAcceptor::from(native_tls::TlsAcceptor::new(pkcs12).unwrap());

    //     let mut listener = TcpListener::bind("0.0.0.0:0").await.unwrap();
    //     let port = listener.local_addr().unwrap().port();

    //     tokio::spawn(async move {
    //         while let Some(stream) = listener.next().await {
    //             let acceptor = acceptor.clone();
    //             tokio::spawn(async move {
    //                 let mut tls = acceptor.accept(stream.unwrap()).await.unwrap();
    //                 let mut buffer = [0_u8; 1024];
    //                 while tls.read(&mut buffer).await.unwrap() > 0 {
    //                     tls.write(&buffer).await.unwrap();
    //                 }
    //             });
    //         }
    //     });

    //     port
    // }

    // async fn run_echo_client(port: u16, message: &[u8]) -> Vec<u8> {
    //     let mut builder = native_tls::TlsConnector::builder();

    //     let mut certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
    //     for cert in certs.split_off(1) {
    //         let cert = native_tls::Certificate::from_der(&cert.to_der().unwrap()).unwrap();
    //         builder.add_root_certificate(cert);
    //     }

    //     let connector = TlsConnector::from(builder.build().unwrap());

    //     let addr = format!("127.0.0.1:{}", port);
    //     let tcp = TcpStream::connect(addr).await.unwrap();
    //     let mut tls = connector.connect("localhost", tcp).await.unwrap();

    //     tls.write(message.as_ref()).await.unwrap();

    //     let mut buffer = vec![0; message.len()];
    //     tls.read(&mut buffer[..]).await.unwrap();

    //     buffer
    // }

    // #[test]
    // fn it_converts_asn1_time() {
    //     let certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
    //     if let Some((cert, _)) = certs.split_first() {
    //         let not_before = parse_openssl_time(cert.not_before()).unwrap();
    //         assert_eq!(Utc.ymd(2020, 7, 9).and_hms(21, 29, 53), not_before);

    //         let not_after = super::parse_openssl_time(cert.not_after()).unwrap();
    //         assert_eq!(Utc.ymd(2020, 10, 7).and_hms(20, 10, 2), not_after);
    //     } else {
    //         panic!("server cert expected");
    //     }
    // }
}
