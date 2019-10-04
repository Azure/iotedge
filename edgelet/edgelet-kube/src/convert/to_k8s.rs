// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::str;

use base64;
use docker::models::{AuthConfig, HostConfig};
use edgelet_core::{Certificate, ModuleSpec};
use edgelet_docker::DockerConfig;
use failure::ResultExt;
use k8s_openapi::api::apps::v1 as api_apps;
use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::api::rbac::v1 as api_rbac;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::ByteString;
use log::warn;
use serde_json;

use crate::constants::env::*;
use crate::constants::*;
use crate::convert::{sanitize_dns_domain, sanitize_dns_value};
use crate::error::{ErrorKind, PullImageErrorReason, Result};
use crate::settings::Settings;

// Use username and server from Docker AuthConfig to construct an image pull secret name.
fn auth_to_pull_secret_name(auth: &AuthConfig) -> Option<String> {
    match (auth.username(), auth.serveraddress()) {
        (Some(user), Some(server)) => {
            Some(format!("{}-{}", user.to_lowercase(), server.to_lowercase()))
        }
        _ => None,
    }
}

// AuthEntry models the JSON string needed for entryies in the image pull secrets.
#[derive(Debug, serde_derive::Serialize, serde_derive::Deserialize, Clone)]
struct AuthEntry {
    pub username: String,
    pub password: String,
    pub auth: String,
}

impl AuthEntry {
    pub fn new(username: String, password: String) -> AuthEntry {
        let auth = base64::encode(&format!("{}:{}", username, password));
        AuthEntry {
            username,
            password,
            auth,
        }
    }
}

// Auth represents the JSON string needed for image pull secrets.
// JSON struct is
// { "auths":
//   {"<registry>" :
//      { "username":"<user>",
//        "password":"<password>",
//        "email":"<email>" (not needed)
//        "auth":"<base64 of '<user>:<password>'>"
//       }
//   }
// }
#[derive(Debug, serde_derive::Serialize, serde_derive::Deserialize, Clone)]
struct Auth {
    pub auths: BTreeMap<String, AuthEntry>,
}

impl Auth {
    pub fn new(auths: BTreeMap<String, AuthEntry>) -> Auth {
        Auth { auths }
    }

    pub fn secret_data(&self) -> Result<ByteString> {
        let data =
            serde_json::to_vec(self).context(ErrorKind::PullImage(PullImageErrorReason::Json))?;
        Ok(ByteString(data))
    }
}

/// Converts Docker `AuthConfig` to a K8s image pull secret.
pub fn auth_to_image_pull_secret(
    namespace: &str,
    auth: &AuthConfig,
) -> Result<(String, api_core::Secret)> {
    let secret_name = auth_to_pull_secret_name(auth)
        .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthName))?;

    let registry = auth
        .serveraddress()
        .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthServerAddress))?;

    let user = auth
        .username()
        .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthUser))?;

    let password = auth
        .password()
        .ok_or_else(|| ErrorKind::PullImage(PullImageErrorReason::AuthPassword))?;

    let mut auths = BTreeMap::new();
    auths.insert(
        registry.to_string(),
        AuthEntry::new(user.to_string(), password.to_string()),
    );

    // construct a JSON string from "auths" structure
    let auth_string = Auth::new(auths).secret_data()?;

    // create a pull secret from auths string.
    let mut secret_data = BTreeMap::new();
    secret_data.insert(PULL_SECRET_DATA_NAME.to_string(), auth_string);
    Ok((
        secret_name.clone(),
        api_core::Secret {
            data: Some(secret_data),
            metadata: Some(api_meta::ObjectMeta {
                name: Some(secret_name),
                namespace: Some(namespace.to_string()),
                ..api_meta::ObjectMeta::default()
            }),
            type_: Some(PULL_SECRET_DATA_TYPE.to_string()),
            ..api_core::Secret::default()
        },
    ))
}

