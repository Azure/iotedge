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

#[cfg(test)]
mod tests {
    use edgelet_core::ModuleRuntimeState;

    #[test]
    fn into_module_status() {
        let timestamp = chrono::NaiveDateTime::from_timestamp(0, 0);
        let timestamp =
            chrono::DateTime::<chrono::offset::Utc>::from_utc(timestamp, chrono::offset::Utc);

        // Running module
        let status = ModuleRuntimeState::default()
            .with_status(edgelet_core::ModuleStatus::Running)
            .with_started_at(Some(timestamp));
        let status: super::ModuleStatus = status.into();

        assert_eq!(Some(timestamp.to_rfc3339()), status.start_time);
        assert!(status.exit_status.is_none());
        assert_eq!("running", &status.runtime_status.status);

        // Exited module
        let status = ModuleRuntimeState::default()
            .with_status(edgelet_core::ModuleStatus::Stopped)
            .with_started_at(Some(timestamp))
            .with_finished_at(Some(timestamp))
            .with_exit_code(Some(0));
        let status: super::ModuleStatus = status.into();

        assert_eq!(Some(timestamp.to_rfc3339()), status.start_time);
        assert_eq!("stopped", &status.runtime_status.status);

        let exit_status = status.exit_status.unwrap();
        assert_eq!(timestamp.to_rfc3339(), exit_status.exit_time);
        assert_eq!("0".to_string(), exit_status.status_code);
    }

    #[test]
    fn into_module_details() {}

    #[test]
    fn into_list_modules_response() {}
}
