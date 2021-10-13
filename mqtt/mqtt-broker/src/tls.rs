use std::{
    fs,
    path::{Path, PathBuf},
};

use chrono::{DateTime, NaiveDateTime, ParseResult, Utc};
use openssl::{
    asn1::Asn1TimeRef,
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

    pub fn from_pem(cert_path: &Path, pkey_path: &Path) -> Result<Self, ServerCertificateError> {
        let certificate = fs::read(&cert_path)
            .map_err(|e| ServerCertificateError::ReadFile(cert_path.to_path_buf(), e))?;

        let private_key = fs::read(&pkey_path)
            .map_err(|e| ServerCertificateError::ReadFile(pkey_path.to_path_buf(), e))?;

        Self::from_pem_pair(certificate, private_key)
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
    OpenSsl(#[from] openssl::error::ErrorStack),

    #[error(transparent)]
    Asn1Time(#[from] chrono::ParseError),
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use std::pin::Pin;

    use chrono::{offset::TimeZone, Utc};
    use futures_util::StreamExt;
    use openssl::{
        ssl::{SslConnector, SslMethod},
        x509::X509,
    };
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::TcpStream,
    };
    use tokio_openssl::SslStream;

    use super::{parse_openssl_time, ServerCertificate};
    use crate::transport::Transport;

    const PRIVATE_KEY: &str = include_str!("../tests/tls/pkey.pem");

    const CERTIFICATE: &str = include_str!("../tests/tls/cert.pem");

    #[tokio::test]
    async fn it_converts_into_identity() {
        const MESSAGE: &[u8] = b"it works!";

        let identity = ServerCertificate::from_pem_pair(CERTIFICATE, PRIVATE_KEY).unwrap();

        let port = run_echo_server(identity).await;
        let buffer = run_echo_client(port, MESSAGE).await;

        assert_eq!(MESSAGE, buffer.as_slice());
    }

    async fn run_echo_server(identity: ServerCertificate) -> u16 {
        let transport = Transport::new_tls("0.0.0.0:0", identity).unwrap();

        let mut incoming = transport.incoming().await.unwrap();
        let addr = incoming.local_addr().unwrap();

        tokio::spawn(async move {
            while let Some(Ok(mut stream)) = incoming.next().await {
                tokio::spawn(async move {
                    let mut buffer = [0_u8; 1024];
                    while stream.read(&mut buffer).await.unwrap() > 0 {
                        stream.write(&buffer).await.unwrap();
                    }
                });
            }
        });

        addr.port()
    }

    async fn run_echo_client(port: u16, message: &[u8]) -> Vec<u8> {
        let mut builder = SslConnector::builder(SslMethod::tls()).unwrap();

        let mut certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
        if let Some(ca) = certs.pop() {
            builder.cert_store_mut().add_cert(ca).unwrap();
        }
        let connector = builder.build();

        let addr = format!("127.0.0.1:{}", port);
        let tcp = TcpStream::connect(addr).await.unwrap();

        let config = connector.configure().unwrap();
        let ssl = config.into_ssl("localhost").unwrap();
        let mut tls = SslStream::new(ssl, tcp).unwrap();
        Pin::new(&mut tls).connect().await.unwrap();

        tls.write(message).await.unwrap();

        let mut buffer = vec![0; message.len()];
        tls.read(&mut buffer[..]).await.unwrap();

        buffer
    }

    #[test]
    fn it_converts_asn1_time() {
        let certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
        if let Some((cert, _)) = certs.split_first() {
            let not_before = parse_openssl_time(cert.not_before()).unwrap();
            assert_eq!(Utc.ymd(2020, 10, 7).and_hms(22, 40, 37), not_before);

            let not_after = super::parse_openssl_time(cert.not_after()).unwrap();
            assert_eq!(Utc.ymd(2030, 10, 5).and_hms(22, 40, 37), not_after);
        } else {
            panic!("server cert expected");
        }
    }
}
