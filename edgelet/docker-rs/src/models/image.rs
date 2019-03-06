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
pub struct Image {
    #[serde(rename = "Id")]
    id: String,
    #[serde(rename = "RepoTags", skip_serializing_if = "Option::is_none")]
    repo_tags: Option<Vec<String>>,
    #[serde(rename = "RepoDigests", skip_serializing_if = "Option::is_none")]
    repo_digests: Option<Vec<String>>,
    #[serde(rename = "Parent")]
    parent: String,
    #[serde(rename = "Comment")]
    comment: String,
    #[serde(rename = "Created")]
    created: String,
    #[serde(rename = "Container")]
    container: String,
    #[serde(rename = "ContainerConfig", skip_serializing_if = "Option::is_none")]
    container_config: Option<crate::models::ContainerConfig>,
    #[serde(rename = "DockerVersion")]
    docker_version: String,
    #[serde(rename = "Author")]
    author: String,
    #[serde(rename = "Config", skip_serializing_if = "Option::is_none")]
    config: Option<crate::models::ContainerConfig>,
    #[serde(rename = "Architecture")]
    architecture: String,
    #[serde(rename = "Os")]
    os: String,
    #[serde(rename = "OsVersion", skip_serializing_if = "Option::is_none")]
    os_version: Option<String>,
    #[serde(rename = "Size")]
    size: i64,
    #[serde(rename = "VirtualSize")]
    virtual_size: i64,
    #[serde(rename = "GraphDriver")]
    graph_driver: crate::models::GraphDriverData,
    #[serde(rename = "RootFS")]
    root_fs: crate::models::ImageRootFs,
    #[serde(rename = "Metadata", skip_serializing_if = "Option::is_none")]
    metadata: Option<crate::models::ImageMetadata>,
}

