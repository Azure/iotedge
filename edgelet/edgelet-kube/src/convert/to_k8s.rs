// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use base64;
use docker::models::{AuthConfig, HostConfig};
use edgelet_core::ModuleSpec;
use edgelet_docker::DockerConfig;
use k8s_openapi::api::apps::v1 as apps;
use k8s_openapi::api::core::v1 as api_core;
use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::ByteString;
use log::warn;
use serde_derive::{Deserialize, Serialize};
use serde_json;

use crate::constants::*;
use crate::convert::sanitize_dns_value;
use crate::error::{ErrorKind, Result};
use crate::runtime::KubeRuntimeData;

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
#[derive(Debug, Serialize, Deserialize, Clone)]
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
#[derive(Debug, Serialize, Deserialize, Clone)]
struct Auth {
    pub auths: BTreeMap<String, AuthEntry>,
}
impl Auth {
    pub fn new(auths: BTreeMap<String, AuthEntry>) -> Auth {
        Auth { auths }
    }

    pub fn secret_data(&self) -> Result<ByteString> {
        Ok(ByteString(serde_json::to_string(self)?.bytes().collect()))
    }
}

/// Convert Docker `AuthConfig` to a K8s image pull secret.
pub fn auth_to_image_pull_secret(
    namespace: &str,
    auth: &AuthConfig,
) -> Result<(String, api_core::Secret)> {
    let secret_name = auth_to_pull_secret_name(auth).ok_or_else(|| ErrorKind::AuthName)?;
    let registry = auth
        .serveraddress()
        .ok_or_else(|| ErrorKind::AuthServerAddress)?;
    let user = auth.username().ok_or_else(|| ErrorKind::AuthUser)?;
    let password = auth.password().ok_or_else(|| ErrorKind::AuthPassword)?;
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
            ..api_core::Secret::default()
        },
    ))
}

/// Convert Docker `ModuleSpec` to K8s `PodSpec`
fn spec_to_podspec<R: KubeRuntimeData>(
    runtime: &R,
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
    if runtime.use_pvc() && EDGE_EDGE_AGENT_NAME == module_label_value {
        let env_var = api_core::EnvVar {
            name: USE_PERSISTENT_VOLUME_CLAIMS.to_string(),
            value: Some("True".to_string()),
            ..api_core::EnvVar::default()
        };
        env_vars.push(env_var);
    }

    // Bind/volume mounts
    // ConfigMap volume name is fixed: "config-volume"
    let proxy_config_volume_source = api_core::ConfigMapVolumeSource {
        name: Some(runtime.proxy_config_map_name().to_string()),
        ..api_core::ConfigMapVolumeSource::default()
    };
    // Volume entry for proxy's config map.
    let proxy_config_volume = api_core::Volume {
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        config_map: Some(proxy_config_volume_source),
        ..api_core::Volume::default()
    };
    let mut volumes = vec![proxy_config_volume];

    // Where to mount proxy config map.
    let proxy_volume_mount = api_core::VolumeMount {
        mount_path: runtime.proxy_config_path().to_string(),
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        read_only: Some(true),
        ..api_core::VolumeMount::default()
    };
    let mut volume_mounts = vec![proxy_volume_mount];
    let proxy_volume_mounts = volume_mounts.clone();

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

                        let volume = if runtime.use_pvc() {
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
    // service account
    let service_account_name = if EDGE_EDGE_AGENT_NAME == module_label_value {
        Some(runtime.service_account_name().to_string())
    } else {
        None
    };

    Ok(api_core::PodSpec {
        containers: vec![
            // module
            api_core::Container {
                name: module_label_value,
                env: Some(env_vars.clone()),
                image: Some(module_image),
                image_pull_policy: Some(runtime.image_pull_policy().to_string()),
                security_context: security,
                volume_mounts: Some(volume_mounts),
                ..api_core::Container::default()
            },
            // proxy
            api_core::Container {
                name: PROXY_CONTAINER_NAME.to_string(),
                env: Some(env_vars),
                image: Some(runtime.proxy_image().to_string()),
                image_pull_policy: Some(runtime.image_pull_policy().to_string()),
                volume_mounts: Some(proxy_volume_mounts),
                ..api_core::Container::default()
            },
        ],
        image_pull_secrets,
        service_account_name,
        volumes: Some(volumes),
        ..api_core::PodSpec::default()
    })
}

/// Convert Docker Module Spec into a K8S Deployment.
pub fn spec_to_deployment<R: KubeRuntimeData>(
    runtime: &R,
    spec: &ModuleSpec<DockerConfig>,
) -> Result<(String, apps::Deployment)> {
    // Set some values...
    let module_label_value = sanitize_dns_value(spec.name())?;
    let device_label_value = sanitize_dns_value(runtime.device_id())?;
    let hubname_label = sanitize_dns_value(runtime.iot_hub_hostname())?;
    let deployment_name = format!(
        "{}-{}-{}",
        &module_label_value, &device_label_value, &hubname_label
    );
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
    let deployment = apps::Deployment {
        metadata: Some(api_meta::ObjectMeta {
            name: Some(deployment_name.clone()),
            namespace: Some(runtime.namespace().to_string()),
            labels: Some(deployment_labels),
            ..api_meta::ObjectMeta::default()
        }),
        spec: Some(apps::DeploymentSpec {
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
                    runtime,
                    spec,
                    module_label_value,
                    module_image,
                )?),
            },
            ..apps::DeploymentSpec::default()
        }),
        ..apps::Deployment::default()
    };
    Ok((deployment_name, deployment))
}

