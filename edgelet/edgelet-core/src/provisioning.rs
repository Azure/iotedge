// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;

use regex::RegexSet;

use error::{Error, ErrorKind};

static DEVICEID_KEY: &'static str = "DeviceId";
static HOSTNAME_KEY: &'static str = "HostName";
static SHAREDACCESSKEY_KEY: &'static str = "SharedAccessKey";

static DEVICEID_REGEX: &'static str = r"DeviceId=([A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128})";
static HOSTNAME_REGEX: &'static str = r"HostName=([a-zA-Z0-9_\-\.]+)";
static SHAREDACCESSKEY_REGEX: &'static str = r"SharedAccessKey=(.+)";

pub trait Provision {
    fn device_id(&self) -> &str;
    fn host_name(&self) -> &str;
}

#[derive(Debug)]
pub struct ManualProvisioning {
    connection_string: String,
    hash_map: HashMap<String, String>,
}

impl ManualProvisioning {
    pub fn new(conn_string: &str) -> Result<Self, Error> {
        ensure_not_empty!(conn_string);
        let hash_map = ManualProvisioning::parse_conn_string(conn_string)?;
        Ok(ManualProvisioning {
            connection_string: conn_string.to_string(),
            hash_map,
        })
    }

    pub fn key(&self) -> Result<&str, Error> {
        self.hash_map
            .get(SHAREDACCESSKEY_KEY)
            .map(|s| s.as_str())
            .ok_or_else(|| Error::from(ErrorKind::NotFound))
    }

    fn parse_conn_string(conn_string: &str) -> Result<HashMap<String, String>, Error> {
        let mut hash_map = HashMap::new();
        let parts: Vec<&str> = conn_string.split(';').collect();
        let set = RegexSet::new(&[DEVICEID_REGEX, HOSTNAME_REGEX, SHAREDACCESSKEY_REGEX])
            .expect("Regex construction failure");
        let matches: Vec<_> = set.matches(conn_string).into_iter().collect();
        if matches != vec![0, 1, 2] {
            // Error if all three components are not provided
            return Err(Error::from(ErrorKind::Provision(
                "Invalid connection string".to_string(),
            )));
        }
        for p in parts {
            let s: Vec<&str> = p.split('=').collect();
            if set.is_match(p) {
                hash_map.insert(s[0].to_string(), s[1].to_string());
            } // Ignore extraneous component in the connection string
        }
        Ok(hash_map)
    }
}

impl Provision for ManualProvisioning {
    fn device_id(&self) -> &str {
        self.hash_map[DEVICEID_KEY].as_str()
    }

    fn host_name(&self) -> &str {
        self.hash_map[HOSTNAME_KEY].as_str()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn manual_get_credentials_success() {
        let test = ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(test.is_ok(), true);
        let t: &ManualProvisioning = &test.expect("fail");
        assert_eq!(t.device_id(), "test");
        assert_eq!(t.host_name(), "test.com");
        assert_eq!(t.key().unwrap_or(""), "test");
    }

    #[test]
    fn manual_malformed_conn_string_gets_error() {
        let test = ManualProvisioning::new("HostName=test.com;DeviceId=test;");
        assert_eq!(test.is_err(), true);
    }

    #[test]
    fn connection_string_split_success() {
        let test = ManualProvisioning::new("HostName=test.com;DeviceId=test;SharedAccessKey=test");
        assert_eq!(test.is_ok(), true);
        let t: &mut ManualProvisioning = &mut test.expect("fail");
        assert_eq!(t.device_id(), "test");
        assert_eq!(t.host_name(), "test.com");
        assert_eq!(t.key().unwrap_or(""), "test");

        let test = ManualProvisioning::new("DeviceId=test;SharedAccessKey=test;HostName=test.com");
        assert_eq!(test.is_ok(), true);
        let t: &mut ManualProvisioning = &mut test.expect("fail");
        assert_eq!(t.device_id(), "test");
        assert_eq!(t.host_name(), "test.com");
        assert_eq!(t.key().unwrap_or(""), "test");
    }

    #[test]
    fn connection_string_split_error() {
        let test1 = ManualProvisioning::new("DeviceId=test;SharedAccessKey=test");
        assert_eq!(test1.is_err(), true);
        let test2 = ManualProvisioning::new(
            "HostName=test.com;Extra=something;DeviceId=test;SharedAccessKey=test",
        );
        assert_eq!(test2.is_ok(), true);
        let t: &ManualProvisioning = &test2.expect("fail");
        assert_eq!(t.device_id(), "test");
        assert_eq!(t.host_name(), "test.com");
        assert_eq!(t.key().unwrap_or(""), "test");
    }
}
