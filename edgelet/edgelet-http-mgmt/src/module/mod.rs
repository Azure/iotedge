// Copyright (c) Microsoft. All rights reserved.

pub(super) mod create_or_list;
pub(super) mod delete_or_get_or_update;
pub(super) mod restart_or_start_or_stop;

pub(super) mod logs;
pub(super) mod prepare_update;

use edgelet_core::ModuleRegistry;

async fn create_module<M>(
    runtime: &M,
    module: edgelet_http::ModuleSpec,
) -> Result<(), http_common::server::Error>
where
    M: edgelet_core::ModuleRuntime,
    <M as edgelet_core::ModuleRuntime>::Config: serde::de::DeserializeOwned,
{
    let module = module
        .to_runtime_spec::<M>()
        .map_err(|err| edgelet_http::error::server_error(err))?;

    pull_image(runtime, &module).await?;

    runtime
        .create(module)
        .await
        .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

    Ok(())
}

async fn pull_image<M>(
    runtime: &M,
    module: &edgelet_settings::ModuleSpec<<M as edgelet_core::ModuleRuntime>::Config>,
) -> Result<(), http_common::server::Error>
where
    M: edgelet_core::ModuleRuntime,
{
    match module.image_pull_policy() {
        edgelet_settings::module::ImagePullPolicy::OnCreate => {
            runtime
                .registry()
                .pull(module.config())
                .await
                .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

            log::debug!("Successfully pulled new image for module {}", module.name());
        }
        edgelet_settings::module::ImagePullPolicy::Never => {
            log::debug!(
                "Skipped pulling image for module {} as per pull policy",
                module.name()
            )
        }
    }

    Ok(())
}
