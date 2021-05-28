// Copyright (c) Microsoft. All rights reserved.

use edgelet_test_utils::server::TestServer;

use edgelet_core::MakeModuleRuntime;

#[tokio::test]
async fn system_info() {
    let settings = edgelet_test_utils::module::TestSettings::new();
    let runtime: edgelet_test_utils::module::TestRuntime<edgelet_core::ErrorKind> =
        edgelet_test_utils::module::TestRuntime::make_runtime(settings)
            .await
            .unwrap();
    let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

    let service = edgelet_http_mgmt::Service { runtime };
    let server = TestServer::start(service).await;

    let uri = TestServer::make_uri("systeminfo", edgelet_http::ApiVersion::Latest);
    let info = server
        .process_request::<(), edgelet_core::SystemInfo>(http::Method::GET, uri, None)
        .await
        .expect("failed to get system info");
    assert_eq!(
        edgelet_core::SystemInfo {
            os_type: "os_type_sample".to_string(),
            architecture: "architecture_sample".to_string(),
            version: edgelet_core::version_with_source_version(),
            provisioning: edgelet_core::ProvisioningInfo {
                r#type: "test".to_string(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: true,
            },
            cpus: 0,
            virtualized: "test".to_string(),
            kernel_version: "test".to_string(),
            operating_system: "test".to_string(),
            server_version: "test".to_string(),
        },
        info
    );
}