/// Converts Docker `ModuleSpec` to K8s `PodSpec`
fn spec_to_podspec(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
    module_label_value: String,
    module_image: String,
) -> Result<api_core::PodSpec> {
    // privileged container
    let security = spec
        .config()
        .create_options()
        .host_config()
        .and_then(HostConfig::privileged)
        .and_then(|privileged| {
            if *privileged {
                let context = api_core::SecurityContext {
                    privileged: Some(*privileged),
                    ..api_core::SecurityContext::default()
                };
                Some(context)
            } else {
                None
            }
        });

    // Environment Variables - use env from ModuleSpec
    let mut env_vars: Vec<api_core::EnvVar> = spec
        .env()
        .iter()
        .map(|(key, val)| api_core::EnvVar {
            name: key.clone(),
            value: Some(val.clone()),
            ..api_core::EnvVar::default()
        })
        .collect();
    // Pass along "USE_PERSISTENT_VOLUMES" to EdgeAgent
    if EDGE_EDGE_AGENT_NAME == module_label_value {
        if settings.use_pvc() {
            env_vars.push(env(USE_PERSISTENT_VOLUME_KEY, "True"));
        }

        env_vars.push(env(EDGE_NETWORKID_KEY, ""));
        env_vars.push(env(NAMESPACE_KEY, settings.namespace()));
        env_vars.push(env(EDGE_AGENT_MODE_KEY, EDGE_AGENT_MODE));
        env_vars.push(env(PROXY_IMAGE_KEY, settings.proxy_image()));
        env_vars.push(env(PROXY_CONFIG_VOLUME_KEY, PROXY_CONFIG_VOLUME_NAME));
        env_vars.push(env(
            PROXY_CONFIG_MAP_NAME_KEY,
            settings.proxy_config_map_name(),
        ));
        env_vars.push(env(PROXY_CONFIG_PATH_KEY, settings.proxy_config_path()));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY,
            settings.proxy_trust_bundle_config_map_name(),
        ));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_VOLUME_KEY,
            PROXY_TRUST_BUNDLE_VOLUME_NAME,
        ));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_PATH_KEY,
            settings.proxy_trust_bundle_path(),
        ));
    }

    // Bind/volume mounts
    // ConfigMap volume name is fixed: "config-volume"
    let proxy_config_volume_source = api_core::ConfigMapVolumeSource {
        name: Some(settings.proxy_config_map_name().to_string()),
        ..api_core::ConfigMapVolumeSource::default()
    };
    // Volume entry for proxy's config map
    let proxy_config_volume = api_core::Volume {
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        config_map: Some(proxy_config_volume_source),
        ..api_core::Volume::default()
    };

    // trust bundle ConfigMap volume name is fixed: "trust-bundle-volume"
    let trust_bundle_config_volume_source = api_core::ConfigMapVolumeSource {
        name: Some(settings.proxy_trust_bundle_config_map_name().to_string()),
        ..api_core::ConfigMapVolumeSource::default()
    };
    // Volume entry for proxy's trust bundle config map
    let trust_bundle_config_volume = api_core::Volume {
        name: PROXY_TRUST_BUNDLE_VOLUME_NAME.to_string(),
        config_map: Some(trust_bundle_config_volume_source),
        ..api_core::Volume::default()
    };

    let mut volumes = vec![proxy_config_volume, trust_bundle_config_volume];

    // Where to mount proxy config map
    let proxy_volume_mount = api_core::VolumeMount {
        mount_path: settings.proxy_config_path().to_string(),
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        read_only: Some(true),
        ..api_core::VolumeMount::default()
    };

    // Where to mount proxy trust bundle config map
    let trust_bundle_volume_mount = api_core::VolumeMount {
        mount_path: settings.proxy_trust_bundle_path().to_string(),
        name: PROXY_TRUST_BUNDLE_VOLUME_NAME.to_string(),
        read_only: Some(true),
        ..api_core::VolumeMount::default()
    };

    let proxy_volume_mounts = vec![proxy_volume_mount, trust_bundle_volume_mount];
    let mut volume_mounts = Vec::new();

    if let Some(binds) = spec
        .config()
        .create_options()
        .host_config()
        .as_ref()
        .and_then(|hc| hc.binds())
    {
        // Binds in Docker options are "source:target:options"
        // We will convert these to a Host Path Volume Source.
        for bind in binds.iter() {
            let bind_elements = bind.split(':').collect::<Vec<&str>>();
            let element_count = bind_elements.len();
            if element_count >= 2 {
                // If we have a valid bind mount, create a Volume with the
                // bind source as a host path.
                let bind_name = sanitize_dns_value(bind_elements[0])?;
                let host_path_volume_source = api_core::HostPathVolumeSource {
                    path: bind_elements[0].to_string(),
                    type_: Some("DirectoryOrCreate".to_string()),
                };
                let bind_volume = api_core::Volume {
                    name: bind_name.clone(),
                    host_path: Some(host_path_volume_source),
                    ..api_core::Volume::default()
                };
                // Then mount the source volume into the container at target.
                // Use bind options, if any.
                let bind_mount = api_core::VolumeMount {
                    mount_path: bind_elements[1].to_string(),
                    name: bind_name,
                    read_only: Some(element_count > 2 && bind_elements[2].contains("ro")),
                    ..api_core::VolumeMount::default()
                };

                volumes.push(bind_volume);
                volume_mounts.push(bind_mount);
            } else {
                warn!("Bind: bind mount did not follow format source:target[:ro|rw]");
            }
        }
    };
    if let Some(mounts) = spec
        .config()
        .create_options()
        .host_config()
        .as_ref()
        .and_then(|hc| hc.mounts())
    {
        // mounts are a structure, with type, source, target, readonly
        for mount in mounts.iter() {
            match mount._type() {
                Some("bind") => {
                    // Treat bind options as above: Host Path Volume Source.
                    if let (Some(source), Some(target)) = (mount.source(), mount.target()) {
                        let bind_name = sanitize_dns_value(source)?;

                        let host_path_volume_source = api_core::HostPathVolumeSource {
                            path: source.to_string(),
                            type_: Some("DirectoryOrCreate".to_string()),
                        };
                        let bind_volume = api_core::Volume {
                            name: bind_name.clone(),
                            host_path: Some(host_path_volume_source),
                            ..api_core::Volume::default()
                        };

                        let bind_mount = api_core::VolumeMount {
                            mount_path: target.to_string(),
                            name: bind_name,
                            read_only: mount.read_only().cloned(),
                            ..api_core::VolumeMount::default()
                        };

                        volumes.push(bind_volume);
                        volume_mounts.push(bind_mount);
                    } else {
                        warn!("Bind mount did not contain a source and target");
                    }
                }
                Some("volume") => {
                    // Treat volume mounts one of two ways:
                    // 1. if use_pvc is set, we assume the user has created a
                    // Persistent Volume and Claim named "source" for us to use.
                    // 2. is use_pvc is not set, we create a simple EmptyDir
                    // volume for this pod to use.  This volume is not persistent.
                    if let (Some(source), Some(target)) = (mount.source(), mount.target()) {
                        let volume_name = sanitize_dns_value(source)?;

                        let volume = if settings.use_pvc() {
                            api_core::Volume {
                                name: volume_name.clone(),
                                persistent_volume_claim: Some(
                                    api_core::PersistentVolumeClaimVolumeSource {
                                        claim_name: volume_name.clone(),
                                        read_only: mount.read_only().cloned(),
                                    },
                                ),
                                ..api_core::Volume::default()
                            }
                        } else {
                            api_core::Volume {
                                name: volume_name.clone(),
                                empty_dir: Some(api_core::EmptyDirVolumeSource::default()),
                                ..api_core::Volume::default()
                            }
                        };
                        let volume_mount = api_core::VolumeMount {
                            mount_path: target.to_string(),
                            name: volume_name,
                            read_only: mount.read_only().cloned(),
                            ..api_core::VolumeMount::default()
                        };
                        volumes.push(volume);
                        volume_mounts.push(volume_mount);
                    } else {
                        warn!("Volume mount did not contain a source and target");
                    }
                }
                _ => {
                    warn!("Mount type not 'bind' or 'volume'");
                }
            }
        }
    };
    //pull secrets
    let image_pull_secrets = spec.config().auth().and_then(|auth| {
        Some(vec![api_core::LocalObjectReference {
            name: auth_to_pull_secret_name(auth),
        }])
    });

    Ok(api_core::PodSpec {
        containers: vec![
            // module
            api_core::Container {
                name: module_label_value.clone(),
                env: Some(env_vars.clone()),
                image: Some(module_image),
                image_pull_policy: Some(settings.image_pull_policy().to_string()),
                security_context: security,
                volume_mounts: Some(volume_mounts),
                ..api_core::Container::default()
            },
            // proxy
            api_core::Container {
                name: PROXY_CONTAINER_NAME.to_string(),
                env: Some(env_vars),
                image: Some(settings.proxy_image().to_string()),
                image_pull_policy: Some(settings.image_pull_policy().to_string()),
                volume_mounts: Some(proxy_volume_mounts),
                ..api_core::Container::default()
            },
        ],
        image_pull_secrets,
        service_account_name: Some(module_label_value),
        volumes: Some(volumes),
        ..api_core::PodSpec::default()
    })
}

