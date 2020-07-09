use std::fs::File;
use std::io::Read;
use std::path::Path;

use serde::Deserialize;

#[derive(Clone, Deserialize)]
pub struct Configuration {
    pub client_id: String,
    pub client_secret: String,
    pub tenant_id: String
}

pub fn load(path: &Path) -> Configuration {
    let mut conf = File::open(path).unwrap();
    let mut buf = String::new();
    conf.read_to_string(&mut buf).unwrap();
    toml::from_str(&buf).unwrap()
}
