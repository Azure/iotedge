/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// InlineResponse200State : The state of the container.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct InlineResponse200State {
    /// The status of the container. For example, `\"running\"` or `\"exited\"`.
    #[serde(rename = "Status", skip_serializing_if = "Option::is_none")]
    status: Option<String>,
    /// Whether this container is running.  Note that a running container can be _paused_. The `Running` and `Paused` booleans are not mutually exclusive:  When pausing a container (on Linux), the cgroups freezer is used to suspend all processes in the container. Freezing the process requires the process to be running. As a result, paused containers are both `Running` _and_ `Paused`.  Use the `Status` field instead to determine if a container's state is \"running\".
    #[serde(rename = "Running", skip_serializing_if = "Option::is_none")]
    running: Option<bool>,
    /// Whether this container is paused.
    #[serde(rename = "Paused", skip_serializing_if = "Option::is_none")]
    paused: Option<bool>,
    /// Whether this container is restarting.
    #[serde(rename = "Restarting", skip_serializing_if = "Option::is_none")]
    restarting: Option<bool>,
    /// Whether this container has been killed because it ran out of memory.
    #[serde(rename = "OOMKilled", skip_serializing_if = "Option::is_none")]
    oom_killed: Option<bool>,
    #[serde(rename = "Dead", skip_serializing_if = "Option::is_none")]
    dead: Option<bool>,
    /// The process ID of this container
    #[serde(rename = "Pid", skip_serializing_if = "Option::is_none")]
    pid: Option<i32>,
    /// The last exit code of this container
    #[serde(rename = "ExitCode", skip_serializing_if = "Option::is_none")]
    exit_code: Option<i64>,
    #[serde(rename = "Error", skip_serializing_if = "Option::is_none")]
    error: Option<String>,
    /// The time when this container was last started.
    #[serde(rename = "StartedAt", skip_serializing_if = "Option::is_none")]
    started_at: Option<String>,
    /// The time when this container last exited.
    #[serde(rename = "FinishedAt", skip_serializing_if = "Option::is_none")]
    finished_at: Option<String>,
}

impl InlineResponse200State {
    /// The state of the container.
    pub fn new() -> Self {
        InlineResponse200State {
            status: None,
            running: None,
            paused: None,
            restarting: None,
            oom_killed: None,
            dead: None,
            pid: None,
            exit_code: None,
            error: None,
            started_at: None,
            finished_at: None,
        }
    }

    pub fn set_status(&mut self, status: String) {
        self.status = Some(status);
    }

    pub fn with_status(mut self, status: String) -> Self {
        self.status = Some(status);
        self
    }

    pub fn status(&self) -> Option<&str> {
        self.status.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_status(&mut self) {
        self.status = None;
    }

    pub fn set_running(&mut self, running: bool) {
        self.running = Some(running);
    }

    pub fn with_running(mut self, running: bool) -> Self {
        self.running = Some(running);
        self
    }

    pub fn running(&self) -> Option<&bool> {
        self.running.as_ref()
    }

    pub fn reset_running(&mut self) {
        self.running = None;
    }

    pub fn set_paused(&mut self, paused: bool) {
        self.paused = Some(paused);
    }

    pub fn with_paused(mut self, paused: bool) -> Self {
        self.paused = Some(paused);
        self
    }

    pub fn paused(&self) -> Option<&bool> {
        self.paused.as_ref()
    }

    pub fn reset_paused(&mut self) {
        self.paused = None;
    }

    pub fn set_restarting(&mut self, restarting: bool) {
        self.restarting = Some(restarting);
    }

    pub fn with_restarting(mut self, restarting: bool) -> Self {
        self.restarting = Some(restarting);
        self
    }

    pub fn restarting(&self) -> Option<&bool> {
        self.restarting.as_ref()
    }

    pub fn reset_restarting(&mut self) {
        self.restarting = None;
    }

    pub fn set_oom_killed(&mut self, oom_killed: bool) {
        self.oom_killed = Some(oom_killed);
    }

    pub fn with_oom_killed(mut self, oom_killed: bool) -> Self {
        self.oom_killed = Some(oom_killed);
        self
    }

    pub fn oom_killed(&self) -> Option<&bool> {
        self.oom_killed.as_ref()
    }

    pub fn reset_oom_killed(&mut self) {
        self.oom_killed = None;
    }

    pub fn set_dead(&mut self, dead: bool) {
        self.dead = Some(dead);
    }

    pub fn with_dead(mut self, dead: bool) -> Self {
        self.dead = Some(dead);
        self
    }

    pub fn dead(&self) -> Option<&bool> {
        self.dead.as_ref()
    }

    pub fn reset_dead(&mut self) {
        self.dead = None;
    }

    pub fn set_pid(&mut self, pid: i32) {
        self.pid = Some(pid);
    }

    pub fn with_pid(mut self, pid: i32) -> Self {
        self.pid = Some(pid);
        self
    }

    pub fn pid(&self) -> Option<i32> {
        self.pid
    }

    pub fn reset_pid(&mut self) {
        self.pid = None;
    }

    pub fn set_exit_code(&mut self, exit_code: i64) {
        self.exit_code = Some(exit_code);
    }

    pub fn with_exit_code(mut self, exit_code: i64) -> Self {
        self.exit_code = Some(exit_code);
        self
    }

    pub fn exit_code(&self) -> Option<i64> {
        self.exit_code
    }

    pub fn reset_exit_code(&mut self) {
        self.exit_code = None;
    }

    pub fn set_error(&mut self, error: String) {
        self.error = Some(error);
    }

    pub fn with_error(mut self, error: String) -> Self {
        self.error = Some(error);
        self
    }

    pub fn error(&self) -> Option<&str> {
        self.error.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_error(&mut self) {
        self.error = None;
    }

    pub fn set_started_at(&mut self, started_at: String) {
        self.started_at = Some(started_at);
    }

    pub fn with_started_at(mut self, started_at: String) -> Self {
        self.started_at = Some(started_at);
        self
    }

    pub fn started_at(&self) -> Option<&str> {
        self.started_at.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_started_at(&mut self) {
        self.started_at = None;
    }

    pub fn set_finished_at(&mut self, finished_at: String) {
        self.finished_at = Some(finished_at);
    }

    pub fn with_finished_at(mut self, finished_at: String) -> Self {
        self.finished_at = Some(finished_at);
        self
    }

    pub fn finished_at(&self) -> Option<&str> {
        self.finished_at.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_finished_at(&mut self) {
        self.finished_at = None;
    }
}
