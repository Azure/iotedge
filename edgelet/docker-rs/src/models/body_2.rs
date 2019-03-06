/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Body2 {
    /// Listen address used for inter-manager communication if the node gets promoted to manager, as well as determining the networking interface used for the VXLAN Tunnel Endpoint (VTEP).
    #[serde(rename = "ListenAddr", skip_serializing_if = "Option::is_none")]
    listen_addr: Option<String>,
    /// Externally reachable address advertised to other nodes. This can either be an address/port combination in the form `192.168.1.1:4567`, or an interface followed by a port number, like `eth0:4567`. If the port number is omitted, the port number from the listen address is used. If `AdvertiseAddr` is not specified, it will be automatically detected when possible.
    #[serde(rename = "AdvertiseAddr", skip_serializing_if = "Option::is_none")]
    advertise_addr: Option<String>,
    /// Address or interface to use for data path traffic (format: `<ip|interface>`), for example,  `192.168.1.1`, or an interface, like `eth0`. If `DataPathAddr` is unspecified, the same address as `AdvertiseAddr` is used.  The `DataPathAddr` specifies the address that global scope network drivers will publish towards other nodes in order to reach the containers running on this node. Using this parameter it is possible to separate the container data traffic from the management traffic of the cluster.
    #[serde(rename = "DataPathAddr", skip_serializing_if = "Option::is_none")]
    data_path_addr: Option<String>,
    /// Addresses of manager nodes already participating in the swarm.
    #[serde(rename = "RemoteAddrs", skip_serializing_if = "Option::is_none")]
    remote_addrs: Option<String>,
    /// Secret token for joining this swarm.
    #[serde(rename = "JoinToken", skip_serializing_if = "Option::is_none")]
    join_token: Option<String>,
}

impl Body2 {
    pub fn new() -> Self {
        Body2 {
            listen_addr: None,
            advertise_addr: None,
            data_path_addr: None,
            remote_addrs: None,
            join_token: None,
        }
    }

    pub fn set_listen_addr(&mut self, listen_addr: String) {
        self.listen_addr = Some(listen_addr);
    }

    pub fn with_listen_addr(mut self, listen_addr: String) -> Self {
        self.listen_addr = Some(listen_addr);
        self
    }

    pub fn listen_addr(&self) -> Option<&str> {
        self.listen_addr.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_listen_addr(&mut self) {
        self.listen_addr = None;
    }

    pub fn set_advertise_addr(&mut self, advertise_addr: String) {
        self.advertise_addr = Some(advertise_addr);
    }

    pub fn with_advertise_addr(mut self, advertise_addr: String) -> Self {
        self.advertise_addr = Some(advertise_addr);
        self
    }

    pub fn advertise_addr(&self) -> Option<&str> {
        self.advertise_addr.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_advertise_addr(&mut self) {
        self.advertise_addr = None;
    }

    pub fn set_data_path_addr(&mut self, data_path_addr: String) {
        self.data_path_addr = Some(data_path_addr);
    }

    pub fn with_data_path_addr(mut self, data_path_addr: String) -> Self {
        self.data_path_addr = Some(data_path_addr);
        self
    }

    pub fn data_path_addr(&self) -> Option<&str> {
        self.data_path_addr.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_data_path_addr(&mut self) {
        self.data_path_addr = None;
    }

    pub fn set_remote_addrs(&mut self, remote_addrs: String) {
        self.remote_addrs = Some(remote_addrs);
    }

    pub fn with_remote_addrs(mut self, remote_addrs: String) -> Self {
        self.remote_addrs = Some(remote_addrs);
        self
    }

    pub fn remote_addrs(&self) -> Option<&str> {
        self.remote_addrs.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_remote_addrs(&mut self) {
        self.remote_addrs = None;
    }

    pub fn set_join_token(&mut self, join_token: String) {
        self.join_token = Some(join_token);
    }

    pub fn with_join_token(mut self, join_token: String) -> Self {
        self.join_token = Some(join_token);
        self
    }

    pub fn join_token(&self) -> Option<&str> {
        self.join_token.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_join_token(&mut self) {
        self.join_token = None;
    }
}
