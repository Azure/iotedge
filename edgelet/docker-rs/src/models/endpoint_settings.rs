/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// EndpointSettings : Configuration for a network endpoint.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct EndpointSettings {
    #[serde(rename = "IPAMConfig", skip_serializing_if = "Option::is_none")]
    ipam_config: Option<crate::models::EndpointIpamConfig>,
    #[serde(rename = "Links", skip_serializing_if = "Option::is_none")]
    links: Option<Vec<String>>,
    #[serde(rename = "Aliases", skip_serializing_if = "Option::is_none")]
    aliases: Option<Vec<String>>,
    /// Unique ID of the network.
    #[serde(rename = "NetworkID", skip_serializing_if = "Option::is_none")]
    network_id: Option<String>,
    /// Unique ID for the service endpoint in a Sandbox.
    #[serde(rename = "EndpointID", skip_serializing_if = "Option::is_none")]
    endpoint_id: Option<String>,
    /// Gateway address for this network.
    #[serde(rename = "Gateway", skip_serializing_if = "Option::is_none")]
    gateway: Option<String>,
    /// IPv4 address.
    #[serde(rename = "IPAddress", skip_serializing_if = "Option::is_none")]
    ip_address: Option<String>,
    /// Mask length of the IPv4 address.
    #[serde(rename = "IPPrefixLen", skip_serializing_if = "Option::is_none")]
    ip_prefix_len: Option<i32>,
    /// IPv6 gateway address.
    #[serde(rename = "IPv6Gateway", skip_serializing_if = "Option::is_none")]
    i_pv6_gateway: Option<String>,
    /// Global IPv6 address.
    #[serde(rename = "GlobalIPv6Address", skip_serializing_if = "Option::is_none")]
    global_i_pv6_address: Option<String>,
    /// Mask length of the global IPv6 address.
    #[serde(
        rename = "GlobalIPv6PrefixLen",
        skip_serializing_if = "Option::is_none"
    )]
    global_i_pv6_prefix_len: Option<i64>,
    /// MAC address for the endpoint on this network.
    #[serde(rename = "MacAddress", skip_serializing_if = "Option::is_none")]
    mac_address: Option<String>,
    /// DriverOpts is a mapping of driver options and values. These options are passed directly to the driver and are driver specific.
    #[serde(rename = "DriverOpts", skip_serializing_if = "Option::is_none")]
    driver_opts: Option<::std::collections::HashMap<String, String>>,
}

impl EndpointSettings {
    /// Configuration for a network endpoint.
    pub fn new() -> Self {
        EndpointSettings {
            ipam_config: None,
            links: None,
            aliases: None,
            network_id: None,
            endpoint_id: None,
            gateway: None,
            ip_address: None,
            ip_prefix_len: None,
            i_pv6_gateway: None,
            global_i_pv6_address: None,
            global_i_pv6_prefix_len: None,
            mac_address: None,
            driver_opts: None,
        }
    }

    pub fn set_ipam_config(&mut self, ipam_config: crate::models::EndpointIpamConfig) {
        self.ipam_config = Some(ipam_config);
    }

    pub fn with_ipam_config(mut self, ipam_config: crate::models::EndpointIpamConfig) -> Self {
        self.ipam_config = Some(ipam_config);
        self
    }

    pub fn ipam_config(&self) -> Option<&crate::models::EndpointIpamConfig> {
        self.ipam_config.as_ref()
    }

    pub fn reset_ipam_config(&mut self) {
        self.ipam_config = None;
    }

    pub fn set_links(&mut self, links: Vec<String>) {
        self.links = Some(links);
    }

    pub fn with_links(mut self, links: Vec<String>) -> Self {
        self.links = Some(links);
        self
    }

    pub fn links(&self) -> Option<&[String]> {
        self.links.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_links(&mut self) {
        self.links = None;
    }

    pub fn set_aliases(&mut self, aliases: Vec<String>) {
        self.aliases = Some(aliases);
    }

    pub fn with_aliases(mut self, aliases: Vec<String>) -> Self {
        self.aliases = Some(aliases);
        self
    }

    pub fn aliases(&self) -> Option<&[String]> {
        self.aliases.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_aliases(&mut self) {
        self.aliases = None;
    }

    pub fn set_network_id(&mut self, network_id: String) {
        self.network_id = Some(network_id);
    }

    pub fn with_network_id(mut self, network_id: String) -> Self {
        self.network_id = Some(network_id);
        self
    }

    pub fn network_id(&self) -> Option<&str> {
        self.network_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_network_id(&mut self) {
        self.network_id = None;
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

    pub fn set_global_i_pv6_prefix_len(&mut self, global_i_pv6_prefix_len: i64) {
        self.global_i_pv6_prefix_len = Some(global_i_pv6_prefix_len);
    }

    pub fn with_global_i_pv6_prefix_len(mut self, global_i_pv6_prefix_len: i64) -> Self {
        self.global_i_pv6_prefix_len = Some(global_i_pv6_prefix_len);
        self
    }

    pub fn global_i_pv6_prefix_len(&self) -> Option<i64> {
        self.global_i_pv6_prefix_len
    }

    pub fn reset_global_i_pv6_prefix_len(&mut self) {
        self.global_i_pv6_prefix_len = None;
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

    pub fn set_driver_opts(&mut self, driver_opts: ::std::collections::HashMap<String, String>) {
        self.driver_opts = Some(driver_opts);
    }

    pub fn with_driver_opts(
        mut self,
        driver_opts: ::std::collections::HashMap<String, String>,
    ) -> Self {
        self.driver_opts = Some(driver_opts);
        self
    }

    pub fn driver_opts(&self) -> Option<&::std::collections::HashMap<String, String>> {
        self.driver_opts.as_ref()
    }

    pub fn reset_driver_opts(&mut self) {
        self.driver_opts = None;
    }
}