fn env<V: Into<String>>(key: &str, value: V) -> api_core::EnvVar {
    api_core::EnvVar {
        name: key.to_string(),
        value: Some(value.into()),
        ..api_core::EnvVar::default()
    }
}

/// Converts Docker Module Spec into a K8S Deployment.
pub fn spec_to_deployment(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
) -> Result<(String, api_apps::Deployment)> {
    // Set some values...
    let module_label_value = sanitize_dns_value(spec.name())?;
    let device_label_value =
        sanitize_dns_value(settings.device_id().ok_or(ErrorKind::MissingDeviceId)?)?;
    let hubname_label = sanitize_dns_domain(
        settings
            .iot_hub_hostname()
            .ok_or(ErrorKind::MissingHubName)?,
    )?;
    let deployment_name = module_label_value.clone();
    let module_image = spec.config().image().to_string();

    // Populate some labels:
    let mut pod_labels = BTreeMap::new();
    pod_labels.insert(EDGE_MODULE_LABEL.to_string(), module_label_value.clone());
    pod_labels.insert(EDGE_DEVICE_LABEL.to_string(), device_label_value);
    pod_labels.insert(EDGE_HUBNAME_LABEL.to_string(), hubname_label);

    let deployment_labels = pod_labels.clone();
    let selector_labels = pod_labels.clone();

    if let Some(spec_labels) = spec.config().create_options().labels() {
        for (label, value) in spec_labels.iter() {
            pod_labels.insert(label.clone(), value.clone());
        }
    }

    // annotations
    let mut annotations = BTreeMap::new();
    annotations.insert(EDGE_ORIGINAL_MODULEID.to_string(), spec.name().to_string());

    // Assemble everything
    let deployment = api_apps::Deployment {
        metadata: Some(api_meta::ObjectMeta {
            name: Some(deployment_name.clone()),
            namespace: Some(settings.namespace().to_string()),
            labels: Some(deployment_labels),
            ..api_meta::ObjectMeta::default()
        }),
        spec: Some(api_apps::DeploymentSpec {
            replicas: Some(1),
            selector: api_meta::LabelSelector {
                match_labels: Some(selector_labels),
                ..api_meta::LabelSelector::default()
            },
            template: api_core::PodTemplateSpec {
                metadata: Some(api_meta::ObjectMeta {
                    labels: Some(pod_labels),
                    annotations: Some(annotations),
                    ..api_meta::ObjectMeta::default()
                }),
                spec: Some(spec_to_podspec(
                    settings,
                    spec,
                    module_label_value,
                    module_image,
                )?),
            },
            ..api_apps::DeploymentSpec::default()
        }),
        ..api_apps::Deployment::default()
    };
    Ok((deployment_name, deployment))
}

