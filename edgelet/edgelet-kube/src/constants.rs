// Copyright (c) Microsoft. All rights reserved.

pub const EDGE_EDGE_AGENT_NAME: &str = "edgeagent";

pub const EDGE_MODULE_LABEL: &str = "net.azure-devices.edge.module";

pub const EDGE_ORIGINAL_MODULEID: &str = "net.azure-devices.edge.original-moduleid";

pub const EDGE_DEVICE_LABEL: &str = "net.azure-devices.edge.deviceid";

pub const EDGE_HUBNAME_LABEL: &str = "net.azure-devices.edge.hub";

pub const PROXY_CONTAINER_NAME: &str = "proxy";

pub const PROXY_CONFIG_VOLUME_NAME: &str = "config-volume";

pub const PROXY_TRUST_BUNDLE_VOLUME_NAME: &str = "trust-bundle-volume";

pub const PROXY_TRUST_BUNDLE_FILENAME: &str = "trust_bundle.pem";

pub const PULL_SECRET_DATA_NAME: &str = ".dockerconfigjson";

pub const PULL_SECRET_DATA_TYPE: &str = "kubernetes.io/dockerconfigjson";

pub const EDGE_AGENT_MODE: &str = "kubernetes";

pub mod env {
    pub const USE_PERSISTENT_VOLUME_KEY: &str = "USE_PERSISTENT_VOLUMES";

    pub const EDGE_AGENT_MODE_KEY: &str = "Mode";

    pub const PROXY_IMAGE_KEY: &str = "ProxyImage";

    pub const PROXY_CONFIG_VOLUME_KEY: &str = "ProxyConfigVolume";

    pub const PROXY_CONFIG_MAP_NAME_KEY: &str = "ProxyConfigMapName";

    pub const PROXY_CONFIG_PATH_KEY: &str = "ProxyConfigPath";

    pub const PROXY_TRUST_BUNDLE_VOLUME_KEY: &str = "ProxyTrustBundleVolume";

    pub const PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY: &str = "ProxyTrustBundleConfigMapName";

    pub const PROXY_TRUST_BUNDLE_PATH_KEY: &str = "ProxyTrustBundlePath";

    pub const NAMESPACE_KEY: &str = "K8sNamespace";

    pub const EDGE_NETWORKID_KEY: &str = "NetworkId";
}
