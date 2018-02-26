/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Mount {
    /// Container path.
    #[serde(rename = "Target")]
    target: Option<String>,
    /// Mount source (e.g. a volume name, a host path).
    #[serde(rename = "Source")]
    source: Option<String>,
    /// The mount type. Available types:  - `bind` Mounts a file or directory from the host into the container. Must exist prior to creating the container. - `volume` Creates a volume with the given name and options (or uses a pre-existing volume with the same name and options). These are **not** removed when the container is removed. - `tmpfs` Create a tmpfs with the given options. The mount source cannot be specified for tmpfs.
    #[serde(rename = "Type")]
    _type: Option<String>,
    /// Whether the mount should be read-only.
    #[serde(rename = "ReadOnly")]
    read_only: Option<bool>,
    /// The consistency requirement for the mount: `default`, `consistent`, `cached`, or `delegated`.
    #[serde(rename = "Consistency")]
    consistency: Option<String>,
    #[serde(rename = "BindOptions")] bind_options: Option<::models::MountBindOptions>,
    #[serde(rename = "VolumeOptions")] volume_options: Option<::models::MountVolumeOptions>,
    #[serde(rename = "TmpfsOptions")] tmpfs_options: Option<::models::MountTmpfsOptions>,
}

impl Mount {
    pub fn new() -> Mount {
        Mount {
            target: None,
            source: None,
            _type: None,
            read_only: None,
            consistency: None,
            bind_options: None,
            volume_options: None,
            tmpfs_options: None,
        }
    }

    pub fn set_target(&mut self, target: String) {
        self.target = Some(target);
    }

    pub fn with_target(mut self, target: String) -> Mount {
        self.target = Some(target);
        self
    }

    pub fn target(&self) -> Option<&String> {
        self.target.as_ref()
    }

    pub fn reset_target(&mut self) {
        self.target = None;
    }

    pub fn set_source(&mut self, source: String) {
        self.source = Some(source);
    }

    pub fn with_source(mut self, source: String) -> Mount {
        self.source = Some(source);
        self
    }

    pub fn source(&self) -> Option<&String> {
        self.source.as_ref()
    }

    pub fn reset_source(&mut self) {
        self.source = None;
    }

    pub fn set__type(&mut self, _type: String) {
        self._type = Some(_type);
    }

    pub fn with__type(mut self, _type: String) -> Mount {
        self._type = Some(_type);
        self
    }

    pub fn _type(&self) -> Option<&String> {
        self._type.as_ref()
    }

    pub fn reset__type(&mut self) {
        self._type = None;
    }

    pub fn set_read_only(&mut self, read_only: bool) {
        self.read_only = Some(read_only);
    }

    pub fn with_read_only(mut self, read_only: bool) -> Mount {
        self.read_only = Some(read_only);
        self
    }

    pub fn read_only(&self) -> Option<&bool> {
        self.read_only.as_ref()
    }

    pub fn reset_read_only(&mut self) {
        self.read_only = None;
    }

    pub fn set_consistency(&mut self, consistency: String) {
        self.consistency = Some(consistency);
    }

    pub fn with_consistency(mut self, consistency: String) -> Mount {
        self.consistency = Some(consistency);
        self
    }

    pub fn consistency(&self) -> Option<&String> {
        self.consistency.as_ref()
    }

    pub fn reset_consistency(&mut self) {
        self.consistency = None;
    }

    pub fn set_bind_options(&mut self, bind_options: ::models::MountBindOptions) {
        self.bind_options = Some(bind_options);
    }

    pub fn with_bind_options(mut self, bind_options: ::models::MountBindOptions) -> Mount {
        self.bind_options = Some(bind_options);
        self
    }

    pub fn bind_options(&self) -> Option<&::models::MountBindOptions> {
        self.bind_options.as_ref()
    }

    pub fn reset_bind_options(&mut self) {
        self.bind_options = None;
    }

    pub fn set_volume_options(&mut self, volume_options: ::models::MountVolumeOptions) {
        self.volume_options = Some(volume_options);
    }

    pub fn with_volume_options(mut self, volume_options: ::models::MountVolumeOptions) -> Mount {
        self.volume_options = Some(volume_options);
        self
    }

    pub fn volume_options(&self) -> Option<&::models::MountVolumeOptions> {
        self.volume_options.as_ref()
    }

    pub fn reset_volume_options(&mut self) {
        self.volume_options = None;
    }

    pub fn set_tmpfs_options(&mut self, tmpfs_options: ::models::MountTmpfsOptions) {
        self.tmpfs_options = Some(tmpfs_options);
    }

    pub fn with_tmpfs_options(mut self, tmpfs_options: ::models::MountTmpfsOptions) -> Mount {
        self.tmpfs_options = Some(tmpfs_options);
        self
    }

    pub fn tmpfs_options(&self) -> Option<&::models::MountTmpfsOptions> {
        self.tmpfs_options.as_ref()
    }

    pub fn reset_tmpfs_options(&mut self) {
        self.tmpfs_options = None;
    }
}