/// Converts Docker Module Spec into Service Account.
pub fn spec_to_service_account(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
) -> Result<(String, api_core::ServiceAccount)> {
    let module_label_value = sanitize_dns_value(spec.name())?;
    let device_label_value =
        sanitize_dns_value(settings.device_id().ok_or(ErrorKind::MissingDeviceId)?)?;
    let hubname_label = sanitize_dns_domain(
        settings
            .iot_hub_hostname()
            .ok_or(ErrorKind::MissingHubName)?,
    )?;

    let service_account_name = module_label_value.clone();

    // labels
    let mut labels = BTreeMap::new();
    labels.insert(EDGE_MODULE_LABEL.to_string(), module_label_value.clone());
    labels.insert(EDGE_DEVICE_LABEL.to_string(), device_label_value);
    labels.insert(EDGE_HUBNAME_LABEL.to_string(), hubname_label);

    // annotations
    let mut annotations = BTreeMap::new();
    annotations.insert(EDGE_ORIGINAL_MODULEID.to_string(), spec.name().to_string());

    let service_account = api_core::ServiceAccount {
        metadata: Some(api_meta::ObjectMeta {
            name: Some(service_account_name.clone()),
            namespace: Some(settings.namespace().to_string()),
            labels: Some(labels),
            annotations: Some(annotations),
            ..api_meta::ObjectMeta::default()
        }),
        ..api_core::ServiceAccount::default()
    };

    Ok((service_account_name, service_account))
}

