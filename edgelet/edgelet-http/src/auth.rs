// Copyright (c) Microsoft. All rights reserved.

#[allow(clippy::module_name_repetitions)]
pub fn auth_agent(
    pid: libc::pid_t,
    runtime: &std::sync::Arc<futures_util::lock::Mutex<impl edgelet_core::ModuleRuntime>>,
) -> Result<(), http_common::server::Error> {
    auth_caller("edgeAgent", pid, runtime)
}

#[allow(clippy::module_name_repetitions)]
pub fn auth_caller(
    expected_name: &str,
    pid: libc::pid_t,
    runtime: &std::sync::Arc<futures_util::lock::Mutex<impl edgelet_core::ModuleRuntime>>,
) -> Result<(), http_common::server::Error> {
    let actual_name = expected_name; // TODO: Get this from docker

    if expected_name != actual_name {
        log::info!(
            "Only {} is authorized for this endpoint; {} (pid {}) not authorized.",
            expected_name,
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
