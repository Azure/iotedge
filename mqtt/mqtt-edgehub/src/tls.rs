use openssl::{pkcs12::Pkcs12, pkey::PKey, stack::Stack, x509::X509};

pub struct Identity(Vec<u8>);

impl Identity {
    pub fn try_from<S, K>(certificate: S, private_key: K) -> Result<Self, IdentityError>
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

        Ok(Identity(identity))
    }
}

impl AsRef<[u8]> for Identity {
    fn as_ref(&self) -> &[u8] {
        &self.0
    }
}

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct IdentityError(#[from] openssl::error::ErrorStack);

#[cfg(test)]
mod tests {
    use futures_util::StreamExt;
    use openssl::x509::X509;
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::{TcpListener, TcpStream},
    };
    use tokio_native_tls::{TlsAcceptor, TlsConnector};

    use super::Identity;

    #[tokio::test]
    async fn it_converts_into_identity() {
        let identity = Identity::try_from(CERTIFICATE, PRIVATE_KEY).unwrap();

        let port = run_echo_server(identity).await;
        run_echo_client(port).await;
    }

    async fn run_echo_server(identity: Identity) -> u16 {
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

    async fn run_echo_client(port: u16) {
        const MESSAGE: &[u8] = b"it works!";

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

        tls.write(MESSAGE).await.unwrap();
        let mut buffer = vec![0; MESSAGE.len()];
        tls.read(&mut buffer[..]).await.unwrap();

        assert_eq!(MESSAGE, buffer.as_slice());
    }

    const CERTIFICATE: &str = "-----BEGIN CERTIFICATE-----\n\
        MIIEbjCCAlagAwIBAgIEdLDcUTANBgkqhkiG9w0BAQsFADAfMR0wGwYDVQQDDBRp\n\
        b3RlZGdlZCB3b3JrbG9hZCBjYTAeFw0yMDA3MDkyMTI5NTNaFw0yMDEwMDcyMDEw\n\
        MDJaMBQxEjAQBgNVBAMMCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEP\n\
        ADCCAQoCggEBAME8iPiXdDF1fS3Ppq53HOD01BQqkDlhv5+8Lxwrz8Hz+K9dM7q4\n\
        xlXjywYeY/f6W7vL4vjxUbBSw3e0L7+X+UShwco8vwbiQqjbNfjAz95rlRwcrfff\n\
        xl04+GEcy7Uahrv2143s32CIPtYKEgUH0HVdRxBh6KrwWjCQuUfsysoxHsM1KqPI\n\
        5p4Gpp87Y4uRkX248IriJz3ap2+LWuAgV54VzuzAMx0SH9Mbgv0/g2k18PfoqJM5\n\
        mCktU88brojoOx6SGOu/kXpT3KmWXmckVMKakjqFERa9GXTe+jggFUS7uVIKY2dz\n\
        cV2fSnX9CiMRvXrtUKHrIg1qH2SPUsRYtgMCAwEAAaOBvDCBuTAJBgNVHRMEAjAA\n\
        MA4GA1UdDwEB/wQEAwID+DATBgNVHSUEDDAKBggrBgEFBQcDATAdBgNVHREEFjAU\n\
        gglsb2NhbGhvc3SCB2VkZ2VodWIwHQYDVR0OBBYEFNiOqy/sZzR6MHKk6pYU0SlR\n\
        z7TGMEkGA1UdIwRCMECAFAbOyQy9xAl46d39FhI0dQXohbRPoSKkIDAeMRwwGgYD\n\
        VQQDDBNUZXN0IEVkZ2UgRGV2aWNlIENBggRri0VnMA0GCSqGSIb3DQEBCwUAA4IC\n\
        AQBGZvInLdwQ9mKMcwM2E6kjIWuCcBOb1HXEswyDc8IKmtJS504s6Ybir/+Pb30m\n\
        rfeWfoMWMP8GS4UeIm9T0zzYFBuDcwqmsQxLZLSigUMmXryEwt6zp1ksZSIEIkFi\n\
        mKNFLuJSzPmLFFACsNQwsgl3qG2qqaMhOrRDEl/OH57tCFbLFVnSLWwB3XX4CsF3\n\
        vN/3Ys+Bf4Y1gtY6gctByI6NCimQQYEaC1BygSUh/nwyjlAy1H8Vu+8+TymJ0KHK\n\
        eee+y/9OkCxUqPHDHmE6JKVefkNwqbb6w+Sl9MQZXRVepNfuTzVF3iTyKu4SARPE\n\
        w19SRlNEfKM+W9U/T0shv3ay0W+3dry/5eY5nX6nuKx2Tt56iC5bjhCpUmsKuWoU\n\
        XGE7z48ZhG2qwPIlNIbTzKFvXL4AXGEhoCot7xPwohTwPUxuDAYGibAB9BKjm3/0\n\
        NgAPXqT82xpwX//mtRAoLFpSGct3E62KiLZD+RJnoC5A2X7KnQKnQndmEHwKGotS\n\
        GJ1GZwU99C+kuG9MD+aNZJZBozcdoRZKT56438J25pOemjTy4MjFs+t3nWe4jtK8\n\
        /gKeDoonQcvGbHR6+ukI+BDgyQwe+jvulA5ESanERONm42bnmZUuXxp2pZYKiB6q\n\
        ov2gTgQyaRE8rbX4SSPZghE5km7p6FAIjm/uqU9kGMUk3A==\n\
        -----END CERTIFICATE-----\n\
        -----BEGIN CERTIFICATE-----\n\
        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\n\
        ym6+PbkEtiBLlE8wRR1yJQ==\n\
        -----END CERTIFICATE-----\n\
        -----BEGIN CERTIFICATE-----\n\
        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\n\
        Aej6YZlBIq1eGVQFeug=\n\
        -----END CERTIFICATE-----\n\
        -----BEGIN CERTIFICATE-----\n\
        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\n\
        6dXExloxXMEiritdew==\n\
        -----END CERTIFICATE-----\n";

    const PRIVATE_KEY: &str = "-----BEGIN PRIVATE KEY-----\n\
        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDBPIj4l3QxdX0t\n\
        z6audxzg9NQUKpA5Yb+fvC8cK8/B8/ivXTO6uMZV48sGHmP3+lu7y+L48VGwUsN3\n\
        tC+/l/lEocHKPL8G4kKo2zX4wM/ea5UcHK3338ZdOPhhHMu1Goa79teN7N9giD7W\n\
        ChIFB9B1XUcQYeiq8FowkLlH7MrKMR7DNSqjyOaeBqafO2OLkZF9uPCK4ic92qdv\n\
        i1rgIFeeFc7swDMdEh/TG4L9P4NpNfD36KiTOZgpLVPPG66I6Dsekhjrv5F6U9yp\n\
        ll5nJFTCmpI6hREWvRl03vo4IBVEu7lSCmNnc3Fdn0p1/QojEb167VCh6yINah9k\n\
        j1LEWLYDAgMBAAECggEAQKYmFTVmlF2AYeFFHL2RFdoTUiPjWK9RUvm8sSofOf6L\n\
        xu/hrKjBAl9Rv1xPidli83OFHlBuShWGe/f2uZz4snODyEuGhaERODkO14h6gylv\n\
        G5akxXdCgFHdF3Bw3shfJ2ewOjVzjnJGw4le+fvhTELG0b5P/1Cme/UPZ/ba6cXs\n\
        TrGyWJk4ef/IevrObf0DPh5XvqDYM8NiRCL19o5ZXK2w9Z1QJva7o2A0OrQ6Dkfa\n\
        avPXl242xd6c06f59akUi4CFNVq2dPdRRmadQkb64Sg2gJ+oxrnj+nZ3BRA9uZ1q\n\
        96cK1/QjFjjB++wALYcCUB9uEWpZgstFuhNAHZIkEQKBgQDo/Y/A/Zq7T/LHM1fU\n\
        VCcmMwtDAVEa69mnIy1rTM1WrZF3lJdj/LR3Ie7cYVa7YZu6CwZZCCHMpJiTOEa5\n\
        A+8ruOFZzk7dRIHbyUEKFJ5cOXW8U7fDjCrAQGULNwJPiwFWN2WaasWb6yUT3lNb\n\
        NtxYVV3QlgXzfKwPRZkN8ksuJQKBgQDUUerM73A3pMEoLNNQWHbZ2MaYZKQvvLNh\n\
        ldbNBXAxl3FiWdZnuUhy76oNQCgl2WQr6t9uoeLU+WQsbBb0STpq/1pKtdrcbMS8\n\
        saHPC9YTV2SWqBQcZ90oX5PcDHNGiGWXg+NEbv7Twv6Q3EraRKY69gswjIJZNv6N\n\
        mE6guLa3BwKBgAfOl0aMOOcV4riyC7tdpoItK69vF9yjEoP7NcZmqGrDHiC3per/\n\
        yLxFMs/HozRcLO+q9ud80kUdLs+gDx6b9yOr6kEsHJBlf0RyFe/UiQnmEv/gfkPb\n\
        KoOOpNQfX8Byk/Tjnk/yS/TRiEiGJpzj1bZQBfi7Ti++ebV5S4ugFszRAoGAT9RR\n\
        BSbNuY4vtexMs3vfi/8OhIPqm6xGgd11uvZdfbfkQMdobu1iSRzFgl/p+HkpSb3w\n\
        rkcd41e1i2JEqyrRVSOXjlmb5Z7bxdq/7PoVToZgYqjljtyacbCmBmmD+jQUhohn\n\
        LfbRx0scrdi2KCUBn0+dW26pH+Qmh5SJk7J/uIcCgYEAjdQB2lyda3keZb+bB/WL\n\
        22SuwmcXKDCRw25d8n3lXEqB9C/01qvz1+AwQwSCBzW+JVRAnk1QzqxXqGnncTqI\n\
        tuMSRV6rq/R8OGd0NH+QPMSLPemgvF1bCFd8+a36dt/e84Q4PBh1vOzZaSGPoE58\n\
        IWF5NoDovkqU7R2qIDFs/ZI=\n\
        -----END PRIVATE KEY-----\n";
}
