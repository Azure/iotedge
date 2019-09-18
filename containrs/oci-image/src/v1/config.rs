use std::collections::HashMap;

use serde::{Deserialize, Serialize};

use super::{media_type, MediaType};

#[derive(Debug, Serialize, Deserialize)]
pub struct EmptyObj {}

/// ImageConfig defines the execution parameters which should be used as a base
/// when running a container using an image.
#[derive(Debug, Serialize, Deserialize)]
pub struct ImageConfig {
    /// User defines the username or UID which the process in the container
    /// should run as.
    #[serde(rename = "User", skip_serializing_if = "Option::is_none")]
    pub user: Option<String>,

    /// ExposedPorts a set of ports to expose from a container running this
    /// image.
    #[serde(rename = "ExposedPorts", skip_serializing_if = "Option::is_none")]
    pub exposed_ports: Option<HashMap<String, EmptyObj>>,

    /// Env is a list of environment variables to be used in a container.
    #[serde(rename = "Env", skip_serializing_if = "Option::is_none")]
    pub env: Option<Vec<String>>,

    /// Entrypoint defines a list of arguments to use as the command to execute
    /// when the container starts.
    #[serde(rename = "Entrypoint", skip_serializing_if = "Option::is_none")]
    pub entrypoint: Option<Vec<String>>,

    /// Cmd defines the default arguments to the entrypoint of the container.
    #[serde(rename = "Cmd", skip_serializing_if = "Option::is_none")]
    pub cmd: Option<Vec<String>>,

    /// Volumes is a set of directories describing where the process is likely
    /// write data specific to a container instance.
    #[serde(rename = "Volumes", skip_serializing_if = "Option::is_none")]
    pub volumes: Option<HashMap<String, EmptyObj>>,

    /// WorkingDir sets the current working directory of the entrypoint process
    /// in the container.
    #[serde(rename = "WorkingDir", skip_serializing_if = "Option::is_none")]
    pub working_dir: Option<String>,

    /// Labels contains arbitrary metadata for the container.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    pub labels: Option<HashMap<String, String>>,

    /// StopSignal contains the system call signal that will be sent to the
    /// container to exit.
    #[serde(rename = "StopSignal", skip_serializing_if = "Option::is_none")]
    pub stop_signal: Option<String>,
}

/// RootFS describes a layer content addresses
#[derive(Debug, Serialize, Deserialize)]
pub struct RootFS {
    /// Type is the type of the rootfs.
    #[serde(rename = "type")]
    pub rootfs_type: String, // avoids raw identifier syntax (i.e: `pub r#type: String`)

    /// DiffIDs is an array of layer content hashes (DiffIDs), in order from
    /// bottom-most to top-most.
    // TODO: use actual digest type instead of a plain string
    #[serde(rename = "diff_ids")]
    pub diff_ids: Vec<String>,
}

/// History describes the history of a layer.
#[derive(Debug, Serialize, Deserialize)]
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

/// Image is the JSON structure which describes some basic information about the
/// image. This provides the `application/vnd.oci.image.config.v1+json`
/// mediatype when marshalled to JSON.
#[derive(Debug, Serialize, Deserialize)]
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
    #[serde(rename = "architecture")]
    pub architecture: String,

    /// OS is the name of the operating system which the image is built to run
    /// on.
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
