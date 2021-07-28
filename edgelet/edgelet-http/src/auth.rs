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
    let actual_name = module_name; // TODO: Get this from docker

    if module_name != actual_name {
        log::info!(
            "Only {} is authorized for this endpoint; {} (pid {}) not authorized.",
            module_name,
            actual_name,
            pid
        );

        return Err(http_common::server::Error {
            status_code: http::StatusCode::FORBIDDEN,
            message: "forbidden".into(),
        });
    }

    Ok(())
}
