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
    let module_name = module_name.trim_start_matches('$');

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

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use super::{auth_agent, auth_caller};

    fn assert_is_forbidden(res: Result<(), http_common::server::Error>) {
        let res = res.unwrap_err();

        assert_eq!(http::StatusCode::FORBIDDEN, res.status_code);
    }

    #[tokio::test]
    #[allow(clippy::field_reassign_with_default)]
    async fn auth_err() {
        // Arbitrary PID for testing.
        let pid = 1000;

        // Runtime errors should cause auth to return 403 errors.
        let runtime = edgelet_test_utils::runtime::Runtime::default();
        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        assert_is_forbidden(auth_caller("runtimeError", pid, &runtime).await);
    }

    #[tokio::test]
    async fn auth_no_match() {
        // Arbitrary PID for testing.
        let pid = 1000;

        // Auth fails when no matching module is found.
        let runtime = edgelet_test_utils::runtime::Runtime::default();
        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        assert_is_forbidden(auth_agent(pid, &runtime).await);
        assert_is_forbidden(auth_caller("testModule", pid, &runtime).await);
    }

    #[tokio::test]
    async fn auth_pids() {
        let mut runtime = edgelet_test_utils::runtime::Runtime::default();
        runtime
            .module_auth
            .insert("edgeAgent".to_string(), vec![1000]);
        runtime
            .module_auth
            .insert("testModule".to_string(), vec![1001]);

        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        // auth_agent
        assert!(auth_agent(1000, &runtime).await.is_ok());
        assert_is_forbidden(auth_agent(1001, &runtime).await);

        // auth_caller
        assert!(auth_caller("testModule", 1001, &runtime).await.is_ok());
        assert_is_forbidden(auth_caller("testModule", 1000, &runtime).await);
    }
}
