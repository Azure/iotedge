// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, serde::Deserialize)]
pub struct ModuleSpec {
    name: String,
    r#type: String,
    config: ModuleConfig,

    #[serde(rename = "imagePullPolicy", skip_serializing_if = "Option::is_none")]
    image_pull_policy: Option<String>,
}

#[derive(Debug, serde::Serialize, serde::Deserialize)]
#[allow(clippy::module_name_repetitions)]
pub struct ListModulesResponse {
    pub modules: Vec<ModuleDetails>,
}

#[derive(Debug, serde::Serialize, serde::Deserialize)]
pub struct ModuleDetails {
    pub id: String,
    pub name: String,
    pub r#type: String,
    pub config: ModuleConfig,
    pub status: ModuleStatus,
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct ModuleConfig {
    pub settings: serde_json::Value,

    #[serde(skip_serializing_if = "Option::is_none")]
    env: Option<Vec<EnvVar>>,
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct EnvVar {
    key: String,
    value: String,
}

#[derive(Debug, serde::Serialize, serde::Deserialize)]
#[cfg_attr(test, derive(PartialEq))]
#[serde(rename_all = "camelCase")]
pub struct ModuleStatus {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub start_time: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub exit_status: Option<ExitStatus>,

    pub runtime_status: RuntimeStatus,
}

#[derive(Debug, serde::Serialize, serde::Deserialize)]
#[cfg_attr(test, derive(PartialEq))]
#[serde(rename_all = "camelCase")]
pub struct ExitStatus {
    pub exit_time: String,
    pub status_code: String,
}

#[derive(Debug, serde::Serialize, serde::Deserialize)]
#[cfg_attr(test, derive(PartialEq))]
pub struct RuntimeStatus {
    pub status: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
}

impl ModuleSpec {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn to_runtime_spec<M>(
        self,
    ) -> Result<edgelet_settings::ModuleSpec<<M as edgelet_core::ModuleRuntime>::Config>, String>
    where
        M: edgelet_core::ModuleRuntime,
        <M as edgelet_core::ModuleRuntime>::Config: serde::de::DeserializeOwned,
    {
        let mut env = std::collections::BTreeMap::new();

        if let Some(vars) = self.config.env {
            for var in vars {
                if env.insert(var.key.clone(), var.value).is_some() {
                    return Err(format!("duplicate env var key: {}", var.key));
                }
            }
        }

        let config = serde_json::from_value(self.config.settings)
            .map_err(|err| format!("invalid Docker config: {}", err))?;

        let image_pull_policy = match self.image_pull_policy {
            Some(policy) => std::str::FromStr::from_str(&policy)
                .map_err(|_| "invalid imagePullPolicy".to_string())?,
            None => edgelet_settings::module::ImagePullPolicy::default(),
        };

        edgelet_settings::ModuleSpec::new(self.name, self.r#type, config, env, image_pull_policy)
    }
}

impl<M> std::convert::From<Vec<(M, edgelet_core::ModuleRuntimeState)>> for ListModulesResponse
where
    M: edgelet_core::Module,
    M::Config: serde::Serialize,
{
    fn from(modules: Vec<(M, edgelet_core::ModuleRuntimeState)>) -> Self {
        let mut response = vec![];

        for module in modules {
            response.push(module.into());
        }

        ListModulesResponse { modules: response }
    }
}

impl ModuleDetails {
    pub fn from_spec(spec: &ModuleSpec, status: edgelet_core::ModuleStatus) -> Self {
        ModuleDetails {
            id: "id".to_string(),
            name: spec.name.clone(),
            r#type: spec.r#type.clone(),
            config: spec.config.clone(),

            status: ModuleStatus {
                start_time: None,
                exit_status: None,
                runtime_status: RuntimeStatus {
                    status: status.to_string(),
                    description: None,
                },
            },
        }
    }
}

impl<M> std::convert::From<(M, edgelet_core::ModuleRuntimeState)> for ModuleDetails
where
    M: edgelet_core::Module,
    M::Config: serde::Serialize,
{
    fn from((module, state): (M, edgelet_core::ModuleRuntimeState)) -> Self {
        ModuleDetails {
            id: "id".to_string(),
            name: module.name().to_string(),
            r#type: module.type_().to_string(),
            config: ModuleConfig {
                settings: serde_json::to_value(module.config()).unwrap_or_default(),
                env: Some(vec![]),
            },
            status: state.into(),
        }
    }
}

impl std::convert::From<edgelet_core::ModuleRuntimeState> for ModuleStatus {
    fn from(state: edgelet_core::ModuleRuntimeState) -> Self {
        let start_time = state.started_at().map(chrono::DateTime::to_rfc3339);

        let exit_status = if let (Some(code), Some(time)) = (state.exit_code(), state.finished_at())
        {
            Some(ExitStatus {
                exit_time: time.to_rfc3339(),
                status_code: code.to_string(),
            })
        } else {
            None
        };

        ModuleStatus {
            start_time,
            exit_status,
            runtime_status: RuntimeStatus {
                status: state.status().to_string(),
                description: state.status_description().map(String::from),
            },
        }
    }
}

#[cfg(test)]
mod tests {
    use edgelet_core::{Module, ModuleRuntimeState};

    #[test]
    fn into_runtime_spec() {
        let module_spec = super::ModuleSpec {
            name: "testModule".to_string(),
            r#type: "test".to_string(),
            config: super::ModuleConfig {
                settings: serde_json::json!({
                    "image": "testImage",
                    "imageHash": "testHash",
                    "digest": "testDigest",
                }),
                env: Some(vec![super::EnvVar {
                    key: "testKey".to_string(),
                    value: "testValue".to_string(),
                }]),
            },
            image_pull_policy: None,
        };

        let runtime_spec: edgelet_settings::ModuleSpec<edgelet_settings::DockerConfig> =
            module_spec
                .clone()
                .to_runtime_spec::<edgelet_docker::DockerModuleRuntime>()
                .unwrap();
        let expected_env: std::collections::BTreeMap<String, String> =
            [("testKey".to_string(), "testValue".to_string())]
                .iter()
                .cloned()
                .collect();

        assert_eq!(module_spec.name, runtime_spec.name());
        assert_eq!(module_spec.r#type, runtime_spec.r#type());
        assert_eq!(
            edgelet_settings::module::ImagePullPolicy::default(),
            runtime_spec.image_pull_policy()
        );
        assert_eq!(expected_env, runtime_spec.env().clone());

        let runtime_config = runtime_spec.config();
        assert_eq!("testImage", runtime_config.image());
        assert_eq!(Some("testHash"), runtime_config.image_hash());
        assert_eq!(Some("testDigest"), runtime_config.digest());
    }

    #[test]
    fn into_module_status() {
        let timestamp = chrono::NaiveDateTime::from_timestamp(0, 0);
        let timestamp =
            chrono::DateTime::<chrono::offset::Utc>::from_utc(timestamp, chrono::offset::Utc);

        // Running module
        let status = ModuleRuntimeState::default()
            .with_status(edgelet_core::ModuleStatus::Running)
            .with_started_at(Some(timestamp));

        assert_eq!(
            super::ModuleStatus {
                start_time: Some(timestamp.to_rfc3339()),
                exit_status: None,
                runtime_status: super::RuntimeStatus {
                    status: "running".to_string(),
                    description: None,
                }
            },
            status.into()
        );

        // Exited module
        let status = ModuleRuntimeState::default()
            .with_status(edgelet_core::ModuleStatus::Stopped)
            .with_started_at(Some(timestamp))
            .with_finished_at(Some(timestamp))
            .with_exit_code(Some(0));

        assert_eq!(
            super::ModuleStatus {
                start_time: Some(timestamp.to_rfc3339()),
                exit_status: Some(super::ExitStatus {
                    exit_time: timestamp.to_rfc3339(),
                    status_code: "0".to_string(),
                }),
                runtime_status: super::RuntimeStatus {
                    status: "stopped".to_string(),
                    description: None,
                }
            },
            status.into()
        );
    }

    // Common data set for tests.
    fn test_modules() -> (
        Vec<(edgelet_test_utils::runtime::Module, ModuleRuntimeState)>,
        chrono::DateTime<chrono::offset::Utc>,
    ) {
        let timestamp = chrono::NaiveDateTime::from_timestamp(0, 0);
        let timestamp =
            chrono::DateTime::<chrono::offset::Utc>::from_utc(timestamp, chrono::offset::Utc);

        let modules = vec![
            // Running module
            (
                edgelet_test_utils::runtime::Module::default(),
                ModuleRuntimeState::default()
                    .with_status(edgelet_core::ModuleStatus::Running)
                    .with_started_at(Some(timestamp)),
            ),
            // Exited module
            (
                edgelet_test_utils::runtime::Module::default(),
                ModuleRuntimeState::default()
                    .with_status(edgelet_core::ModuleStatus::Stopped)
                    .with_started_at(Some(timestamp))
                    .with_finished_at(Some(timestamp))
                    .with_exit_code(Some(0)),
            ),
        ];

        (modules, timestamp)
    }

    #[test]
    fn into_module_details() {
        let (modules, _) = test_modules();

        for (module, state) in modules {
            let details: super::ModuleDetails = (module.clone(), state.clone()).into();
            assert_eq!("id", &details.id);
            assert_eq!(module.name(), &details.name);
            assert_eq!(module.type_(), &details.r#type);
            assert_eq!(
                serde_json::to_value(module.config()).unwrap(),
                details.config.settings
            );

            let status: super::ModuleStatus = state.into();
            assert_eq!(status, details.status);
        }
    }
}
