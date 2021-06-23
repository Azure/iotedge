// Copyright (c) Microsoft. All rights reserved.

#[allow(clippy::module_name_repetitions)]
pub fn auth_agent(pid: libc::pid_t) -> Result<(), http_common::server::Error> {
    auth_caller("edgeAgent", pid)
}

#[allow(clippy::module_name_repetitions)]
pub fn auth_caller(name: &str, pid: libc::pid_t) -> Result<(), http_common::server::Error> {
    log::info!("");

    Err(http_common::server::Error {
        status_code: http::StatusCode::FORBIDDEN,
        message: "module not authorized to access endpoint".into(),
    })
}