#[cfg(test)]
mod tests {
    use super::spec_to_deployment;
    use crate::constants;
    use crate::convert::to_k8s::auth_to_image_pull_secret;
    use crate::convert::to_k8s::{Auth, AuthEntry};
    use crate::runtime::KubeRuntimeData;
    use docker::models::AuthConfig;
    use docker::models::ContainerCreateBody;
    use docker::models::HostConfig;
    use docker::models::Mount;
    use edgelet_core::{ImagePullPolicy, ModuleSpec};
    use edgelet_docker::DockerConfig;
    use k8s_openapi::apimachinery::pkg::apis::meta::v1 as api_meta;
    use serde_json;
    use std::collections::BTreeMap;
    use std::collections::HashMap;
    use std::str;
    use url::Url;

    struct KubeRuntimeTest {
        use_pvc: bool,
        namespace: String,
        iot_hub_hostname: String,
        device_id: String,
        edge_hostname: String,
        proxy_image: String,
        proxy_config_path: String,
        proxy_config_map_name: String,
        image_pull_policy: String,
        service_account_name: String,
        workload_uri: Url,
        management_uri: Url,
    }

    impl KubeRuntimeTest {
        pub fn new(
            use_pvc: bool,
            namespace: String,
            iot_hub_hostname: String,
            device_id: String,
            edge_hostname: String,
            proxy_image: String,
            proxy_config_path: String,
            proxy_config_map_name: String,
            image_pull_policy: String,
            service_account_name: String,
            workload_uri: Url,
            management_uri: Url,
        ) -> KubeRuntimeTest {
            KubeRuntimeTest {
                use_pvc,
                namespace,
                iot_hub_hostname,
                device_id,
                edge_hostname,
                proxy_image,
                proxy_config_path,
                proxy_config_map_name,
                image_pull_policy,
                service_account_name,
                workload_uri,
                management_uri,
            }
        }
    }