/// Converts Docker Module Spec into Role Binding.
pub fn spec_to_role_binding(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
) -> Result<(String, api_rbac::RoleBinding)> {
    let module_label_value = sanitize_dns_value(spec.name())?;
    let device_label_value =
        sanitize_dns_value(settings.device_id().ok_or(ErrorKind::MissingDeviceId)?)?;
    let hubname_label = sanitize_dns_domain(
        settings
            .iot_hub_hostname()
            .ok_or(ErrorKind::MissingHubName)?,
    )?;

    let role_binding_name = module_label_value.clone();

    // labels
    let mut labels = BTreeMap::new();
    labels.insert(EDGE_MODULE_LABEL.to_string(), module_label_value.clone());
    labels.insert(EDGE_DEVICE_LABEL.to_string(), device_label_value);
    labels.insert(EDGE_HUBNAME_LABEL.to_string(), hubname_label);

    // annotations
    let mut annotations = BTreeMap::new();
    annotations.insert(EDGE_ORIGINAL_MODULEID.to_string(), spec.name().to_string());

    let role_binding = api_rbac::RoleBinding {
        metadata: Some(api_meta::ObjectMeta {
            name: Some(role_binding_name.clone()),
            namespace: Some(settings.namespace().to_string()),
            labels: Some(labels),
            annotations: Some(annotations),
            ..api_meta::ObjectMeta::default()
        }),
        role_ref: api_rbac::RoleRef {
            api_group: "rbac.authorization.k8s.io".into(),
            kind: "Role".into(),
            name: module_label_value.clone(),
        },
        subjects: vec![api_rbac::Subject {
            api_group: None,
            kind: "ServiceAccount".into(),
            name: module_label_value,
            namespace: Some(settings.namespace().into()),
        }],
    };

    Ok((role_binding_name, role_binding))
}

/// Creates Config Map with Edge Trust Bundle.
pub fn trust_bundle_to_config_map(
    settings: &Settings,
    cert: &impl Certificate,
) -> Result<(String, api_core::ConfigMap)> {
    let device_label_value =
        sanitize_dns_value(settings.device_id().ok_or(ErrorKind::MissingDeviceId)?)?;
    let hubname_label = sanitize_dns_domain(
        settings
            .iot_hub_hostname()
            .ok_or(ErrorKind::MissingHubName)?,
    )?;

    // labels
    let mut labels = BTreeMap::new();
    labels.insert(EDGE_DEVICE_LABEL.to_string(), device_label_value);
    labels.insert(EDGE_HUBNAME_LABEL.to_string(), hubname_label);

    let cert = cert.pem().context(ErrorKind::IdentityCertificate)?;
    let cert = str::from_utf8(cert.as_ref()).context(ErrorKind::IdentityCertificate)?;

    let mut data = BTreeMap::new();
    data.insert(PROXY_TRUST_BUNDLE_FILENAME.to_string(), cert.to_string());
    let config_map_name = settings.proxy_trust_bundle_config_map_name().to_string();

    let config_map = api_core::ConfigMap {
        metadata: Some(api_meta::ObjectMeta {
            name: Some(config_map_name.clone()),
            namespace: Some(settings.namespace().to_string()),
            labels: Some(labels),
            ..api_meta::ObjectMeta::default()
        }),
        data: Some(data),
        ..api_core::ConfigMap::default()
    };
    Ok((config_map_name, config_map))
}

#[cfg(test)]
mod tests {
    use std::collections::{BTreeMap, HashMap};
    use std::str;

    use k8s_openapi::api::core::v1 as api_core;
    use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;

    use docker::models::AuthConfig;
    use docker::models::ContainerCreateBody;
    use docker::models::HostConfig;
    use docker::models::Mount;
    use edgelet_core::{ImagePullPolicy, ModuleSpec};
    use edgelet_docker::DockerConfig;
    use edgelet_test_utils::cert::TestCert;

    use crate::constants::env::*;
    use crate::constants::*;
    use crate::convert::to_k8s::{Auth, AuthEntry};
    use crate::convert::{
        auth_to_image_pull_secret, spec_to_deployment, spec_to_role_binding,
        spec_to_service_account, trust_bundle_to_config_map,
    };
    use crate::tests::{make_settings, PROXY_CONFIG_MAP_NAME, PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME};
    use crate::ErrorKind;

