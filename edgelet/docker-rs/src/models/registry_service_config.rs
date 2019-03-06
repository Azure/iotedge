/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// RegistryServiceConfig : RegistryServiceConfig stores daemon registry services configuration.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct RegistryServiceConfig {
    /// List of IP ranges to which nondistributable artifacts can be pushed, using the CIDR syntax [RFC 4632](https://tools.ietf.org/html/4632).  Some images (for example, Windows base images) contain artifacts whose distribution is restricted by license. When these images are pushed to a registry, restricted artifacts are not included.  This configuration override this behavior, and enables the daemon to push nondistributable artifacts to all registries whose resolved IP address is within the subnet described by the CIDR syntax.  This option is useful when pushing images containing nondistributable artifacts to a registry on an air-gapped network so hosts on that network can pull the images without connecting to another server.  > **Warning**: Nondistributable artifacts typically have restrictions > on how and where they can be distributed and shared. Only use this > feature to push artifacts to private registries and ensure that you > are in compliance with any terms that cover redistributing > nondistributable artifacts.
    #[serde(
        rename = "AllowNondistributableArtifactsCIDRs",
        skip_serializing_if = "Option::is_none"
    )]
    allow_nondistributable_artifacts_cid_rs: Option<Vec<String>>,
    /// List of registry hostnames to which nondistributable artifacts can be pushed, using the format `<hostname>[:<port>]` or `<IP address>[:<port>]`.  Some images (for example, Windows base images) contain artifacts whose distribution is restricted by license. When these images are pushed to a registry, restricted artifacts are not included.  This configuration override this behavior for the specified registries.  This option is useful when pushing images containing nondistributable artifacts to a registry on an air-gapped network so hosts on that network can pull the images without connecting to another server.  > **Warning**: Nondistributable artifacts typically have restrictions > on how and where they can be distributed and shared. Only use this > feature to push artifacts to private registries and ensure that you > are in compliance with any terms that cover redistributing > nondistributable artifacts.
    #[serde(
        rename = "AllowNondistributableArtifactsHostnames",
        skip_serializing_if = "Option::is_none"
    )]
    allow_nondistributable_artifacts_hostnames: Option<Vec<String>>,
    /// List of IP ranges of insecure registries, using the CIDR syntax ([RFC 4632](https://tools.ietf.org/html/4632)). Insecure registries accept un-encrypted (HTTP) and/or untrusted (HTTPS with certificates from unknown CAs) communication.  By default, local registries (`127.0.0.0/8`) are configured as insecure. All other registries are secure. Communicating with an insecure registry is not possible if the daemon assumes that registry is secure.  This configuration override this behavior, insecure communication with registries whose resolved IP address is within the subnet described by the CIDR syntax.  Registries can also be marked insecure by hostname. Those registries are listed under `IndexConfigs` and have their `Secure` field set to `false`.  > **Warning**: Using this option can be useful when running a local > registry, but introduces security vulnerabilities. This option > should therefore ONLY be used for testing purposes. For increased > security, users should add their CA to their system's list of trusted > CAs instead of enabling this option.
    #[serde(
        rename = "InsecureRegistryCIDRs",
        skip_serializing_if = "Option::is_none"
    )]
    insecure_registry_cid_rs: Option<Vec<String>>,
    #[serde(rename = "IndexConfigs", skip_serializing_if = "Option::is_none")]
    index_configs: Option<::std::collections::HashMap<String, crate::models::IndexInfo>>,
    /// List of registry URLs that act as a mirror for the official (`docker.io`) registry.
    #[serde(rename = "Mirrors", skip_serializing_if = "Option::is_none")]
    mirrors: Option<Vec<String>>,
}

impl RegistryServiceConfig {
    /// RegistryServiceConfig stores daemon registry services configuration.
    pub fn new() -> Self {
        RegistryServiceConfig {
            allow_nondistributable_artifacts_cid_rs: None,
            allow_nondistributable_artifacts_hostnames: None,
            insecure_registry_cid_rs: None,
            index_configs: None,
            mirrors: None,
        }
    }

