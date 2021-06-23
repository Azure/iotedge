// Copyright (c) Microsoft. All rights reserved.

pub(crate) async fn start() {
    let socket = url::Url::parse("unix:///tmp/mgmt_test.sock").unwrap();

    let identity_mgmt = edgelet_http_mgmt::IdentityManagement::default();
    let connector = http_common::Connector::new(&socket).unwrap();

    let mut incoming = connector
        .incoming()
        .await
        .expect("failed to listen on socket");

    incoming
        .serve(identity_mgmt)
        .await
        .expect("failed to serve socket");
}
