// Copyright (c) Microsoft. All rights reserved.

pub(crate) async fn start() {
    let socket = url::Url::parse("unix:///tmp/mgmt_test.sock").unwrap();
    let connector = http_common::Connector::new(&socket).unwrap();

    let identity_socket = url::Url::parse("unix:///run/aziot/identityd.sock")
        .expect("cannot fail to parse hardcoded path");
    let identity_mgmt = edgelet_http_mgmt::IdentityManagement::new(identity_socket)
        .expect("hardcoded Identity socket must be valid");

    let mut identity_incoming = connector
        .clone()
        .incoming()
        .await
        .expect("failed to listen on socket");

    tokio::spawn(async move {
        identity_incoming
            .serve(identity_mgmt)
            .await
            .expect("failed to serve socket");
    });

    let device_mgmt = edgelet_http_mgmt::DeviceManagement {};
    let mut device_incoming = connector
        .incoming()
        .await
        .expect("failed to listen on socket");

    device_incoming
        .serve(device_mgmt)
        .await
        .expect("failed to serve socket");
}