    impl KubeRuntimeData for KubeRuntimeTest {
        fn namespace(&self) -> &str {
            &self.namespace
        }
        fn use_pvc(&self) -> bool {
            self.use_pvc
        }
        fn iot_hub_hostname(&self) -> &str {
            &self.iot_hub_hostname
        }
        fn device_id(&self) -> &str {
            &self.device_id
        }
        fn edge_hostname(&self) -> &str {
            &self.edge_hostname
        }
        fn proxy_image(&self) -> &str {
            &self.proxy_image
        }
        fn proxy_config_path(&self) -> &str {
            &self.proxy_config_path
        }
        fn proxy_config_map_name(&self) -> &str {
            &self.proxy_config_map_name
        }
        fn image_pull_policy(&self) -> &str {
            &self.image_pull_policy
        }
        fn service_account_name(&self) -> &str {
            &self.service_account_name
        }
        fn workload_uri(&self) -> &Url {
            &self.workload_uri
        }
        fn management_uri(&self) -> &Url {
            &self.management_uri
        }
    }

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
        let name = format!("{}-{}-{}", module, device, iothub);
        assert!(meta.is_some());
        if let Some(meta) = meta {
            assert_eq!(meta.name, Some(name));
            assert!(meta.labels.is_some());
            if let Some(labels) = meta.labels.as_ref() {
                assert_eq!(
                    labels.get(constants::EDGE_MODULE_LABEL).unwrap(),
                    "edgeagent"
                );
                assert_eq!(labels.get(constants::EDGE_DEVICE_LABEL).unwrap(), "device1");
                assert_eq!(labels.get(constants::EDGE_HUBNAME_LABEL).unwrap(), "iothub");
            }
        }
    }

    #[allow(clippy::cognitive_complexity)]
    #[test]
    fn deployment_success() {
        let runtime = KubeRuntimeTest::new(
            true,
            String::from("default1"),
            String::from("iotHub"),
            String::from("device1"),
            String::from("$edgeAgent"),
            String::from("proxy:latest"),
            String::from("/etc/traefik"),
            String::from("device1-iotedged-proxy-config"),
            String::from("On-Create"),
            String::from("iotedge"),
            Url::parse("http://localhost:35000").unwrap(),
            Url::parse("http://localhost:35001").unwrap(),
        );
        let module_config = create_module_spec();

        let (name, deployment) = spec_to_deployment(&runtime, &module_config).unwrap();
        assert_eq!(name, "edgeagent-device1-iothub");
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
                assert_eq!(
                    match_labels.get(constants::EDGE_MODULE_LABEL).unwrap(),
                    "edgeagent"
                );
                assert_eq!(
                    match_labels.get(constants::EDGE_DEVICE_LABEL).unwrap(),
                    "device1"
                );
                assert_eq!(
                    match_labels.get(constants::EDGE_HUBNAME_LABEL).unwrap(),
                    "iothub"
                );
            }
            assert!(spec.template.spec.is_some());
            if let Some(podspec) = spec.template.spec.as_ref() {
                assert_eq!(podspec.containers.len(), 2);
                if let Some(module) = podspec.containers.iter().find(|c| c.name == "edgeagent") {
                    // 2 from module spec, 1 for use_pvc
                    assert_eq!(module.env.as_ref().map(Vec::len).unwrap(), 3);
                    assert_eq!(module.volume_mounts.as_ref().map(Vec::len).unwrap(), 7);
                    assert_eq!(module.image.as_ref().unwrap(), "my-image:v1.0");
                    assert_eq!(module.image_pull_policy.as_ref().unwrap(), "On-Create");
                }
                if let Some(proxy) = podspec
                    .containers
                    .iter()
                    .find(|c| c.name == constants::PROXY_CONTAINER_NAME)
                {
                    // 2 from module spec, 1 for use_pvc
                    assert_eq!(proxy.env.as_ref().map(Vec::len).unwrap(), 3);
                    assert_eq!(proxy.volume_mounts.as_ref().map(Vec::len).unwrap(), 1);
                    assert_eq!(proxy.image.as_ref().unwrap(), "proxy:latest");
                    assert_eq!(proxy.image_pull_policy.as_ref().unwrap(), "On-Create");
                }
                assert_eq!(podspec.service_account_name.as_ref().unwrap(), "iotedge");
                assert!(podspec.image_pull_secrets.is_some());
                // 4 bind mounts, 2 volume mounts, 1 proxy configmap
                assert_eq!(podspec.volumes.as_ref().map(Vec::len).unwrap(), 7);
            }
        }
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
}
