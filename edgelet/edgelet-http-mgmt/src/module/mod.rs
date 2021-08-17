// Copyright (c) Microsoft. All rights reserved.

pub(super) mod create_or_list;
pub(super) mod delete_or_get_or_update;
pub(super) mod restart_or_start_or_stop;

pub(super) mod logs;
pub(super) mod prepare_update;

#[derive(serde::Deserialize)]
pub(crate) struct ModuleSpec {
    name: String,
    r#type: String,
    config: edgelet_http::ModuleConfig,

    #[serde(rename = "imagePullPolicy", skip_serializing_if = "Option::is_none")]
    image_pull_policy: Option<String>,
}

fn to_module_details(
    spec: &ModuleSpec,
    status: edgelet_core::ModuleStatus,
) -> edgelet_http::ModuleDetails {
    todo!()
}

type DockerSpec = edgelet_settings::ModuleSpec<edgelet_settings::DockerConfig>;

impl std::convert::TryInto<DockerSpec> for ModuleSpec {
    type Error = String;

    fn try_into(self) -> Result<DockerSpec, Self::Error> {
        todo!()
    }
}
