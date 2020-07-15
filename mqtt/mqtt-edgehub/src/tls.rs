use openssl::{pkcs12::Pkcs12, pkey::PKey, stack::Stack, x509::X509};

/// Identity certificate that holds server certificate, along with its corresponding private key
/// and chain of certificates to a trusted root.
pub struct ServerCertificate(Vec<u8>);

impl ServerCertificate {
    pub fn try_from<S, K>(certificate: S, private_key: K) -> Result<Self, ServerCertificateError>
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
        let identity = pkcs_certs.to_der()?;

        Ok(ServerCertificate(identity))
    }
}

impl AsRef<[u8]> for ServerCertificate {
    fn as_ref(&self) -> &[u8] {
        &self.0
    }
}

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct ServerCertificateError(#[from] openssl::error::ErrorStack);

#[cfg(test)]
mod tests {
    use futures_util::StreamExt;
    use openssl::x509::X509;
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::{TcpListener, TcpStream},
    };
    use tokio_native_tls::{TlsAcceptor, TlsConnector};

    use super::ServerCertificate;

    const PRIVATE_KEY: &str = include_str!("../test/tls/pkey.pem");

    const CERTIFICATE: &str = include_str!("../test/tls/cert.pem");

    #[tokio::test]
    async fn it_converts_into_identity() {
        const MESSAGE: &[u8] = b"it works!";

        let identity = ServerCertificate::try_from(CERTIFICATE, PRIVATE_KEY).unwrap();

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
}