impl Image {
    pub fn new(
        id: String,
        parent: String,
        comment: String,
        created: String,
        container: String,
        docker_version: String,
        author: String,
        architecture: String,
        os: String,
        size: i64,
        virtual_size: i64,
        graph_driver: crate::models::GraphDriverData,
        root_fs: crate::models::ImageRootFs,
    ) -> Self {
        Image {
            id: id,
            repo_tags: None,
            repo_digests: None,
            parent: parent,
            comment: comment,
            created: created,
            container: container,
            container_config: None,
            docker_version: docker_version,
            author: author,
            config: None,
            architecture: architecture,
            os: os,
            os_version: None,
            size: size,
            virtual_size: virtual_size,
            graph_driver: graph_driver,
            root_fs: root_fs,
            metadata: None,
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

    pub fn set_repo_tags(&mut self, repo_tags: Vec<String>) {
        self.repo_tags = Some(repo_tags);
    }

    pub fn with_repo_tags(mut self, repo_tags: Vec<String>) -> Self {
        self.repo_tags = Some(repo_tags);
        self
    }

    pub fn repo_tags(&self) -> Option<&[String]> {
        self.repo_tags.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_repo_tags(&mut self) {
        self.repo_tags = None;
    }

    pub fn set_repo_digests(&mut self, repo_digests: Vec<String>) {
        self.repo_digests = Some(repo_digests);
    }

    pub fn with_repo_digests(mut self, repo_digests: Vec<String>) -> Self {
        self.repo_digests = Some(repo_digests);
        self
    }

    pub fn repo_digests(&self) -> Option<&[String]> {
        self.repo_digests.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_repo_digests(&mut self) {
        self.repo_digests = None;
    }

    pub fn set_parent(&mut self, parent: String) {
        self.parent = parent;
    }

    pub fn with_parent(mut self, parent: String) -> Self {
        self.parent = parent;
        self
    }

    pub fn parent(&self) -> &String {
        &self.parent
    }

    pub fn set_comment(&mut self, comment: String) {
        self.comment = comment;
    }

    pub fn with_comment(mut self, comment: String) -> Self {
        self.comment = comment;
        self
    }

    pub fn comment(&self) -> &String {
        &self.comment
    }

    pub fn set_created(&mut self, created: String) {
        self.created = created;
    }

    pub fn with_created(mut self, created: String) -> Self {
        self.created = created;
        self
    }

    pub fn created(&self) -> &String {
        &self.created
    }

    pub fn set_container(&mut self, container: String) {
        self.container = container;
    }

    pub fn with_container(mut self, container: String) -> Self {
        self.container = container;
        self
    }

    pub fn container(&self) -> &String {
        &self.container
    }

    pub fn set_container_config(&mut self, container_config: crate::models::ContainerConfig) {
        self.container_config = Some(container_config);
    }

    pub fn with_container_config(
        mut self,
        container_config: crate::models::ContainerConfig,
    ) -> Self {
        self.container_config = Some(container_config);
        self
    }

    pub fn container_config(&self) -> Option<&crate::models::ContainerConfig> {
        self.container_config.as_ref()
    }

    pub fn reset_container_config(&mut self) {
        self.container_config = None;
    }

    pub fn set_docker_version(&mut self, docker_version: String) {
        self.docker_version = docker_version;
    }

    pub fn with_docker_version(mut self, docker_version: String) -> Self {
        self.docker_version = docker_version;
        self
    }

    pub fn docker_version(&self) -> &String {
        &self.docker_version
    }

    pub fn set_author(&mut self, author: String) {
        self.author = author;
    }

    pub fn with_author(mut self, author: String) -> Self {
        self.author = author;
        self
    }

    pub fn author(&self) -> &String {
        &self.author
    }

    pub fn set_config(&mut self, config: crate::models::ContainerConfig) {
        self.config = Some(config);
    }

    pub fn with_config(mut self, config: crate::models::ContainerConfig) -> Self {
        self.config = Some(config);
        self
    }

    pub fn config(&self) -> Option<&crate::models::ContainerConfig> {
        self.config.as_ref()
    }

    pub fn reset_config(&mut self) {
        self.config = None;
    }

    pub fn set_architecture(&mut self, architecture: String) {
        self.architecture = architecture;
    }

    pub fn with_architecture(mut self, architecture: String) -> Self {
        self.architecture = architecture;
        self
    }

    pub fn architecture(&self) -> &String {
        &self.architecture
    }

    pub fn set_os(&mut self, os: String) {
        self.os = os;
    }

    pub fn with_os(mut self, os: String) -> Self {
        self.os = os;
        self
    }

    pub fn os(&self) -> &String {
        &self.os
    }

    pub fn set_os_version(&mut self, os_version: String) {
        self.os_version = Some(os_version);
    }

    pub fn with_os_version(mut self, os_version: String) -> Self {
        self.os_version = Some(os_version);
        self
    }

    pub fn os_version(&self) -> Option<&str> {
        self.os_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_os_version(&mut self) {
        self.os_version = None;
    }

    pub fn set_size(&mut self, size: i64) {
        self.size = size;
    }

    pub fn with_size(mut self, size: i64) -> Self {
        self.size = size;
        self
    }

    pub fn size(&self) -> &i64 {
        &self.size
    }

    pub fn set_virtual_size(&mut self, virtual_size: i64) {
        self.virtual_size = virtual_size;
    }

    pub fn with_virtual_size(mut self, virtual_size: i64) -> Self {
        self.virtual_size = virtual_size;
        self
    }

    pub fn virtual_size(&self) -> &i64 {
        &self.virtual_size
    }

    pub fn set_graph_driver(&mut self, graph_driver: crate::models::GraphDriverData) {
        self.graph_driver = graph_driver;
    }

    pub fn with_graph_driver(mut self, graph_driver: crate::models::GraphDriverData) -> Self {
        self.graph_driver = graph_driver;
        self
    }

    pub fn graph_driver(&self) -> &crate::models::GraphDriverData {
        &self.graph_driver
    }

    pub fn set_root_fs(&mut self, root_fs: crate::models::ImageRootFs) {
        self.root_fs = root_fs;
    }

    pub fn with_root_fs(mut self, root_fs: crate::models::ImageRootFs) -> Self {
        self.root_fs = root_fs;
        self
    }

    pub fn root_fs(&self) -> &crate::models::ImageRootFs {
        &self.root_fs
    }

    pub fn set_metadata(&mut self, metadata: crate::models::ImageMetadata) {
        self.metadata = Some(metadata);
    }

    pub fn with_metadata(mut self, metadata: crate::models::ImageMetadata) -> Self {
        self.metadata = Some(metadata);
        self
    }

    pub fn metadata(&self) -> Option<&crate::models::ImageMetadata> {
        self.metadata.as_ref()
    }

    pub fn reset_metadata(&mut self) {
        self.metadata = None;
    }
}
