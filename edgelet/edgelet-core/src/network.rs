// Copyright (c) Microsoft. All rights reserved.

use crate::DEFAULT_NETWORKID;
use serde_derive::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Network {
    name: String,

    #[serde(rename = "ipv6", skip_serializing_if = "Option::is_none")]
    ipv6: Option<bool>,

    #[serde(rename = "ipam", skip_serializing_if = "Option::is_none")]
    ipam: Option<Vec<Ipam>>,
}

impl Network {
    pub fn new(name: String) -> Self {
        Network {
            name,
            ipv6: None,
            ipam: None,
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn ipv6(&self) -> Option<&bool> {
        self.ipv6.as_ref()
    }

    pub fn with_ipv6(mut self, ipv6: Option<bool>) -> Self {
        self.ipv6 = ipv6;
        self
    }

    pub fn ipam(&self) -> Option<&Vec<Ipam>> {
        self.ipam.as_ref().map(AsRef::as_ref)
    }

    pub fn with_ipam(mut self, ipam: Option<Vec<Ipam>>) -> Self {
        self.ipam = ipam;
        self
    }
}

#[derive(Clone, Debug, Default, Deserialize, PartialEq, Serialize)]
pub struct Ipam {
    #[serde(rename = "gateway", skip_serializing_if = "Option::is_none")]
    gateway: Option<String>,

    #[serde(rename = "subnet", skip_serializing_if = "Option::is_none")]
    subnet: Option<String>,

    #[serde(rename = "ip_range", skip_serializing_if = "Option::is_none")]
    ip_range: Option<String>,
}

impl Ipam {
    pub fn gateway(&self) -> Option<&str> {
        self.gateway.as_ref().map(AsRef::as_ref)
    }

    pub fn with_gateway(mut self, gateway: String) -> Self {
        self.gateway = Some(gateway);
        self
    }

    pub fn subnet(&self) -> Option<&str> {
        self.subnet.as_ref().map(AsRef::as_ref)
    }

    pub fn with_subnet(mut self, subnet: String) -> Self {
        self.subnet = Some(subnet);
        self
    }

    pub fn ip_range(&self) -> Option<&str> {
        self.ip_range.as_ref().map(AsRef::as_ref)
    }

    pub fn with_ip_range(mut self, ip_range: String) -> Self {
        self.ip_range = Some(ip_range);
        self
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(untagged)]
pub enum MobyNetwork {
    Network(Network),
    Name(String),
}

impl MobyNetwork {
    pub fn name(&self) -> &str {
        match self {
            MobyNetwork::Name(name) => {
                if name.is_empty() {
                    DEFAULT_NETWORKID
                } else {
                    name
                }
            }
            MobyNetwork::Network(network) => &network.name,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_ipam_with_values() {
        let gateway = "172.18.0.1";
        let subnet = "172.18.0.0/16";
        let ip_range = "172.18.0.0/24";

        let ipam = Ipam::default()
            .with_gateway(gateway.to_string())
            .with_ip_range(ip_range.to_string())
            .with_subnet(subnet.to_string());

        assert_eq!(gateway, ipam.gateway().unwrap());
        assert_eq!(subnet, ipam.subnet().unwrap());
        assert_eq!(ip_range, ipam.ip_range().unwrap());
    }

    #[test]
    fn test_network_with_values() {
        let ipam = Ipam::default()
            .with_gateway("172.18.0.1".to_string())
            .with_ip_range("172.18.0.0/16".to_string())
            .with_subnet("172.18.0.0/16".to_string());

        let network_name = "my-network";
        let ipv6 = true;
        let network = Network::new(network_name.to_string())
            .with_ipv6(Some(ipv6))
            .with_ipam(Some(vec![ipam.clone()]));

        assert_eq!(network_name, network.name());
        assert_eq!(&ipv6, network.ipv6().unwrap());
        assert_eq!(true, network.ipam().unwrap().contains(&ipam));
    }

    #[test]
    fn test_moby_network_name() {
        let moby_network_with_no_name = MobyNetwork::Name("".to_string());

        let moby_1 = "name-1";
        let moby_network_with_name = MobyNetwork::Name(moby_1.to_string());

        let moby_2 = "network-1";
        let moby_network_config = MobyNetwork::Network(Network::new(moby_2.to_string()));

        assert_eq!(DEFAULT_NETWORKID, moby_network_with_no_name.name());
        assert_eq!(moby_1, moby_network_with_name.name());
        assert_eq!(moby_2, moby_network_config.name());
    }
}
