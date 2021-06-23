// Copyright (c) Microsoft. All rights reserved.

#[allow(clippy::module_name_repetitions)]
pub fn auth_agent(pid: libc::pid_t) -> Result<(), http_common::server::Error> {
    auth_caller("edgeAgent", pid)
}

#[allow(clippy::module_name_repetitions)]
pub fn auth_caller(
    expected_name: &str,
    pid: libc::pid_t,
) -> Result<(), http_common::server::Error> {
    let actual_name = "edgeAgent"; // TODO: Get this from docker

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
