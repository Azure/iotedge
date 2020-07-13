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
