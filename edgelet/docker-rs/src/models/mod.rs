// Copyright (c) Microsoft. All rights reserved.

// Source: https://raw.githubusercontent.com/moby/moby/refs/heads/20.10/api/swagger.yaml
//
// These types have been hand-generated instead of using any code generator. In the past we had used a code generator,
// but we had needed to run it on a third-party swagger spec because the code generator was unable to handle the inline types
// in upstream's swagger spec. This third-party spec had existed for v1.34, but there is nothing equivalent for v1.41 etc.
// In any case, IoT Edge uses a small fraction of the Docker Engine API so it's simpler to just maintain the types and fields we need.
//
// Also see comment in `edgelet/docker-rs/src/apis/configuration.rs` for details of the API version situation.
//
// We do not want to restrict the properties that the user can set in their create options, because future versions of Docker can add new properties
// that we don't define here. So some types have a `#[serde(flatten)] other_properties: BTreeMap<String, Value>` field
// to collect all the extra properties that we don't have a struct field for. This is only needed for fields that are deserialized from user input
// and then reserialized to send to Docker.
//
// Note: We're using BTreeMap instead of HashMap because aziot-edged stores a hash of its local config (whose object representation uses this struct)
// to detect changes. Since different HashMaps with the same keys aren't guaranteed to serialize in the same order (and thus won't compare equal),
// we need to use another map type that can provide that guarantee.

mod auth_config;
pub use self::auth_config::AuthConfig;

mod container_config;
pub use self::container_config::ContainerConfig;

mod container_create_body;
pub use self::container_create_body::{
    ContainerCreateBody, ContainerCreateBodyNetworkingConfig, EndpointSettings,
};

mod container_inspect_response;
pub use self::container_inspect_response::{
    ContainerInspectResponse, ContainerInspectResponseState, MountPoint,
};

mod container_summary;
pub use self::container_summary::ContainerSummary;

mod container_top_response;
pub use self::container_top_response::ContainerTopResponse;

mod host_config;
pub use self::host_config::{HostConfig, HostConfigPortBindings};

mod image_summary;
pub use self::image_summary::ImageSummary;

mod ipam;
pub use self::ipam::Ipam;

mod network_config;
pub use self::network_config::NetworkConfig;

mod system_info;
pub use self::system_info::SystemInfo;
