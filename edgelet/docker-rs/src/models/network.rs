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
pub struct Network {
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    #[serde(rename = "Id", skip_serializing_if = "Option::is_none")]
    id: Option<String>,
    #[serde(rename = "Created", skip_serializing_if = "Option::is_none")]
    created: Option<String>,
    #[serde(rename = "Scope", skip_serializing_if = "Option::is_none")]
    scope: Option<String>,
    #[serde(rename = "Driver", skip_serializing_if = "Option::is_none")]
    driver: Option<String>,
    #[serde(rename = "EnableIPv6", skip_serializing_if = "Option::is_none")]
    enable_i_pv6: Option<bool>,
    #[serde(rename = "IPAM", skip_serializing_if = "Option::is_none")]
    IPAM: Option<crate::models::Ipam>,
    #[serde(rename = "Internal", skip_serializing_if = "Option::is_none")]
    internal: Option<bool>,
    #[serde(rename = "Attachable", skip_serializing_if = "Option::is_none")]
    attachable: Option<bool>,
    #[serde(rename = "Ingress", skip_serializing_if = "Option::is_none")]
    ingress: Option<bool>,
    #[serde(rename = "Containers", skip_serializing_if = "Option::is_none")]
    containers: Option<::std::collections::HashMap<String, crate::models::NetworkContainer>>,
    #[serde(rename = "Options", skip_serializing_if = "Option::is_none")]
    options: Option<::std::collections::HashMap<String, String>>,
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
}

impl Network {
    pub fn new() -> Self {
        Network {
            name: None,
            id: None,
            created: None,
            scope: None,
            driver: None,
            enable_i_pv6: None,
            IPAM: None,
            internal: None,
            attachable: None,
            ingress: None,
            containers: None,
            options: None,
            labels: None,
        }
    }

    pub fn set_name(&mut self, name: String) {
        self.name = Some(name);
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = Some(name);
        self
    }

    pub fn name(&self) -> Option<&str> {
        self.name.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_name(&mut self) {
        self.name = None;
    }

    pub fn set_id(&mut self, id: String) {
        self.id = Some(id);
    }

    pub fn with_id(mut self, id: String) -> Self {
        self.id = Some(id);
        self
    }

    pub fn id(&self) -> Option<&str> {
        self.id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_id(&mut self) {
        self.id = None;
    }

    pub fn set_created(&mut self, created: String) {
        self.created = Some(created);
    }

    pub fn with_created(mut self, created: String) -> Self {
        self.created = Some(created);
        self
    }

    pub fn created(&self) -> Option<&str> {
        self.created.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_created(&mut self) {
        self.created = None;
    }

    pub fn set_scope(&mut self, scope: String) {
        self.scope = Some(scope);
    }

    pub fn with_scope(mut self, scope: String) -> Self {
        self.scope = Some(scope);
        self
    }

    pub fn scope(&self) -> Option<&str> {
        self.scope.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_scope(&mut self) {
        self.scope = None;
    }

    pub fn set_driver(&mut self, driver: String) {
        self.driver = Some(driver);
    }

    pub fn with_driver(mut self, driver: String) -> Self {
        self.driver = Some(driver);
        self
    }

    pub fn driver(&self) -> Option<&str> {
        self.driver.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_driver(&mut self) {
        self.driver = None;
    }

    pub fn set_enable_i_pv6(&mut self, enable_i_pv6: bool) {
        self.enable_i_pv6 = Some(enable_i_pv6);
    }

    pub fn with_enable_i_pv6(mut self, enable_i_pv6: bool) -> Self {
        self.enable_i_pv6 = Some(enable_i_pv6);
        self
    }

    pub fn enable_i_pv6(&self) -> Option<&bool> {
        self.enable_i_pv6.as_ref()
    }

    pub fn reset_enable_i_pv6(&mut self) {
        self.enable_i_pv6 = None;
    }

    pub fn set_IPAM(&mut self, IPAM: crate::models::Ipam) {
        self.IPAM = Some(IPAM);
    }

    pub fn with_IPAM(mut self, IPAM: crate::models::Ipam) -> Self {
        self.IPAM = Some(IPAM);
        self
    }

    pub fn IPAM(&self) -> Option<&crate::models::Ipam> {
        self.IPAM.as_ref()
    }

    pub fn reset_IPAM(&mut self) {
        self.IPAM = None;
    }

    pub fn set_internal(&mut self, internal: bool) {
        self.internal = Some(internal);
    }

    pub fn with_internal(mut self, internal: bool) -> Self {
        self.internal = Some(internal);
        self
    }

    pub fn internal(&self) -> Option<&bool> {
        self.internal.as_ref()
    }

    pub fn reset_internal(&mut self) {
        self.internal = None;
    }

    pub fn set_attachable(&mut self, attachable: bool) {
        self.attachable = Some(attachable);
    }

    pub fn with_attachable(mut self, attachable: bool) -> Self {
        self.attachable = Some(attachable);
        self
    }

    pub fn attachable(&self) -> Option<&bool> {
        self.attachable.as_ref()
    }

    pub fn reset_attachable(&mut self) {
        self.attachable = None;
    }

    pub fn set_ingress(&mut self, ingress: bool) {
        self.ingress = Some(ingress);
    }

    pub fn with_ingress(mut self, ingress: bool) -> Self {
        self.ingress = Some(ingress);
        self
    }

    pub fn ingress(&self) -> Option<&bool> {
        self.ingress.as_ref()
    }

    pub fn reset_ingress(&mut self) {
        self.ingress = None;
    }

    pub fn set_containers(
        &mut self,
        containers: ::std::collections::HashMap<String, crate::models::NetworkContainer>,
    ) {
        self.containers = Some(containers);
    }

    pub fn with_containers(
        mut self,
        containers: ::std::collections::HashMap<String, crate::models::NetworkContainer>,
    ) -> Self {
        self.containers = Some(containers);
        self
    }

    pub fn containers(
        &self,
    ) -> Option<&::std::collections::HashMap<String, crate::models::NetworkContainer>> {
        self.containers.as_ref()
    }

    pub fn reset_containers(&mut self) {
        self.containers = None;
    }

    pub fn set_options(&mut self, options: ::std::collections::HashMap<String, String>) {
        self.options = Some(options);
    }

    pub fn with_options(mut self, options: ::std::collections::HashMap<String, String>) -> Self {
        self.options = Some(options);
        self
    }

    pub fn options(&self) -> Option<&::std::collections::HashMap<String, String>> {
        self.options.as_ref()
    }

    pub fn reset_options(&mut self) {
        self.options = None;
    }

    pub fn set_labels(&mut self, labels: ::std::collections::HashMap<String, String>) {
        self.labels = Some(labels);
    }

    pub fn with_labels(mut self, labels: ::std::collections::HashMap<String, String>) -> Self {
        self.labels = Some(labels);
        self
    }

    pub fn labels(&self) -> Option<&::std::collections::HashMap<String, String>> {
        self.labels.as_ref()
    }

    pub fn reset_labels(&mut self) {
        self.labels = None;
    }
}
