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
pub struct InlineResponse20011 {
    #[serde(rename = "Version", skip_serializing_if = "Option::is_none")]
    version: Option<String>,
    #[serde(rename = "ApiVersion", skip_serializing_if = "Option::is_none")]
    api_version: Option<String>,
    #[serde(rename = "MinAPIVersion", skip_serializing_if = "Option::is_none")]
    min_api_version: Option<String>,
    #[serde(rename = "GitCommit", skip_serializing_if = "Option::is_none")]
    git_commit: Option<String>,
    #[serde(rename = "GoVersion", skip_serializing_if = "Option::is_none")]
    go_version: Option<String>,
    #[serde(rename = "Os", skip_serializing_if = "Option::is_none")]
    os: Option<String>,
    #[serde(rename = "Arch", skip_serializing_if = "Option::is_none")]
    arch: Option<String>,
    #[serde(rename = "KernelVersion", skip_serializing_if = "Option::is_none")]
    kernel_version: Option<String>,
    #[serde(rename = "Experimental", skip_serializing_if = "Option::is_none")]
    experimental: Option<bool>,
    #[serde(rename = "BuildTime", skip_serializing_if = "Option::is_none")]
    build_time: Option<String>,
}

impl InlineResponse20011 {
    pub fn new() -> Self {
        InlineResponse20011 {
            version: None,
            api_version: None,
            min_api_version: None,
            git_commit: None,
            go_version: None,
            os: None,
            arch: None,
            kernel_version: None,
            experimental: None,
            build_time: None,
        }
    }

    pub fn set_version(&mut self, version: String) {
        self.version = Some(version);
    }

    pub fn with_version(mut self, version: String) -> Self {
        self.version = Some(version);
        self
    }

    pub fn version(&self) -> Option<&str> {
        self.version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_version(&mut self) {
        self.version = None;
    }

    pub fn set_api_version(&mut self, api_version: String) {
        self.api_version = Some(api_version);
    }

    pub fn with_api_version(mut self, api_version: String) -> Self {
        self.api_version = Some(api_version);
        self
    }

    pub fn api_version(&self) -> Option<&str> {
        self.api_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_api_version(&mut self) {
        self.api_version = None;
    }

    pub fn set_min_api_version(&mut self, min_api_version: String) {
        self.min_api_version = Some(min_api_version);
    }

    pub fn with_min_api_version(mut self, min_api_version: String) -> Self {
        self.min_api_version = Some(min_api_version);
        self
    }

    pub fn min_api_version(&self) -> Option<&str> {
        self.min_api_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_min_api_version(&mut self) {
        self.min_api_version = None;
    }

    pub fn set_git_commit(&mut self, git_commit: String) {
        self.git_commit = Some(git_commit);
    }

    pub fn with_git_commit(mut self, git_commit: String) -> Self {
        self.git_commit = Some(git_commit);
        self
    }

    pub fn git_commit(&self) -> Option<&str> {
        self.git_commit.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_git_commit(&mut self) {
        self.git_commit = None;
    }

    pub fn set_go_version(&mut self, go_version: String) {
        self.go_version = Some(go_version);
    }

    pub fn with_go_version(mut self, go_version: String) -> Self {
        self.go_version = Some(go_version);
        self
    }

    pub fn go_version(&self) -> Option<&str> {
        self.go_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_go_version(&mut self) {
        self.go_version = None;
    }

    pub fn set_os(&mut self, os: String) {
        self.os = Some(os);
    }

    pub fn with_os(mut self, os: String) -> Self {
        self.os = Some(os);
        self
    }

    pub fn os(&self) -> Option<&str> {
        self.os.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_os(&mut self) {
        self.os = None;
    }

    pub fn set_arch(&mut self, arch: String) {
        self.arch = Some(arch);
    }

    pub fn with_arch(mut self, arch: String) -> Self {
        self.arch = Some(arch);
        self
    }

    pub fn arch(&self) -> Option<&str> {
        self.arch.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_arch(&mut self) {
        self.arch = None;
    }

    pub fn set_kernel_version(&mut self, kernel_version: String) {
        self.kernel_version = Some(kernel_version);
    }

    pub fn with_kernel_version(mut self, kernel_version: String) -> Self {
        self.kernel_version = Some(kernel_version);
        self
    }

    pub fn kernel_version(&self) -> Option<&str> {
        self.kernel_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_kernel_version(&mut self) {
        self.kernel_version = None;
    }

    pub fn set_experimental(&mut self, experimental: bool) {
        self.experimental = Some(experimental);
    }

    pub fn with_experimental(mut self, experimental: bool) -> Self {
        self.experimental = Some(experimental);
        self
    }

    pub fn experimental(&self) -> Option<&bool> {
        self.experimental.as_ref()
    }

    pub fn reset_experimental(&mut self) {
        self.experimental = None;
    }

    pub fn set_build_time(&mut self, build_time: String) {
        self.build_time = Some(build_time);
    }

    pub fn with_build_time(mut self, build_time: String) -> Self {
        self.build_time = Some(build_time);
        self
    }

    pub fn build_time(&self) -> Option<&str> {
        self.build_time.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_build_time(&mut self) {
        self.build_time = None;
    }
}
