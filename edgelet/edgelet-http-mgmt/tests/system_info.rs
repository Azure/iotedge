// Copyright (c) Microsoft. All rights reserved.

use edgelet_test_utils::server::TestServer;

#[tokio::test]
async fn system_info() {
    let service = edgelet_http_mgmt::Service {};
    let server = TestServer::start(service).await;

    let uri = TestServer::make_uri("systeminfo", edgelet_http::ApiVersion::Latest);
    let info = server
        .process_request::<(), edgelet_core::SystemInfo>(http::Method::GET, uri, None)
        .await
        .expect("failed to get system info");
    assert_eq!(edgelet_core::SystemInfo::default(), info);
}
