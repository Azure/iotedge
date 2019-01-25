// Copyright (c) Microsoft. All rights reserved.

use crate::constants::*;
use crate::convert::sanitize_dns_value;
use crate::error::{ErrorKind, Result};
use crate::runtime::KubeRuntimeData;
use base64;
use docker::models::AuthConfig;
use edgelet_core::ModuleSpec;
use edgelet_docker::DockerConfig;
use k8s_openapi::v1_10::api::apps::v1 as apps;
use k8s_openapi::v1_10::api::core::v1 as api_core;
use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
use k8s_openapi::ByteString;
use log::warn;
use serde_derive::{Deserialize, Serialize};
use serde_json;
use std::collections::BTreeMap;

fn auth_to_pull_secret_name(auth: &AuthConfig) -> Option<String> {
    match (auth.username(), auth.serveraddress()) {
        (Some(user), Some(server)) => {
            Some(format!("{}-{}", user.to_lowercase(), server.to_lowercase()))
        }
        _ => None,
    }
}

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
    let mut auths = BTreeMap::<String, AuthEntry>::new();
    auths.insert(
        registry.to_string(),
        AuthEntry::new(user.to_string(), password.to_string()),
    );
    let auth_string = Auth::new(auths).secret_data()?;
    let mut secret_data = BTreeMap::<String, ByteString>::new();
    secret_data.insert(PULL_SECRET_DATA_NAME.to_string(), auth_string);
    Ok((
        secret_name.clone(),
        api_core::Secret {
            data: Some(secret_data),
            kind: Some(PULL_SECRET_TYPE.to_string()),
            metadata: Some(api_meta::ObjectMeta {
                name: Some(secret_name),
                namespace: Some(namespace.to_string()),
                ..api_meta::ObjectMeta::default()
            }),
            ..api_core::Secret::default()
        },
    ))
}

