// mod csr;
// mod csr;
mod csr;

use std::collections::HashMap;
// use est_rs::csr::Csr;
// use crate::csr::Csr
// use std::str;

pub use csr::Csr;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let csr = Csr::new().unwrap();

    /*
    let resp = reqwest::post("https://testrfc7030.com:8443/.well-known/est/simpleenroll")
        .await?
        .json::<HashMap<String, String>>()
        .await?;
    */

    let client = reqwest::Client::new();
    let res = client.post("https://testrfc7030.com:8443/.well-known/est/simpleenroll")
        .basic_auth("estuser", "estpwd")
        .body(csr.to_pem().unwrap())
        .send()
        .await?;
    println!("{:#?}", resp);

    // Check response status

    // write out signed certificate
    Ok(())
}


pub struct Cert {

}

impl Cert { 
    // CERTGEN_ERROR (*create_or_load_cert)(CERTGEN_CERT_KIND kind, const char *uri, EVP_PKEY *public_key, EVP_PKEY *private_key, X509 **pcert)

    pub fn create_or_load_cert(str kind, str uri, str public_key, str private_key) -> Result<vec[u8]> {
        let csr = Csr::new().unwrap();

        let client = reqwest::Client::new();
        let res = client.post("https://testrfc7030.com:8443/.well-known/est/simpleenroll")
            .basic_auth("estuser", "estpwd")
            .body(csr.to_pem().unwrap())
            .send()
            .await?;
    }
}