use std::fs::File;
use std::io::Read;
use std::path::Path;

use serde::Deserialize;
use zeroize::Zeroize;

#[derive(Clone, Deserialize, Zeroize)]
#[zeroize(drop)]
pub struct AADCredentials {
    pub tenant_id: String,
    pub client_id: String,
    pub client_secret: String
}

#[derive(Deserialize)]
pub struct Principal {
    pub name: String,
    pub authtype: String,
    pub gid: u32
    // pub secrets: String
}

#[derive(Deserialize)]
pub struct Configuration {
    pub credentials: AADCredentials,
    pub principal: Vec<Principal>
}

pub fn load(path: &Path) -> Configuration {
    let mut conf = File::open(path).unwrap();
    let mut buf = String::new();
    conf.read_to_string(&mut buf).unwrap();
    toml::from_str(&buf).unwrap()
}
