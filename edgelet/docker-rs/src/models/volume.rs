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
pub struct Volume {
    /// Name of the volume.
    #[serde(rename = "Name")]
    name: String,
    /// Name of the volume driver used by the volume.
    #[serde(rename = "Driver")]
    driver: String,
    /// Mount path of the volume on the host.
    #[serde(rename = "Mountpoint")]
    mountpoint: String,
    /// Date/Time the volume was created.
    #[serde(rename = "CreatedAt", skip_serializing_if = "Option::is_none")]
    created_at: Option<String>,
    /// Low-level details about the volume, provided by the volume driver. Details are returned as a map with key/value pairs: `{\"key\":\"value\",\"key2\":\"value2\"}`.  The `Status` field is optional, and is omitted if the volume driver does not support this feature.
    #[serde(rename = "Status", skip_serializing_if = "Option::is_none")]
    status: Option<::std::collections::HashMap<String, Value>>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels")]
    labels: ::std::collections::HashMap<String, String>,
    /// The level at which the volume exists. Either `global` for cluster-wide, or `local` for machine level.
    #[serde(rename = "Scope")]
    scope: String,
    /// The driver specific options used when creating the volume.
    #[serde(rename = "Options")]
    options: ::std::collections::HashMap<String, String>,
    #[serde(rename = "UsageData", skip_serializing_if = "Option::is_none")]
    usage_data: Option<crate::models::VolumeUsageData>,
}

impl Volume {
    pub fn new(
        name: String,
        driver: String,
        mountpoint: String,
        labels: ::std::collections::HashMap<String, String>,
        scope: String,
        options: ::std::collections::HashMap<String, String>,
    ) -> Self {
        Volume {
            name: name,
            driver: driver,
            mountpoint: mountpoint,
            created_at: None,
            status: None,
            labels: labels,
            scope: scope,
            options: options,
            usage_data: None,
        }
    }

    pub fn set_name(&mut self, name: String) {
        self.name = name;
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn name(&self) -> &String {
        &self.name
    }

    pub fn set_driver(&mut self, driver: String) {
        self.driver = driver;
    }

    pub fn with_driver(mut self, driver: String) -> Self {
        self.driver = driver;
        self
    }

    pub fn driver(&self) -> &String {
        &self.driver
    }

    pub fn set_mountpoint(&mut self, mountpoint: String) {
        self.mountpoint = mountpoint;
    }

    pub fn with_mountpoint(mut self, mountpoint: String) -> Self {
        self.mountpoint = mountpoint;
        self
    }

    pub fn mountpoint(&self) -> &String {
        &self.mountpoint
    }

    pub fn set_created_at(&mut self, created_at: String) {
        self.created_at = Some(created_at);
    }

    pub fn with_created_at(mut self, created_at: String) -> Self {
        self.created_at = Some(created_at);
        self
    }

    pub fn created_at(&self) -> Option<&str> {
        self.created_at.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_created_at(&mut self) {
        self.created_at = None;
    }

    pub fn set_status(&mut self, status: ::std::collections::HashMap<String, Value>) {
        self.status = Some(status);
    }

    pub fn with_status(mut self, status: ::std::collections::HashMap<String, Value>) -> Self {
        self.status = Some(status);
        self
    }

    pub fn status(&self) -> Option<&::std::collections::HashMap<String, Value>> {
        self.status.as_ref()
    }

    pub fn reset_status(&mut self) {
        self.status = None;
    }

    pub fn set_labels(&mut self, labels: ::std::collections::HashMap<String, String>) {
        self.labels = labels;
    }

    pub fn with_labels(mut self, labels: ::std::collections::HashMap<String, String>) -> Self {
        self.labels = labels;
        self
    }

    pub fn labels(&self) -> &::std::collections::HashMap<String, String> {
        &self.labels
    }

    pub fn set_scope(&mut self, scope: String) {
        self.scope = scope;
    }

    pub fn with_scope(mut self, scope: String) -> Self {
        self.scope = scope;
        self
    }

    pub fn scope(&self) -> &String {
        &self.scope
    }

    pub fn set_options(&mut self, options: ::std::collections::HashMap<String, String>) {
        self.options = options;
    }

    pub fn with_options(mut self, options: ::std::collections::HashMap<String, String>) -> Self {
        self.options = options;
        self
    }

    pub fn options(&self) -> &::std::collections::HashMap<String, String> {
        &self.options
    }

    pub fn set_usage_data(&mut self, usage_data: crate::models::VolumeUsageData) {
        self.usage_data = Some(usage_data);
    }

    pub fn with_usage_data(mut self, usage_data: crate::models::VolumeUsageData) -> Self {
        self.usage_data = Some(usage_data);
        self
    }

    pub fn usage_data(&self) -> Option<&crate::models::VolumeUsageData> {
        self.usage_data.as_ref()
    }

    pub fn reset_usage_data(&mut self) {
        self.usage_data = None;
    }
}
