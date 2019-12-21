// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::str;

use docker::models::HostConfig;
use edgelet_core::{Certificate, ModuleSpec};
use edgelet_docker::DockerConfig;
use failure::ResultExt;
use k8s_openapi::api::apps::v1 as api_apps;
use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::api::rbac::v1 as api_rbac;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
use log::warn;

use crate::constants::env::*;
use crate::constants::*;
use crate::convert::{sanitize_dns_domain, sanitize_dns_value};
use crate::error::{ErrorKind, Result};
use crate::registry::ImagePullSecret;
use crate::settings::Settings;
use crate::KubeModuleOwner;

/// Converts Docker `ModuleSpec` to K8s `PodSpec`
fn spec_to_podspec(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
    module_label_value: String,
    module_image: String,
    module_owner: &KubeModuleOwner,
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

    let proxy_pull_secret = settings
        .proxy()
        .auth()
        .and_then(|auth| ImagePullSecret::from_auth(auth).and_then(|secret| secret.name()));

    if EDGE_EDGE_AGENT_NAME == module_label_value {
        env_vars.push(env(EDGE_NETWORK_ID_KEY, ""));
        env_vars.push(env(NAMESPACE_KEY, settings.namespace()));
        env_vars.push(env(PROXY_IMAGE_KEY, settings.proxy().image()));
        env_vars.push(env(PROXY_CONFIG_VOLUME_KEY, PROXY_CONFIG_VOLUME_NAME));
        env_vars.push(env(
            PROXY_CONFIG_MAP_NAME_KEY,
            settings.proxy().config_map_name(),
        ));
        env_vars.push(env(PROXY_CONFIG_PATH_KEY, settings.proxy().config_path()));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY,
            settings.proxy().trust_bundle_config_map_name(),
        ));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_VOLUME_KEY,
            PROXY_TRUST_BUNDLE_VOLUME_NAME,
        ));
        env_vars.push(env(
            PROXY_TRUST_BUNDLE_PATH_KEY,
            settings.proxy().trust_bundle_path(),
        ));

        env_vars.push(env(
            EDGE_OBJECT_OWNER_API_VERSION_KEY,
            module_owner.api_version(),
        ));
        env_vars.push(env(EDGE_OBJECT_OWNER_KIND_KEY, module_owner.kind()));
        env_vars.push(env(EDGE_OBJECT_OWNER_NAME_KEY, module_owner.name()));
        env_vars.push(env(EDGE_OBJECT_OWNER_UID_KEY, module_owner.uid()));

        if let Some(proxy_pull_secret) = &proxy_pull_secret {
            env_vars.push(env(PROXY_IMAGE_PULL_SECRET_NAME_KEY, proxy_pull_secret))
        }
    }

    // Bind/volume mounts
    // ConfigMap volume name is fixed: "config-volume"
    let proxy_config_volume_source = api_core::ConfigMapVolumeSource {
        name: Some(settings.proxy().config_map_name().to_string()),
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
        name: Some(settings.proxy().trust_bundle_config_map_name().to_string()),
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
        mount_path: settings.proxy().config_path().to_string(),
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        read_only: Some(true),
        ..api_core::VolumeMount::default()
    };

    // Where to mount proxy trust bundle config map
    let trust_bundle_volume_mount = api_core::VolumeMount {
        mount_path: settings.proxy().trust_bundle_path().to_string(),
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
                    if let (Some(source), Some(target)) = (mount.source(), mount.target()) {
                        let volume_name = sanitize_dns_value(source)?;

                        let volume = api_core::Volume {
                            name: volume_name.clone(),
                            empty_dir: Some(api_core::EmptyDirVolumeSource::default()),
                            ..api_core::Volume::default()
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

    let module_pull_secret = spec
        .config()
        .auth()
        .and_then(|auth| ImagePullSecret::from_auth(&auth).and_then(|secret| secret.name()));

    //pull secrets
    let image_pull_secrets = vec![proxy_pull_secret, module_pull_secret]
        .into_iter()
        .filter_map(|name| name.map(|name| api_core::LocalObjectReference { name: Some(name) }))
        .collect::<Vec<_>>();

    Ok(api_core::PodSpec {
        containers: vec![
            // module
            api_core::Container {
                name: module_label_value.clone(),
                env: Some(env_vars.clone()),
                image: Some(module_image),
                image_pull_policy: Some(settings.proxy().image_pull_policy().to_string()), //todo user edgeagent imagepullpolicy instead
                security_context: security,
                volume_mounts: Some(volume_mounts),
                ..api_core::Container::default()
            },
            // proxy
            api_core::Container {
                name: PROXY_CONTAINER_NAME.to_string(),
                env: Some(env_vars),
                image: Some(settings.proxy().image().to_string()),
                image_pull_policy: Some(settings.proxy().image_pull_policy().to_string()),
                volume_mounts: Some(proxy_volume_mounts),
                ..api_core::Container::default()
            },
        ],
        image_pull_secrets: if image_pull_secrets.is_empty() {
            None
        } else {
            Some(image_pull_secrets)
        },
        service_account_name: Some(module_label_value),
        security_context: Some(api_core::PodSecurityContext {
            run_as_non_root: Some(true),
            run_as_user: Some(1000),
            ..api_core::PodSecurityContext::default()
        }),
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

fn owner_references(module_owner: &KubeModuleOwner) -> Vec<api_meta::OwnerReference> {
    vec![api_meta::OwnerReference {
        api_version: module_owner.api_version().to_string(),
        name: module_owner.name().to_string(),
        kind: module_owner.kind().to_string(),
        uid: module_owner.uid().to_string(),
        ..api_meta::OwnerReference::default()
    }]
}

/// Converts Docker Module Spec into a K8S Deployment.
pub fn spec_to_deployment(
    settings: &Settings,
    spec: &ModuleSpec<DockerConfig>,
    module_owner: &KubeModuleOwner,
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
            owner_references: Some(owner_references(module_owner)),
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
                    module_owner,
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
    module_owner: &KubeModuleOwner,
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
    labels.insert(EDGE_MODULE_LABEL.to_string(), module_label_value);
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
            owner_references: Some(owner_references(module_owner)),
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
    module_owner: &KubeModuleOwner,
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
            owner_references: Some(owner_references(module_owner)),
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
    let config_map_name = settings.proxy().trust_bundle_config_map_name().to_string();

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
    use std::collections::HashMap;
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
    use crate::convert::{
        spec_to_deployment, spec_to_role_binding, spec_to_service_account,
        trust_bundle_to_config_map,
    };
    use crate::tests::{
        create_module_owner, make_settings, PROXY_CONFIG_MAP_NAME,
        PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
    };
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
        let module_owner = create_module_owner();

        let (name, deployment) =
            spec_to_deployment(&make_settings(None), &module_config, &module_owner).unwrap();
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
                assert!(podspec.security_context.is_some());
                if let Some(security_context) = &podspec.security_context {
                    assert_eq!(security_context.run_as_non_root, Some(true));
                    assert_eq!(security_context.run_as_user, Some(1000));
                }
                assert!(podspec.image_pull_secrets.is_some());
                if let Some(image_pull_secrets) = &podspec.image_pull_secrets {
                    assert_eq!(image_pull_secrets.len(), 1);
                    assert_eq!(
                        image_pull_secrets[0].name,
                        Some("username-registry".to_string())
                    );
                }
                // 4 bind mounts, 2 volume mounts, 1 proxy configmap, 1 trust bundle configmap
                assert_eq!(podspec.volumes.as_ref().map(Vec::len).unwrap(), 8);
            }
        }
    }

    fn validate_container_env(env: &[api_core::EnvVar]) {
        assert_eq!(env.len(), 15);
        assert!(env.contains(&super::env("a", "b")));
        assert!(env.contains(&super::env("C", "D")));
        assert!(env.contains(&super::env(NAMESPACE_KEY, "default")));
        assert!(env.contains(&super::env(PROXY_IMAGE_KEY, "proxy:latest")));
        assert!(env.contains(&super::env(
            PROXY_CONFIG_VOLUME_KEY,
            PROXY_CONFIG_VOLUME_NAME,
        )));
        assert!(env.contains(&super::env(PROXY_CONFIG_PATH_KEY, "/etc/traefik")));
        assert!(env.contains(&super::env(
            PROXY_CONFIG_MAP_NAME_KEY,
            PROXY_CONFIG_MAP_NAME,
        )));
        assert!(env.contains(&super::env(
            PROXY_TRUST_BUNDLE_VOLUME_KEY,
            PROXY_TRUST_BUNDLE_VOLUME_NAME,
        )));
        assert!(env.contains(&super::env(
            &PROXY_TRUST_BUNDLE_PATH_KEY,
            "/etc/trust-bundle",
        )));
        assert!(env.contains(&super::env(
            &PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME_KEY,
            PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
        )));
        assert!(env.contains(&super::env(&EDGE_OBJECT_OWNER_API_VERSION_KEY, "v1",)));
        assert!(env.contains(&super::env(&EDGE_OBJECT_OWNER_KIND_KEY, "Deployment",)));
        assert!(env.contains(&super::env(&EDGE_OBJECT_OWNER_NAME_KEY, "iotedged",)));
        assert!(env.contains(&super::env(&EDGE_OBJECT_OWNER_UID_KEY, "123",)));
    }

    #[test]
    fn module_to_service_account() {
        let module = create_module_spec();
        let module_owner = create_module_owner();

        let (name, service_account) =
            spec_to_service_account(&make_settings(None), &module, &module_owner).unwrap();
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
        let module_owner = create_module_owner();

        let (name, role_binding) =
            spec_to_role_binding(&make_settings(None), &module, &module_owner).unwrap();
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
