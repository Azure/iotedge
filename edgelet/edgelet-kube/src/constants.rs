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

pub mod env {
    pub const PROXY_IMAGE_KEY: &str = "ProxyImage";

    pub const PROXY_CONFIG_VOLUME_KEY: &str = "ProxyConfigVolume";

    pub const PROXY_CONFIG_MAP_NAME_KEY: &str = "ProxyConfigMapName";

    pub const PROXY_CONFIG_PATH_KEY: &str = "ProxyConfigPath";

    pub const PROXY_TRUST_BUNDLE_VOLUME_KEY: &str = "ProxyTrustBundleVolume";

    pub const PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY: &str = "ProxyTrustBundleConfigMapName";

    pub const PROXY_TRUST_BUNDLE_PATH_KEY: &str = "ProxyTrustBundlePath";

    pub const PROXY_IMAGE_PULL_SECRET_NAME_KEY: &str = "ProxyImagePullSecretName";

    pub const NAMESPACE_KEY: &str = "K8sNamespace";

    pub const EDGE_NETWORK_ID_KEY: &str = "NetworkId";

    pub const EDGE_OBJECT_OWNER_API_VERSION_KEY: &str = "EdgeK8sObjectOwnerApiVersion";

    pub const EDGE_OBJECT_OWNER_KIND_KEY: &str = "EdgeK8sObjectOwnerKind";

    pub const EDGE_OBJECT_OWNER_NAME_KEY: &str = "EdgeK8sObjectOwnerName";

    pub const EDGE_OBJECT_OWNER_UID_KEY: &str = "EdgeK8sObjectOwnerUid";
}
