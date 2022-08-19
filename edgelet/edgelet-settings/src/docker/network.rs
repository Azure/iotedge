// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
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
                    crate::DEFAULT_NETWORKID
                } else {
                    name
                }
            }
            MobyNetwork::Network(network) => &network.name,
        }
    }
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Network {
    pub name: String,

    #[serde(rename = "ipv6", skip_serializing_if = "Option::is_none")]
    pub ipv6: Option<bool>,

    #[serde(rename = "ipam", skip_serializing_if = "Option::is_none")]
    pub ipam: Option<Ipam>,
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

    #[must_use]
    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn ipv6(&self) -> Option<bool> {
        self.ipv6
    }

    #[must_use]
    pub fn with_ipv6(mut self, ipv6: Option<bool>) -> Self {
        self.ipv6 = ipv6;
        self
    }

    pub fn ipam(&self) -> Option<&Ipam> {
        self.ipam.as_ref()
    }

    #[must_use]
    pub fn with_ipam(mut self, ipam: Ipam) -> Self {
        self.ipam = Some(ipam);
        self
    }
}

#[derive(Clone, Debug, Default, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct Ipam {
    #[serde(rename = "config", skip_serializing_if = "Option::is_none")]
    pub config: Option<Vec<IpamConfig>>,
}

impl Ipam {
    pub fn config(&self) -> Option<&[IpamConfig]> {
        self.config.as_deref()
    }

    #[must_use]
    pub fn with_config(mut self, config: Vec<IpamConfig>) -> Self {
        self.config = Some(config);
        self
    }
}

#[derive(Clone, Debug, Default, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct IpamConfig {
    #[serde(rename = "gateway", skip_serializing_if = "Option::is_none")]
    pub gateway: Option<String>,

    #[serde(rename = "subnet", skip_serializing_if = "Option::is_none")]
    pub subnet: Option<String>,

    #[serde(rename = "ip_range", skip_serializing_if = "Option::is_none")]
    pub ip_range: Option<String>,
}

impl IpamConfig {
    pub fn gateway(&self) -> Option<&str> {
        self.gateway.as_deref()
    }

    #[must_use]
    pub fn with_gateway(mut self, gateway: String) -> Self {
        self.gateway = Some(gateway);
        self
    }

    pub fn subnet(&self) -> Option<&str> {
        self.subnet.as_deref()
    }

    #[must_use]
    pub fn with_subnet(mut self, subnet: String) -> Self {
        self.subnet = Some(subnet);
        self
    }

    pub fn ip_range(&self) -> Option<&str> {
        self.ip_range.as_deref()
    }

    #[must_use]
    pub fn with_ip_range(mut self, ip_range: String) -> Self {
        self.ip_range = Some(ip_range);
        self
    }
}

#[cfg(test)]
mod tests {
    use super::{Ipam, IpamConfig, MobyNetwork, Network};

    #[test]
    fn ipam_config_with_values() {
        let gateway = "172.18.0.1";
        let subnet = "172.18.0.0/16";
        let ip_range = "172.18.0.0/24";

        let ipam_config = IpamConfig::default()
            .with_gateway(gateway.to_string())
            .with_ip_range(ip_range.to_string())
            .with_subnet(subnet.to_string());

        assert_eq!(gateway, ipam_config.gateway().unwrap());
        assert_eq!(subnet, ipam_config.subnet().unwrap());
        assert_eq!(ip_range, ipam_config.ip_range().unwrap());
    }

    #[test]
    fn network_with_values() {
        let ipam_config = IpamConfig::default()
            .with_gateway("172.18.0.1".to_string())
            .with_ip_range("172.18.0.0/16".to_string())
            .with_subnet("172.18.0.0/16".to_string());

        let ipam = Ipam::default().with_config(vec![ipam_config]);
        let network_name = "my-network";
        let ipv6 = true;
        let network = Network::new(network_name.to_string())
            .with_ipv6(Some(ipv6))
            .with_ipam(ipam.clone());

        assert_eq!(network_name, network.name());
        assert_eq!(ipv6, network.ipv6().unwrap());
        assert_eq!(ipam, network.ipam().unwrap().clone());
    }

    #[test]
    fn moby_network_name() {
        let moby_network_with_no_name = MobyNetwork::Name("".to_string());

        let moby_1 = "name-1";
        let moby_network_with_name = MobyNetwork::Name(moby_1.to_string());

        let moby_2 = "network-1";
        let moby_network_config = MobyNetwork::Network(Network::new(moby_2.to_string()));

        assert_eq!(crate::DEFAULT_NETWORKID, moby_network_with_no_name.name());
        assert_eq!(moby_1, moby_network_with_name.name());
        assert_eq!(moby_2, moby_network_config.name());
    }
}
