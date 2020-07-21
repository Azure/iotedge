use chrono::{DateTime, NaiveDateTime, ParseResult, Utc};
use native_tls::Identity;
use openssl::{asn1::Asn1TimeRef, pkcs12::Pkcs12, pkey::PKey, stack::Stack, x509::X509};
use std::{
    convert::TryFrom,
    fs,
    path::{Path, PathBuf},
};

/// Identity certificate that holds server certificate, along with its corresponding private key
/// and chain of certificates to a trusted root.
#[derive(Debug)]
pub struct ServerCertificate {
    der: Vec<u8>,
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
        let mut certs = X509::stack_from_pem(certificate.as_ref())?;

        // the first cert is the server cert and the other certs are part of the CA
        // chain; we skip the server cert and build an OpenSSL cert stack with the
        // other certs
        let mut ca_certs = Stack::new()?;
        for cert in certs.split_off(1) {
            ca_certs.push(cert)?;
        }

        // load the private key for the server cert
        let key = PKey::private_key_from_pem(private_key.as_ref())?;

        // build a PKCS12 cert archive that includes:
        //  - the server cert
        //  - the private key for the server cert
        //  - all the other certs that are part of the CA chain
        let server_cert = &certs[0];
        let mut builder = Pkcs12::builder();
        builder.ca(ca_certs);
        let pkcs_certs = builder.build("", "", &key, &server_cert)?;

        // build a native TLS identity from the PKCS12 cert archive that can then be
        // used to setup a TLS server endpoint
        let identity = ServerCertificate {
            der: pkcs_certs.to_der()?,
            not_before: parse_openssl_time(server_cert.not_before())?,
            not_after: parse_openssl_time(server_cert.not_after())?,
        };

        Ok(identity)
    }

    pub fn from_file(path: &Path) -> Result<Self, ServerCertificateError> {
        let cert_buffer =
            fs::read(&path).map_err(|e| ServerCertificateError::ReadFile(path.to_path_buf(), e))?;

        let pksc12 = Pkcs12::from_der(&cert_buffer)?;
        let parts = pksc12.parse("")?;

        let identity = ServerCertificate {
            der: pksc12.to_der()?,
            not_before: parse_openssl_time(parts.cert.not_before())?,
            not_after: parse_openssl_time(parts.cert.not_after())?,
        };

        Ok(identity)
    }

    pub fn not_before(&self) -> DateTime<Utc> {
        self.not_before
    }

    pub fn not_after(&self) -> DateTime<Utc> {
        self.not_after
    }
}

impl TryFrom<ServerCertificate> for Identity {
    type Error = DecodeIdentityError;

    fn try_from(value: ServerCertificate) -> Result<Self, Self::Error> {
        let identity = Identity::from_pkcs12(&value.der, "")?;
        Ok(identity)
    }
}

impl AsRef<[u8]> for ServerCertificate {
    fn as_ref(&self) -> &[u8] {
        &self.der
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

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct DecodeIdentityError(#[from] native_tls::Error);

#[cfg(test)]
mod tests {
    use chrono::{offset::TimeZone, Utc};
    use futures_util::StreamExt;
    use openssl::x509::X509;
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::{TcpListener, TcpStream},
    };
    use tokio_native_tls::{TlsAcceptor, TlsConnector};

    use super::{parse_openssl_time, ServerCertificate};

    const PRIVATE_KEY: &str = include_str!("../test/tls/pkey.pem");

    const CERTIFICATE: &str = include_str!("../test/tls/cert.pem");

    #[tokio::test]
    async fn it_converts_into_identity() {
        const MESSAGE: &[u8] = b"it works!";

        let identity = ServerCertificate::from_pem_pair(CERTIFICATE, PRIVATE_KEY).unwrap();

        let port = run_echo_server(identity).await;
        let buffer = run_echo_client(port, MESSAGE).await;

        assert_eq!(MESSAGE, buffer.as_slice());
    }

    async fn run_echo_server(identity: ServerCertificate) -> u16 {
        let pkcs12 = native_tls::Identity::from_pkcs12(identity.as_ref(), "").unwrap();
        let acceptor = TlsAcceptor::from(native_tls::TlsAcceptor::new(pkcs12).unwrap());

        let mut listener = TcpListener::bind("0.0.0.0:0").await.unwrap();
        let port = listener.local_addr().unwrap().port();

        tokio::spawn(async move {
            while let Some(stream) = listener.next().await {
                let acceptor = acceptor.clone();
                tokio::spawn(async move {
                    let mut tls = acceptor.accept(stream.unwrap()).await.unwrap();
                    let mut buffer = [0_u8; 1024];
                    while tls.read(&mut buffer).await.unwrap() > 0 {
                        tls.write(&buffer).await.unwrap();
                    }
                });
            }
        });

        port
    }

    async fn run_echo_client(port: u16, message: &[u8]) -> Vec<u8> {
        let mut builder = native_tls::TlsConnector::builder();

        let mut certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
        for cert in certs.split_off(1) {
            let cert = native_tls::Certificate::from_der(&cert.to_der().unwrap()).unwrap();
            builder.add_root_certificate(cert);
        }

        let connector = TlsConnector::from(builder.build().unwrap());

        let addr = format!("127.0.0.1:{}", port);
        let tcp = TcpStream::connect(addr).await.unwrap();
        let mut tls = connector.connect("localhost", tcp).await.unwrap();

        tls.write(message.as_ref()).await.unwrap();

        let mut buffer = vec![0; message.len()];
        tls.read(&mut buffer[..]).await.unwrap();

        buffer
    }

    #[test]
    fn it_converts_asn1_time() {
        let certs = X509::stack_from_pem(CERTIFICATE.as_ref()).unwrap();
        if let Some((cert, _)) = certs.split_first() {
            let not_before = parse_openssl_time(cert.not_before()).unwrap();
            assert_eq!(Utc.ymd(2020, 7, 9).and_hms(21, 29, 53), not_before);

            let not_after = super::parse_openssl_time(cert.not_after()).unwrap();
            assert_eq!(Utc.ymd(2020, 10, 7).and_hms(20, 10, 2), not_after);
        } else {
            panic!("server cert expected");
        }
    }
}