    fn create_module_spec() -> ModuleSpec<DockerConfig> {
        let create_body = ContainerCreateBody::new()
            .with_host_config(
                HostConfig::new()
                    .with_binds(vec![String::from("/a:/b:ro"), String::from("/c:/d")])
                    .with_privileged(true)
                    .with_mounts(vec![
                        Mount::new()
                            .with__type(String::from("bind"))
                            .with_read_only(true)
                            .with_source(String::from("/e"))
                            .with_target(String::from("/f")),
                        Mount::new()
                            .with__type(String::from("bind"))
                            .with_source(String::from("/g"))
                            .with_target(String::from("/h")),
                        Mount::new()
                            .with__type(String::from("volume"))
                            .with_read_only(true)
                            .with_source(String::from("i"))
                            .with_target(String::from("/j")),
                        Mount::new()
                            .with__type(String::from("volume"))
                            .with_source(String::from("k"))
                            .with_target(String::from("/l")),
                    ]),
            )
            .with_labels({
                let mut labels = HashMap::<String, String>::new();
                labels.insert(String::from("label1"), String::from("value1"));
                labels.insert(String::from("label2"), String::from("value2"));
                labels
            });
        let auth_config = AuthConfig::new()
            .with_password(String::from("a password"))
            .with_username(String::from("USERNAME"))
            .with_serveraddress(String::from("REGISTRY"));
        ModuleSpec::new(
            "$edgeAgent".to_string(),
            "docker".to_string(),
            DockerConfig::new("my-image:v1.0".to_string(), create_body, Some(auth_config)).unwrap(),
            {
                let mut env = HashMap::new();
                env.insert(String::from("a"), String::from("b"));
                env.insert(String::from("C"), String::from("D"));
                env
            },
            ImagePullPolicy::default(),
        )
        .unwrap()
    }

    fn validate_deployment_metadata(
        module: &str,
        device: &str,
        iothub: &str,
        meta: Option<&api_meta::ObjectMeta>,
    ) {
        assert!(meta.is_some());
        if let Some(meta) = meta {
            assert_eq!(meta.name, Some(module.to_string()));
            assert!(meta.labels.is_some());
            if let Some(labels) = meta.labels.as_ref() {
                assert_eq!(labels.get(EDGE_MODULE_LABEL).unwrap(), "edgeagent");
                assert_eq!(labels.get(EDGE_DEVICE_LABEL).unwrap(), device);
                assert_eq!(labels.get(EDGE_HUBNAME_LABEL).unwrap(), iothub);
            }
        }
    }

    #[allow(clippy::cognitive_complexity)]
    #[test]
    fn deployment_success() {
        let module_config = create_module_spec();

        let (name, deployment) = spec_to_deployment(&make_settings(None), &module_config).unwrap();
        assert_eq!(name, "edgeagent");
        validate_deployment_metadata(
            "edgeagent",
            "device1",
            "iothub",
            deployment.metadata.as_ref(),
        );

        assert!(deployment.spec.is_some());
        if let Some(spec) = deployment.spec.as_ref() {
            assert_eq!(spec.replicas, Some(1));
            assert!(spec.selector.match_labels.is_some());
            if let Some(match_labels) = spec.selector.match_labels.as_ref() {
                assert_eq!(match_labels.get(EDGE_MODULE_LABEL).unwrap(), "edgeagent");
                assert_eq!(match_labels.get(EDGE_DEVICE_LABEL).unwrap(), "device1");
                assert_eq!(match_labels.get(EDGE_HUBNAME_LABEL).unwrap(), "iothub");
            }
            assert!(spec.template.spec.is_some());
            if let Some(podspec) = spec.template.spec.as_ref() {
                assert_eq!(podspec.containers.len(), 2);
                if let Some(module) = podspec.containers.iter().find(|c| c.name == "edgeagent") {
                    validate_container_env(module.env.as_ref().unwrap());
                    assert_eq!(module.volume_mounts.as_ref().map(Vec::len).unwrap(), 6);
                    assert_eq!(module.image.as_ref().unwrap(), "my-image:v1.0");
                    assert_eq!(module.image_pull_policy.as_ref().unwrap(), "IfNotPresent");
                }
                if let Some(proxy) = podspec
                    .containers
                    .iter()
                    .find(|c| c.name == PROXY_CONTAINER_NAME)
                {
                    validate_container_env(proxy.env.as_ref().unwrap());
                    assert_eq!(proxy.volume_mounts.as_ref().map(Vec::len).unwrap(), 2);
                    assert_eq!(proxy.image.as_ref().unwrap(), "proxy:latest");
                    assert_eq!(proxy.image_pull_policy.as_ref().unwrap(), "IfNotPresent");
                }
                assert_eq!(podspec.service_account_name.as_ref().unwrap(), "edgeagent");
                assert!(podspec.image_pull_secrets.is_some());
                // 4 bind mounts, 2 volume mounts, 1 proxy configmap, 1 trust bundle configmap
                assert_eq!(podspec.volumes.as_ref().map(Vec::len).unwrap(), 8);
            }
        }
    }

