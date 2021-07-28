// Copyright (c) Microsoft. All rights reserved.

#[allow(clippy::module_name_repetitions)]
pub async fn auth_agent(
    pid: libc::pid_t,
    runtime: &std::sync::Arc<futures_util::lock::Mutex<impl edgelet_core::ModuleRuntime>>,
) -> Result<(), http_common::server::Error> {
    auth_caller("edgeAgent", pid, runtime).await
}

#[allow(clippy::module_name_repetitions)]
pub async fn auth_caller(
    module_name: &str,
    pid: libc::pid_t,
    runtime: &std::sync::Arc<futures_util::lock::Mutex<impl edgelet_core::ModuleRuntime>>,
) -> Result<(), http_common::server::Error> {
    let module_pids = {
        let runtime = runtime.lock().await;

        runtime.module_top(module_name).await.map_err(|err| {
            log::info!("Auth for {} failed: {}", module_name, err);

            crate::error::forbidden()
        })
    }?;

    if !module_pids.contains(&pid) {
        log::info!(
            "Only {} is authorized for this endpoint; pid {} not authorized.",
            module_name,
            pid
        );

        return Err(crate::error::forbidden());
    }

    Ok(())
}
