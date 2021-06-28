// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn start(
    settings: &edgelet_docker::Settings,
    sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
) -> Result<(), EdgedError> {
    // TODO: fix support in http_common for fd://
    let socket = url::Url::parse("unix:///tmp/mgmt_test.sock").unwrap();

    let connector = http_common::Connector::new(&socket)
        .map_err(|err| EdgedError::from_err("Invalid management API URL", err))?;

    let identity_mgmt =
        edgelet_http_mgmt::IdentityManagement::new(&settings.base.endpoints.aziot_identityd_url)
            .map_err(|err| EdgedError::from_err("Invalid Identity Service URL", err))?;

    let mut identity_incoming = connector.clone().incoming().await.map_err(|err| {
        EdgedError::from_err("Failed to listen on management socket", err)
    })?;

    tokio::spawn(async move {
        if let Err(err) = identity_incoming.serve(identity_mgmt).await {
            log::error!("Failed to serve management socket: {}", err);
        }
    });

    let device_mgmt = edgelet_http_mgmt::DeviceManagement::new(sender);
    let mut device_incoming = connector.incoming().await.map_err(|err| {
        EdgedError::from_err("Failed to listen on management socket", err)
    })?;

    tokio::spawn(async move {
        if let Err(err) = device_incoming.serve(device_mgmt).await {
            log::error!("Failed to serve management socket: {}", err);
        }
    });

    Ok(())
}