    fn validate_container_env(env: &[api_core::EnvVar]) {
        assert_eq!(env.len(), 13);
        assert!(env.contains(&super::env("a", "b")));
        assert!(env.contains(&super::env("C", "D")));
        assert!(env.contains(&super::env(USE_PERSISTENT_VOLUME_KEY, "True")));
        assert!(env.contains(&super::env(NAMESPACE_KEY, "default")));
        assert!(env.contains(&super::env(EDGE_AGENT_MODE_KEY, EDGE_AGENT_MODE)));
        assert!(env.contains(&super::env(PROXY_IMAGE_KEY, "proxy:latest")));
        assert!(env.contains(&super::env(
            PROXY_CONFIG_VOLUME_KEY,
            PROXY_CONFIG_VOLUME_NAME
        )));
        assert!(env.contains(&super::env(PROXY_CONFIG_PATH_KEY, "/etc/traefik")));
        assert!(env.contains(&super::env(
            PROXY_CONFIG_MAP_NAME_KEY,
            PROXY_CONFIG_MAP_NAME
        )));
        assert!(env.contains(&super::env(
            PROXY_TRUST_BUNDLE_VOLUME_KEY,
            PROXY_TRUST_BUNDLE_VOLUME_NAME,
        )));
        assert!(env.contains(&super::env(
            &PROXY_TRUST_BUNDLE_PATH_KEY,
            "/etc/trust-bundle"
        )));
        assert!(env.contains(&super::env(
            &PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY,
            PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME
        )));
    }

    #[test]
    fn auth_to_image_pull_secret_success() {
        let mut auths = BTreeMap::new();
        auths.insert(
            "REGISTRY".to_string(),
            AuthEntry::new("USER".to_string(), "a password".to_string()),
        );
        let json_data = serde_json::to_string(&Auth::new(auths)).unwrap();
        let auth_config = AuthConfig::new()
            .with_password(String::from("a password"))
            .with_username(String::from("USER"))
            .with_serveraddress(String::from("REGISTRY"));
        let (name, secret) = auth_to_image_pull_secret("namespace", &auth_config).unwrap();
        assert_eq!(name, "user-registry");

        assert_eq!(secret.type_, Some(PULL_SECRET_DATA_TYPE.to_string()));
        assert!(secret.metadata.is_some());
        if let Some(meta) = secret.metadata.as_ref() {
            assert_eq!(meta.name, Some(name));
            assert_eq!(meta.namespace, Some("namespace".to_string()));
        }
        assert_eq!(
            str::from_utf8(secret.data.unwrap()[".dockerconfigjson"].0.as_slice()).unwrap(),
            json_data
        );
    }

    #[test]
    fn auth_to_image_pull_secret_failure() {
        let auths = vec![
            AuthConfig::new()
                .with_username(String::from("USER"))
                .with_serveraddress(String::from("REGISTRY")),
            AuthConfig::new()
                .with_password(String::from("a password"))
                .with_serveraddress(String::from("REGISTRY")),
            AuthConfig::new()
                .with_password(String::from("a password"))
                .with_username(String::from("USER")),
        ];
        for auth in auths {
            let result = auth_to_image_pull_secret("namespace", &auth);
            assert!(result.is_err());
        }
    }

