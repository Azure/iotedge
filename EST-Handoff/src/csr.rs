use openssl::rsa::Rsa;
use openssl::pkey::PKey;
use openssl::hash::MessageDigest;
use openssl::x509::{X509Req, X509ReqBuilder};

//use crate::error::Error;
use est_rs::error::Error;

pub struct Csr {
    inner_csr: X509Req,
}

impl Csr {
    pub fn new() -> Result<Self, Error> {
        // Generate a key
        let rsaKey = Rsa::generate(2048)?;
        let pkey = PKey::from_rsa(rsaKey)?;

        // Build the PKCS#10 CSR
        let mut x509req_builder = X509ReqBuilder::new()?;
        x509req_builder.set_version(1)?;
        x509req_builder.set_pubkey(&pkey)?;
        x509req_builder.sign(&pkey, MessageDigest::sha256())?;

        Ok(Csr {
            inner_csr: x509req_builder.build(),
        })
    }
}

pub struct Csr_CA {
    inner_csr: X509Req,
}

impl Csr_CA {
    pub fn new() -> Result<Self, Error> {
        // Generate a key
        let rsaKey = Rsa::generate(2048)?;
        let pkey = PKey::from_rsa(rsaKey)?;

        // Build the PKCS#10 CSR
        let mut x509req_builder = X509ReqBuilder::new()?;
        x509req_builder.set_version(1)?;
        x509req_builder.set_pubkey(&pkey)?;
        x509req_builder.sign(&pkey, MessageDigest::sha256())?;

        Ok(Csr {
            inner_csr: x509req_builder.build(),
        })
    }
}