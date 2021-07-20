// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn start(
    settings: &impl edgelet_settings::RuntimeSettings,
    sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
) -> Result<tokio::sync::oneshot::Sender<()>, EdgedError> {
    // TODO: use settings
    /*let socket = url::Url::parse("unix:///tmp/mgmt_test.sock").unwrap();

    let connector = http_common::Connector::new(&socket)
        .map_err(|err| EdgedError::from_err("Invalid management API URL", err))?;

    let service =
        edgelet_http_mgmt::Service::new(&settings.endpoints().aziot_identityd_url(), sender)
            .map_err(|err| EdgedError::from_err("Invalid Identity Service URL", err))?;

    let mut incoming = connector
        .incoming()
        .await
        .map_err(|err| EdgedError::from_err("Failed to listen on management socket", err))?;

    tokio::spawn(async move {
        if let Err(err) = incoming.serve(service).await {
            log::error!("Failed to serve management socket: {}", err);
        }
    });*/

    let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel::<()>();

    Ok(shutdown_tx)
}