    #[test]
    fn module_to_service_account() {
        let module = create_module_spec();

        let (name, service_account) =
            spec_to_service_account(&make_settings(None), &module).unwrap();
        assert_eq!(name, "edgeagent");

        assert!(service_account.metadata.is_some());
        if let Some(metadata) = service_account.metadata {
            assert_eq!(metadata.name, Some("edgeagent".to_string()));
            assert_eq!(metadata.namespace, Some("default".to_string()));

            assert!(metadata.annotations.is_some());
            if let Some(annotations) = metadata.annotations {
                assert_eq!(annotations.len(), 1);
                assert_eq!(annotations[EDGE_ORIGINAL_MODULEID], "$edgeAgent");
            }

            assert!(metadata.labels.is_some());
            if let Some(labels) = metadata.labels {
                assert_eq!(labels.len(), 3);
                assert_eq!(labels[EDGE_DEVICE_LABEL], "device1");
                assert_eq!(labels[EDGE_HUBNAME_LABEL], "iothub");
                assert_eq!(labels[EDGE_MODULE_LABEL], "edgeagent");
            }
        }
    }

    #[test]
    fn module_to_role_binding() {
        let module = create_module_spec();

        let (name, role_binding) = spec_to_role_binding(&make_settings(None), &module).unwrap();
        assert_eq!(name, "edgeagent");
        assert!(role_binding.metadata.is_some());
        if let Some(metadata) = role_binding.metadata {
            assert_eq!(metadata.name, Some("edgeagent".to_string()));
            assert_eq!(metadata.namespace, Some("default".to_string()));

            assert!(metadata.annotations.is_some());
            if let Some(annotations) = metadata.annotations {
                assert_eq!(annotations.len(), 1);
                assert_eq!(annotations[EDGE_ORIGINAL_MODULEID], "$edgeAgent");
            }

            assert!(metadata.labels.is_some());
            if let Some(labels) = metadata.labels {
                assert_eq!(labels.len(), 3);
                assert_eq!(labels[EDGE_DEVICE_LABEL], "device1");
                assert_eq!(labels[EDGE_HUBNAME_LABEL], "iothub");
                assert_eq!(labels[EDGE_MODULE_LABEL], "edgeagent");
            }
        }

        assert_eq!(role_binding.role_ref.api_group, "rbac.authorization.k8s.io");
        assert_eq!(role_binding.role_ref.kind, "Role");
        assert_eq!(role_binding.role_ref.name, "edgeagent");

        assert_eq!(role_binding.subjects.len(), 1);
        let subject = &role_binding.subjects[0];
        assert_eq!(subject.api_group, None);
        assert_eq!(subject.kind, "ServiceAccount");
        assert_eq!(subject.name, "edgeagent");
        assert_eq!(subject.namespace, Some("default".to_string()));
    }

    #[test]
    fn trust_bundle_to_config_map_fails_when_cert_is_not_available() {
        let config_map = trust_bundle_to_config_map(
            &make_settings(None),
            &TestCert::default().with_fail_pem(true),
        );

        assert_eq!(
            config_map.unwrap_err().kind(),
            &ErrorKind::IdentityCertificate
        )
    }

    #[test]
    fn trust_bundle_to_config_map_fails_when_cert_is_invalid() {
        let config_map = trust_bundle_to_config_map(
            &make_settings(None),
            &TestCert::default().with_cert(vec![0, 159, 146, 150]),
        );

        assert_eq!(
            config_map.unwrap_err().kind(),
            &ErrorKind::IdentityCertificate
        )
    }

    #[test]
    fn trust_bundle_to_config_map_with_cert() {
        let (name, config_map) = trust_bundle_to_config_map(
            &make_settings(None),
            &TestCert::default().with_cert(b"secret_cert".to_vec()),
        )
        .unwrap();

        assert_eq!(name, "device1-iotedged-proxy-trust-bundle");

        assert!(config_map.metadata.is_some());
        if let Some(metadata) = config_map.metadata {
            assert_eq!(
                metadata.name,
                Some("device1-iotedged-proxy-trust-bundle".to_string())
            );
            assert_eq!(metadata.namespace, Some("default".to_string()));

            assert!(metadata.labels.is_some());
            if let Some(labels) = metadata.labels {
                assert_eq!(labels.len(), 2);
                assert_eq!(labels[EDGE_DEVICE_LABEL], "device1");
                assert_eq!(labels[EDGE_HUBNAME_LABEL], "iothub");
            }
        }

        assert!(config_map.data.is_some());
        if let Some(data) = config_map.data {
            assert_eq!(data.len(), 1);
            assert_eq!(data[PROXY_TRUST_BUNDLE_FILENAME], "secret_cert");
        }
    }
}
