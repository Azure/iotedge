// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, serde::Serialize)]
#[allow(clippy::module_name_repetitions)]
pub struct ListModulesResponse {
    modules: Vec<ModuleDetails>,
}

#[derive(Debug, serde::Serialize)]
pub struct ModuleDetails {
    id: String,
    name: String,
    r#type: String,
    config: ModuleConfig,
    status: ModuleStatus,
}

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub struct ModuleConfig {
    settings: serde_json::Value,

    #[serde(skip_serializing_if = "Option::is_none")]
    env: Option<Vec<EnvVar>>,
}

#[derive(Debug, serde::Deserialize, serde::Serialize)]
struct EnvVar {
    key: String,
    value: String,
}

#[derive(Debug, serde::Serialize)]
#[serde(rename_all = "camelCase")]
struct ModuleStatus {
    #[serde(skip_serializing_if = "Option::is_none")]
    start_time: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    exit_status: Option<ExitStatus>,

    runtime_status: RuntimeStatus,
}

#[derive(Debug, serde::Serialize)]
#[serde(rename_all = "camelCase")]
struct ExitStatus {
    exit_time: String,
    status_code: String,
}

#[derive(Debug, serde::Serialize)]
struct RuntimeStatus {
    status: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    description: Option<String>,
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
