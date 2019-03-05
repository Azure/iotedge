/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// NetworkSettings : NetworkSettings exposes the network settings in the API
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct NetworkSettings {
    /// Name of the network'a bridge (for example, `docker0`).
    #[serde(rename = "Bridge", skip_serializing_if = "Option::is_none")]
    bridge: Option<String>,
    /// SandboxID uniquely represents a container's network stack.
    #[serde(rename = "SandboxID", skip_serializing_if = "Option::is_none")]
    sandbox_id: Option<String>,
    /// Indicates if hairpin NAT should be enabled on the virtual interface.
    #[serde(rename = "HairpinMode", skip_serializing_if = "Option::is_none")]
    hairpin_mode: Option<bool>,
    /// IPv6 unicast address using the link-local prefix.
    #[serde(
        rename = "LinkLocalIPv6Address",
        skip_serializing_if = "Option::is_none"
    )]
    link_local_i_pv6_address: Option<String>,
    /// Prefix length of the IPv6 unicast address.
    #[serde(
        rename = "LinkLocalIPv6PrefixLen",
        skip_serializing_if = "Option::is_none"
    )]
    link_local_i_pv6_prefix_len: Option<i32>,
    #[serde(rename = "Ports", skip_serializing_if = "Option::is_none")]
    ports: Option<crate::models::PortMap>,
    /// SandboxKey identifies the sandbox
    #[serde(rename = "SandboxKey", skip_serializing_if = "Option::is_none")]
    sandbox_key: Option<String>,
    ///
    #[serde(
        rename = "SecondaryIPAddresses",
        skip_serializing_if = "Option::is_none"
    )]
    secondary_ip_addresses: Option<Vec<crate::models::Address>>,
    ///
    #[serde(
        rename = "SecondaryIPv6Addresses",
        skip_serializing_if = "Option::is_none"
    )]
    secondary_i_pv6_addresses: Option<Vec<crate::models::Address>>,
    /// EndpointID uniquely represents a service endpoint in a Sandbox.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "EndpointID", skip_serializing_if = "Option::is_none")]
    endpoint_id: Option<String>,
    /// Gateway address for the default \"bridge\" network.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "Gateway", skip_serializing_if = "Option::is_none")]
    gateway: Option<String>,
    /// Global IPv6 address for the default \"bridge\" network.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "GlobalIPv6Address", skip_serializing_if = "Option::is_none")]
    global_i_pv6_address: Option<String>,
    /// Mask length of the global IPv6 address.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(
        rename = "GlobalIPv6PrefixLen",
        skip_serializing_if = "Option::is_none"
    )]
    global_i_pv6_prefix_len: Option<i32>,
    /// IPv4 address for the default \"bridge\" network.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "IPAddress", skip_serializing_if = "Option::is_none")]
    ip_address: Option<String>,
    /// Mask length of the IPv4 address.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "IPPrefixLen", skip_serializing_if = "Option::is_none")]
    ip_prefix_len: Option<i32>,
    /// IPv6 gateway address for this network.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "IPv6Gateway", skip_serializing_if = "Option::is_none")]
    i_pv6_gateway: Option<String>,
    /// MAC address for the container on the default \"bridge\" network.  <p><br /></p>  > **Deprecated**: This field is only propagated when attached to the > default \"bridge\" network. Use the information from the \"bridge\" > network inside the `Networks` map instead, which contains the same > information. This field was deprecated in Docker 1.9 and is scheduled > to be removed in Docker 17.12.0
    #[serde(rename = "MacAddress", skip_serializing_if = "Option::is_none")]
    mac_address: Option<String>,
    /// Information about all networks that the container is connected to.
    #[serde(rename = "Networks", skip_serializing_if = "Option::is_none")]
    networks: Option<::std::collections::HashMap<String, crate::models::EndpointSettings>>,
}

impl NetworkSettings {
    /// NetworkSettings exposes the network settings in the API
    pub fn new() -> Self {
        NetworkSettings {
            bridge: None,
            sandbox_id: None,
            hairpin_mode: None,
            link_local_i_pv6_address: None,
            link_local_i_pv6_prefix_len: None,
            ports: None,
            sandbox_key: None,
            secondary_ip_addresses: None,
            secondary_i_pv6_addresses: None,
            endpoint_id: None,
            gateway: None,
            global_i_pv6_address: None,
            global_i_pv6_prefix_len: None,
            ip_address: None,
            ip_prefix_len: None,
            i_pv6_gateway: None,
            mac_address: None,
            networks: None,
        }
    }

    pub fn set_bridge(&mut self, bridge: String) {
        self.bridge = Some(bridge);
    }