fn spec_to_podspec<R: KubeRuntimeData>(
    runtime: &R,
    spec: &ModuleSpec<DockerConfig>,
    module_label_value: String,
    module_image: String,
) -> Result<api_core::PodSpec> {
    // privileged container
    let security = if let Some(privileged) = spec
        .config()
        .create_options()
        .host_config()
        .and_then(|hc| hc.privileged())
    {
        if *privileged {
            let context = api_core::SecurityContext {
                privileged: Some(*privileged),
                ..api_core::SecurityContext::default()
            };
            Some(context)
        } else {
            None
        }
    } else {
        None
    };
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
            name: USE_PERSISTANT_VOLUME_CLAIMS.to_string(),
            value: Some("TRUE".to_string()),
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
    let proxy_config_volume = api_core::Volume {
        name: PROXY_CONFIG_VOLUME_NAME.to_string(),
        config_map: Some(proxy_config_volume_source),
        ..api_core::Volume::default()
    };
    let mut volumes = vec![proxy_config_volume];

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
        for bind in binds.iter() {
            let bind_elements = bind.split(':').collect::<Vec<&str>>();
            let element_count = bind_elements.len();
            if element_count >= 2 {
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
        for mount in mounts.iter() {
            match mount._type() {
                Some("bind") => {
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

    Ok(api_core::PodSpec {
        containers: vec![
            // module
            api_core::Container {
                name: module_label_value,
                env: Some(env_vars.clone()),
                image: Some(module_image),
                security_context: security,
                volume_mounts: Some(volume_mounts),
                ..api_core::Container::default()
            },
            // proxy
            api_core::Container {
                name: PROXY_CONTAINER_NAME.to_string(),
                env: Some(env_vars),
                image: Some(runtime.proxy_image().to_string()),
                volume_mounts: Some(proxy_volume_mounts),
                ..api_core::Container::default()
            },
        ],
        image_pull_secrets,
        volumes: Some(volumes),
        ..api_core::PodSpec::default()
    })
}

pub fn spec_to_deployment<R: KubeRuntimeData>(
    runtime: &R,
    spec: &ModuleSpec<DockerConfig>,
) -> Result<(String, apps::Deployment)> {
    //labels
    let module_label_value = sanitize_dns_value(spec.name())?;
    let device_label_value = sanitize_dns_value(runtime.device_id())?;
    let hubname_label = sanitize_dns_value(runtime.iot_hub_hostname())?;
    let deployment_name = format!(
        "{}-{}-{}",
        &module_label_value, &device_label_value, &hubname_label
    );
    let module_image = spec.config().image().to_string();

    let mut labels = BTreeMap::<String, String>::new();
    labels.insert(EDGE_MODULE_LABEL.to_string(), module_label_value.clone());
    labels.insert(EDGE_DEVICE_LABEL.to_string(), device_label_value);
    labels.insert(EDGE_HUBNAME_LABEL.to_string(), hubname_label);
    let deployment_labels = labels.clone();
    let selector_labels = labels.clone();

    if let Some(spec_labels) = spec.config().create_options().labels() {
        for (label, value) in spec_labels.iter() {
            labels.insert(label.clone(), value.clone());
        }
    }

    // annotations
    let mut annotations = BTreeMap::<String, String>::new();
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
                    labels: Some(labels),
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
    use crate::runtime::{DeviceIdentity, KubeRuntimeData, ProxySettings, ServiceSettings};
    use docker::models::AuthConfig;
    use docker::models::ContainerCreateBody;
    use docker::models::HostConfig;
    use docker::models::Mount;
    use edgelet_core::ModuleSpec;
    use edgelet_docker::DockerConfig;
    use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
    use serde_json;
    use std::collections::BTreeMap;
    use std::collections::HashMap;
    use std::str;
    use url::Url;

    struct KubeRuntimeTest {
        use_pvc: bool,
        namespace: String,
        device_identity: DeviceIdentity,
        proxy_settings: ProxySettings,
        service_settings: ServiceSettings,
    }

    impl KubeRuntimeTest {
        pub fn new(
            use_pvc: bool,
            namespace: String,
            device_identity: DeviceIdentity,
            proxy_settings: ProxySettings,
            service_settings: ServiceSettings,
        ) -> KubeRuntimeTest {
            KubeRuntimeTest {
                use_pvc,
                namespace,
                device_identity,
                proxy_settings,
                service_settings,
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
            &self.device_identity.iot_hub_hostname
        }
        fn device_id(&self) -> &str {
            &self.device_identity.device_id
        }
        fn edge_hostname(&self) -> &str {
            &self.device_identity.edge_hostname
        }
        fn proxy_image(&self) -> &str {
            &self.proxy_settings.proxy_image
        }
        fn proxy_config_path(&self) -> &str {
            &self.proxy_settings.proxy_config_path
        }
        fn proxy_config_map_name(&self) -> &str {
            &self.proxy_settings.proxy_config_map_name
        }
        fn service_account_name(&self) -> &str {
            &self.service_settings.service_account_name
        }
        fn workload_uri(&self) -> &Url {
            &self.service_settings.workload_uri
        }
        fn management_uri(&self) -> &Url {
            &self.service_settings.management_uri
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

    #[test]
    fn deployment_success() {
        let runtime = KubeRuntimeTest::new(
            true,
            String::from("default1"),
            DeviceIdentity::new(
                String::from("iotHub"),
                String::from("device1"),
                String::from("$edgeAgent"),
            ),
            ProxySettings::new(
                String::from("proxy:latest"),
                String::from("/etc/traefik"),
                String::from("device1-iotedged-proxy-config"),
            ),
            ServiceSettings::new(
                String::from("iotedge"),
                Url::parse("http://localhost:35000").unwrap(),
                Url::parse("http://localhost:35001").unwrap(),
            ),
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
                }

                assert!(podspec.image_pull_secrets.is_some());
                // 4 bind mounts, 2 volume mounts, 1 proxy configmap
                assert_eq!(podspec.volumes.as_ref().map(Vec::len).unwrap(), 7);
            }
        }
    }

    #[test]
    fn auth_to_image_pull_secret_success() {
        let mut auths = BTreeMap::<String, AuthEntry>::new();
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
        assert_eq!(
            secret.kind.as_ref().unwrap(),
            "kubernetes.io/dockerconfigjson"
        );
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
