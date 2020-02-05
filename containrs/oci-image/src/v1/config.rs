use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use oci_common::fixed_newtype;
use oci_common::types::EnvVar;
use oci_digest::Digest;

use super::{media_type, Annotations};
use crate::MediaType;

#[derive(Debug, Serialize, Deserialize, Default, PartialEq, Eq, Hash, Copy, Clone)]
pub struct EmptyObj {}

/// Image is the JSON structure which describes some basic information about the
/// image. This provides the `application/vnd.oci.image.config.v1+json`
/// mediatype when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Image {
    /// Created is the combined date and time at which the image was created,
    /// formatted as defined by RFC 3339, section 5.6.
    // TODO: use actual Time type instead of string
    #[serde(rename = "created", skip_serializing_if = "Option::is_none")]
    pub created: Option<String>,

    /// Author defines the name and/or email address of the person or entity
    /// which created and is responsible for maintaining the image.
    #[serde(rename = "author", skip_serializing_if = "Option::is_none")]
    pub author: Option<String>,

    /// Architecture is the CPU architecture which the binaries in this image
    /// are built to run on.
    ///
    /// Configurations SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for
    /// [GOARCH](https://golang.org/doc/install/source#environment).
    // TODO: this could be validated while deserializing
    #[serde(rename = "architecture")]
    pub architecture: String,

    /// OS is the name of the operating system which the image is built to run
    /// on.
    ///
    /// Configurations SHOULD use, and implementations SHOULD understand, values
    /// listed in the Go Language document for
    /// [GOOS](https://golang.org/doc/install/source#environment).
    // TODO: this could be validated while deserializing
    #[serde(rename = "os")]
    pub os: String,

    /// Config defines the execution parameters which should be used as a base
    /// when running a container using the image.
    #[serde(rename = "config", skip_serializing_if = "Option::is_none")]
    pub config: Option<ImageConfig>,

    /// RootFS references the layer content addresses used by the image.
    #[serde(rename = "rootfs")]
    pub rootfs: RootFS,

    /// History describes the history of each layer.
    #[serde(rename = "history", skip_serializing_if = "Option::is_none")]
    pub history: Option<Vec<History>>,
}

impl MediaType for Image {
    const MEDIA_TYPE: &'static str = media_type::IMAGE_CONFIG;
    const SIMILAR_MEDIA_TYPES: &'static [&'static str] =
        &["application/vnd.docker.container.image.v1+json"];
}

/// ImageConfig defines the execution parameters which should be used as a base
/// when running a container using an image.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct ImageConfig {
    /// The username or UID which is a platform-specific structure that allows
    /// specific control over which user the process run as. This acts as a
    /// default value to use when the value is not specified when creating a
    /// container.
    ///
    /// For Linux based systems, all of the following are valid:
    /// `user`, `uid`, `user:group`, `uid:gid`, `uid:group`, `user:gid`.
    ///
    /// If group/gid is not specified, the default group and supplementary
    /// groups of the given user/uid in /etc/passwd from the container are
    /// applied.
    #[serde(rename = "User", skip_serializing_if = "Option::is_none")]
    pub user: Option<String>,

    /// ExposedPorts a set of ports to expose from a container running this
    /// image.
    ///
    /// NOTE: This JSON structure value is unusual because it is a direct JSON
    /// serialization of the Go type map[string]struct{} and is represented in
    /// JSON as an object mapping its keys to an empty object.
    #[serde(rename = "ExposedPorts", skip_serializing_if = "Option::is_none")]
    pub exposed_ports: Option<HashMap<String, EmptyObj>>,

    /// Env is a list of environment variables to be used in a container.
    ///
    /// Entries are in the format of VARNAME=VARVALUE. These values act as
    /// defaults and are merged with any specified when creating a container.
    #[serde(rename = "Env", skip_serializing_if = "Option::is_none")]
    pub env: Option<Vec<EnvVar>>,

    /// Entrypoint defines a list of arguments to use as the command to execute
    /// when the container starts.
    #[serde(rename = "Entrypoint", skip_serializing_if = "Option::is_none")]
    pub entrypoint: Option<Vec<String>>,

    /// Cmd defines the default arguments to the entrypoint of the container.
    #[serde(rename = "Cmd", skip_serializing_if = "Option::is_none")]
    pub cmd: Option<Vec<String>>,

    /// Volumes is a set of directories describing where the process is likely
    /// write data specific to a container instance.
    ///
    /// NOTE: This JSON structure value is unusual because it is a direct JSON
    /// serialization of the Go type map[string]struct{} and is represented in
    /// JSON as an object mapping its keys to an empty object.
    #[serde(rename = "Volumes", skip_serializing_if = "Option::is_none")]
    pub volumes: Option<HashMap<String, EmptyObj>>,

    /// WorkingDir sets the current working directory of the entrypoint process
    /// in the container.
    #[serde(rename = "WorkingDir", skip_serializing_if = "Option::is_none")]
    pub working_dir: Option<String>,

    /// Labels contains arbitrary metadata for the container.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    pub labels: Option<Annotations>,

    /// StopSignal contains the system call signal that will be sent to the
    /// container to exit.
    ///
    /// The signal can be a signal name in the format `SIGNAME`, for instance
    /// `SIGKILL` or `SIGRTMIN+3`.
    #[serde(rename = "StopSignal", skip_serializing_if = "Option::is_none")]
    pub stop_signal: Option<String>,
}

/// RootFS describes a layer content addresses
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct RootFS {
    /// Type is the type of the rootfs.
    ///
    /// MUST be set to layers.
    #[serde(rename = "type")]
    pub type_: RootfsType,

    /// DiffIDs is an array of layer content hashes (DiffIDs), in order from
    /// bottom-most to top-most.
    #[serde(rename = "diff_ids")]
    pub diff_ids: Vec<Digest>,
}

/// History describes the history of a layer.
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct History {
    /// Created is the combined date and time at which the layer was created,
    /// formatted as defined by RFC 3339, section 5.6.
    // TODO: use actual Time type instead of string
    #[serde(rename = "created", skip_serializing_if = "Option::is_none")]
    pub created: Option<String>,

    /// CreatedBy is the command which created the layer.
    #[serde(rename = "created_by", skip_serializing_if = "Option::is_none")]
    pub created_by: Option<String>,

    /// Author is the author of the build point.
    #[serde(rename = "author", skip_serializing_if = "Option::is_none")]
    pub author: Option<String>,

    /// Comment is a custom message set when creating the layer.
    #[serde(rename = "comment", skip_serializing_if = "Option::is_none")]
    pub comment: Option<String>,

    /// EmptyLayer is used to mark if the history item created a filesystem
    /// diff.
    #[serde(rename = "empty_layer", skip_serializing_if = "Option::is_none")]
    pub empty_layer: Option<bool>,
}

fixed_newtype! {
    /// Wrapper around a String whose value is guaranteed to be "layers"
    pub struct RootfsType(String) == "layers";
    else "v1::Image must have .rootfs.rootfs_type = \"layers\"";
}