    pub fn with_bridge(mut self, bridge: String) -> Self {
        self.bridge = Some(bridge);
        self
    }

    pub fn bridge(&self) -> Option<&str> {
        self.bridge.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_bridge(&mut self) {
        self.bridge = None;
    }

    pub fn set_sandbox_id(&mut self, sandbox_id: String) {
        self.sandbox_id = Some(sandbox_id);
    }

    pub fn with_sandbox_id(mut self, sandbox_id: String) -> Self {
        self.sandbox_id = Some(sandbox_id);
        self
    }

    pub fn sandbox_id(&self) -> Option<&str> {
        self.sandbox_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_sandbox_id(&mut self) {
        self.sandbox_id = None;
    }

    pub fn set_hairpin_mode(&mut self, hairpin_mode: bool) {
        self.hairpin_mode = Some(hairpin_mode);
    }

    pub fn with_hairpin_mode(mut self, hairpin_mode: bool) -> Self {
        self.hairpin_mode = Some(hairpin_mode);
        self
    }

    pub fn hairpin_mode(&self) -> Option<&bool> {
        self.hairpin_mode.as_ref()
    }

    pub fn reset_hairpin_mode(&mut self) {
        self.hairpin_mode = None;
    }

    pub fn set_link_local_i_pv6_address(&mut self, link_local_i_pv6_address: String) {
        self.link_local_i_pv6_address = Some(link_local_i_pv6_address);
    }

    pub fn with_link_local_i_pv6_address(mut self, link_local_i_pv6_address: String) -> Self {
        self.link_local_i_pv6_address = Some(link_local_i_pv6_address);
        self
    }

    pub fn link_local_i_pv6_address(&self) -> Option<&str> {
        self.link_local_i_pv6_address.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_link_local_i_pv6_address(&mut self) {
        self.link_local_i_pv6_address = None;
    }

    pub fn set_link_local_i_pv6_prefix_len(&mut self, link_local_i_pv6_prefix_len: i32) {
        self.link_local_i_pv6_prefix_len = Some(link_local_i_pv6_prefix_len);
    }

    pub fn with_link_local_i_pv6_prefix_len(mut self, link_local_i_pv6_prefix_len: i32) -> Self {
        self.link_local_i_pv6_prefix_len = Some(link_local_i_pv6_prefix_len);
        self
    }

    pub fn link_local_i_pv6_prefix_len(&self) -> Option<i32> {
        self.link_local_i_pv6_prefix_len
    }

    pub fn reset_link_local_i_pv6_prefix_len(&mut self) {
        self.link_local_i_pv6_prefix_len = None;
    }

    pub fn set_ports(&mut self, ports: crate::models::PortMap) {
        self.ports = Some(ports);
    }

    pub fn with_ports(mut self, ports: crate::models::PortMap) -> Self {
        self.ports = Some(ports);
        self
    }

    pub fn ports(&self) -> Option<&crate::models::PortMap> {
        self.ports.as_ref()
    }

    pub fn reset_ports(&mut self) {
        self.ports = None;
    }

    pub fn set_sandbox_key(&mut self, sandbox_key: String) {
        self.sandbox_key = Some(sandbox_key);
    }

    pub fn with_sandbox_key(mut self, sandbox_key: String) -> Self {
        self.sandbox_key = Some(sandbox_key);
        self
    }

    pub fn sandbox_key(&self) -> Option<&str> {
        self.sandbox_key.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_sandbox_key(&mut self) {
        self.sandbox_key = None;
    }

    pub fn set_secondary_ip_addresses(
        &mut self,
        secondary_ip_addresses: Vec<crate::models::Address>,
    ) {
        self.secondary_ip_addresses = Some(secondary_ip_addresses);
    }

    pub fn with_secondary_ip_addresses(
        mut self,
        secondary_ip_addresses: Vec<crate::models::Address>,
    ) -> Self {
        self.secondary_ip_addresses = Some(secondary_ip_addresses);
        self
    }

    pub fn secondary_ip_addresses(&self) -> Option<&[crate::models::Address]> {
        self.secondary_ip_addresses.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_secondary_ip_addresses(&mut self) {
        self.secondary_ip_addresses = None;
    }

    pub fn set_secondary_i_pv6_addresses(
        &mut self,
        secondary_i_pv6_addresses: Vec<crate::models::Address>,
    ) {
        self.secondary_i_pv6_addresses = Some(secondary_i_pv6_addresses);
    }

    pub fn with_secondary_i_pv6_addresses(
        mut self,
        secondary_i_pv6_addresses: Vec<crate::models::Address>,
    ) -> Self {
        self.secondary_i_pv6_addresses = Some(secondary_i_pv6_addresses);
        self
    }

    pub fn secondary_i_pv6_addresses(&self) -> Option<&[crate::models::Address]> {
        self.secondary_i_pv6_addresses.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_secondary_i_pv6_addresses(&mut self) {
        self.secondary_i_pv6_addresses = None;
    }

    pub fn set_endpoint_id(&mut self, endpoint_id: String) {
        self.endpoint_id = Some(endpoint_id);
    }

    pub fn with_endpoint_id(mut self, endpoint_id: String) -> Self {
        self.endpoint_id = Some(endpoint_id);
        self
    }

    pub fn endpoint_id(&self) -> Option<&str> {
        self.endpoint_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_endpoint_id(&mut self) {
        self.endpoint_id = None;
    }

    pub fn set_gateway(&mut self, gateway: String) {
        self.gateway = Some(gateway);
    }

    pub fn with_gateway(mut self, gateway: String) -> Self {
        self.gateway = Some(gateway);
        self
    }

    pub fn gateway(&self) -> Option<&str> {
        self.gateway.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_gateway(&mut self) {
        self.gateway = None;
    }

    pub fn set_global_i_pv6_address(&mut self, global_i_pv6_address: String) {
        self.global_i_pv6_address = Some(global_i_pv6_address);
    }

    pub fn with_global_i_pv6_address(mut self, global_i_pv6_address: String) -> Self {
        self.global_i_pv6_address = Some(global_i_pv6_address);
        self
    }

    pub fn global_i_pv6_address(&self) -> Option<&str> {
        self.global_i_pv6_address.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_global_i_pv6_address(&mut self) {
        self.global_i_pv6_address = None;
    }

    pub fn set_global_i_pv6_prefix_len(&mut self, global_i_pv6_prefix_len: i32) {
        self.global_i_pv6_prefix_len = Some(global_i_pv6_prefix_len);
    }

    pub fn with_global_i_pv6_prefix_len(mut self, global_i_pv6_prefix_len: i32) -> Self {
        self.global_i_pv6_prefix_len = Some(global_i_pv6_prefix_len);
        self
    }

    pub fn global_i_pv6_prefix_len(&self) -> Option<i32> {
        self.global_i_pv6_prefix_len
    }

    pub fn reset_global_i_pv6_prefix_len(&mut self) {
        self.global_i_pv6_prefix_len = None;
    }

    pub fn set_ip_address(&mut self, ip_address: String) {
        self.ip_address = Some(ip_address);
    }

    pub fn with_ip_address(mut self, ip_address: String) -> Self {
        self.ip_address = Some(ip_address);
        self
    }

    pub fn ip_address(&self) -> Option<&str> {
        self.ip_address.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_ip_address(&mut self) {
        self.ip_address = None;
    }

    pub fn set_ip_prefix_len(&mut self, ip_prefix_len: i32) {
        self.ip_prefix_len = Some(ip_prefix_len);
    }

    pub fn with_ip_prefix_len(mut self, ip_prefix_len: i32) -> Self {
        self.ip_prefix_len = Some(ip_prefix_len);
        self
    }

    pub fn ip_prefix_len(&self) -> Option<i32> {
        self.ip_prefix_len
    }

    pub fn reset_ip_prefix_len(&mut self) {
        self.ip_prefix_len = None;
    }

    pub fn set_i_pv6_gateway(&mut self, i_pv6_gateway: String) {
        self.i_pv6_gateway = Some(i_pv6_gateway);
    }

    pub fn with_i_pv6_gateway(mut self, i_pv6_gateway: String) -> Self {
        self.i_pv6_gateway = Some(i_pv6_gateway);
        self
    }

    pub fn i_pv6_gateway(&self) -> Option<&str> {
        self.i_pv6_gateway.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_i_pv6_gateway(&mut self) {
        self.i_pv6_gateway = None;
    }

    pub fn set_mac_address(&mut self, mac_address: String) {
        self.mac_address = Some(mac_address);
    }

    pub fn with_mac_address(mut self, mac_address: String) -> Self {
        self.mac_address = Some(mac_address);
        self
    }

    pub fn mac_address(&self) -> Option<&str> {
        self.mac_address.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_mac_address(&mut self) {
        self.mac_address = None;
    }

    pub fn set_networks(
        &mut self,
        networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
    ) {
        self.networks = Some(networks);
    }

    pub fn with_networks(
        mut self,
        networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
    ) -> Self {
        self.networks = Some(networks);
        self
    }

    pub fn networks(
        &self,
    ) -> Option<&::std::collections::HashMap<String, crate::models::EndpointSettings>> {
        self.networks.as_ref()
    }

    pub fn reset_networks(&mut self) {
        self.networks = None;
    }
}
