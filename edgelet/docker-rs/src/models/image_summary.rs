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
pub struct ImageSummary {
    #[serde(rename = "Id")]
    id: String,
    #[serde(rename = "ParentId")]
    parent_id: String,
    #[serde(rename = "RepoTags")]
    repo_tags: Vec<String>,
    #[serde(rename = "RepoDigests")]
    repo_digests: Vec<String>,
    #[serde(rename = "Created")]
    created: i32,
    #[serde(rename = "Size")]
    size: i32,
    #[serde(rename = "SharedSize")]
    shared_size: i32,
    #[serde(rename = "VirtualSize")]
    virtual_size: i32,
    #[serde(rename = "Labels")]
    labels: ::std::collections::HashMap<String, String>,
    #[serde(rename = "Containers")]
    containers: i32,
}

impl ImageSummary {
    pub fn new(
        id: String,
        parent_id: String,
        repo_tags: Vec<String>,
        repo_digests: Vec<String>,
        created: i32,
        size: i32,
        shared_size: i32,
        virtual_size: i32,
        labels: ::std::collections::HashMap<String, String>,
        containers: i32,
    ) -> Self {
        ImageSummary {
            id: id,
            parent_id: parent_id,
            repo_tags: repo_tags,
            repo_digests: repo_digests,
            created: created,
            size: size,
            shared_size: shared_size,
            virtual_size: virtual_size,
            labels: labels,
            containers: containers,
        }
    }

    pub fn set_id(&mut self, id: String) {
        self.id = id;
    }

    pub fn with_id(mut self, id: String) -> Self {
        self.id = id;
        self
    }

    pub fn id(&self) -> &String {
        &self.id
    }

    pub fn set_parent_id(&mut self, parent_id: String) {
        self.parent_id = parent_id;
    }

    pub fn with_parent_id(mut self, parent_id: String) -> Self {
        self.parent_id = parent_id;
        self
    }

    pub fn parent_id(&self) -> &String {
        &self.parent_id
    }

    pub fn set_repo_tags(&mut self, repo_tags: Vec<String>) {
        self.repo_tags = repo_tags;
    }

    pub fn with_repo_tags(mut self, repo_tags: Vec<String>) -> Self {
        self.repo_tags = repo_tags;
        self
    }

    pub fn repo_tags(&self) -> &[String] {
        &self.repo_tags
    }

    pub fn set_repo_digests(&mut self, repo_digests: Vec<String>) {
        self.repo_digests = repo_digests;
    }

    pub fn with_repo_digests(mut self, repo_digests: Vec<String>) -> Self {
        self.repo_digests = repo_digests;
        self
    }

    pub fn repo_digests(&self) -> &[String] {
        &self.repo_digests
    }

    pub fn set_created(&mut self, created: i32) {
        self.created = created;
    }

    pub fn with_created(mut self, created: i32) -> Self {
        self.created = created;
        self
    }

    pub fn created(&self) -> &i32 {
        &self.created
    }

    pub fn set_size(&mut self, size: i32) {
        self.size = size;
    }

    pub fn with_size(mut self, size: i32) -> Self {
        self.size = size;
        self
    }

    pub fn size(&self) -> &i32 {
        &self.size
    }

    pub fn set_shared_size(&mut self, shared_size: i32) {
        self.shared_size = shared_size;
    }

    pub fn with_shared_size(mut self, shared_size: i32) -> Self {
        self.shared_size = shared_size;
        self
    }

    pub fn shared_size(&self) -> &i32 {
        &self.shared_size
    }

    pub fn set_virtual_size(&mut self, virtual_size: i32) {
        self.virtual_size = virtual_size;
    }

    pub fn with_virtual_size(mut self, virtual_size: i32) -> Self {
        self.virtual_size = virtual_size;
        self
    }

    pub fn virtual_size(&self) -> &i32 {
        &self.virtual_size
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

    pub fn set_containers(&mut self, containers: i32) {
        self.containers = containers;
    }

    pub fn with_containers(mut self, containers: i32) -> Self {
        self.containers = containers;
        self
    }

    pub fn containers(&self) -> &i32 {
        &self.containers
    }
}