    pub fn set_allow_nondistributable_artifacts_cid_rs(
        &mut self,
        allow_nondistributable_artifacts_cid_rs: Vec<String>,
    ) {
        self.allow_nondistributable_artifacts_cid_rs =
            Some(allow_nondistributable_artifacts_cid_rs);
    }

    pub fn with_allow_nondistributable_artifacts_cid_rs(
        mut self,
        allow_nondistributable_artifacts_cid_rs: Vec<String>,
    ) -> Self {
        self.allow_nondistributable_artifacts_cid_rs =
            Some(allow_nondistributable_artifacts_cid_rs);
        self
    }

    pub fn allow_nondistributable_artifacts_cid_rs(&self) -> Option<&[String]> {
        self.allow_nondistributable_artifacts_cid_rs
            .as_ref()
            .map(AsRef::as_ref)
    }

    pub fn reset_allow_nondistributable_artifacts_cid_rs(&mut self) {
        self.allow_nondistributable_artifacts_cid_rs = None;
    }

    pub fn set_allow_nondistributable_artifacts_hostnames(
        &mut self,
        allow_nondistributable_artifacts_hostnames: Vec<String>,
    ) {
        self.allow_nondistributable_artifacts_hostnames =
            Some(allow_nondistributable_artifacts_hostnames);
    }

    pub fn with_allow_nondistributable_artifacts_hostnames(
        mut self,
        allow_nondistributable_artifacts_hostnames: Vec<String>,
    ) -> Self {
        self.allow_nondistributable_artifacts_hostnames =
            Some(allow_nondistributable_artifacts_hostnames);
        self
    }

    pub fn allow_nondistributable_artifacts_hostnames(&self) -> Option<&[String]> {
        self.allow_nondistributable_artifacts_hostnames
            .as_ref()
            .map(AsRef::as_ref)
    }

    pub fn reset_allow_nondistributable_artifacts_hostnames(&mut self) {
        self.allow_nondistributable_artifacts_hostnames = None;
    }

    pub fn set_insecure_registry_cid_rs(&mut self, insecure_registry_cid_rs: Vec<String>) {
        self.insecure_registry_cid_rs = Some(insecure_registry_cid_rs);
    }

    pub fn with_insecure_registry_cid_rs(mut self, insecure_registry_cid_rs: Vec<String>) -> Self {
        self.insecure_registry_cid_rs = Some(insecure_registry_cid_rs);
        self
    }

    pub fn insecure_registry_cid_rs(&self) -> Option<&[String]> {
        self.insecure_registry_cid_rs.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_insecure_registry_cid_rs(&mut self) {
        self.insecure_registry_cid_rs = None;
    }

    pub fn set_index_configs(
        &mut self,
        index_configs: ::std::collections::HashMap<String, crate::models::IndexInfo>,
    ) {
        self.index_configs = Some(index_configs);
    }

    pub fn with_index_configs(
        mut self,
        index_configs: ::std::collections::HashMap<String, crate::models::IndexInfo>,
    ) -> Self {
        self.index_configs = Some(index_configs);
        self
    }

    pub fn index_configs(
        &self,
    ) -> Option<&::std::collections::HashMap<String, crate::models::IndexInfo>> {
        self.index_configs.as_ref()
    }

    pub fn reset_index_configs(&mut self) {
        self.index_configs = None;
    }

    pub fn set_mirrors(&mut self, mirrors: Vec<String>) {
        self.mirrors = Some(mirrors);
    }

    pub fn with_mirrors(mut self, mirrors: Vec<String>) -> Self {
        self.mirrors = Some(mirrors);
        self
    }

    pub fn mirrors(&self) -> Option<&[String]> {
        self.mirrors.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_mirrors(&mut self) {
        self.mirrors = None;
    }
}
