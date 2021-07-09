// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn start(
    settings: &edgelet_docker::Settings,
    sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
) -> Result<(), EdgedError> {
    // TODO: fix support in http_common for fd://
    /*let socket = url::Url::parse("unix:///tmp/mgmt_test.sock").unwrap();

    let connector = http_common::Connector::new(&socket)
        .map_err(|err| EdgedError::from_err("Invalid management API URL", err))?;

    let service =
        edgelet_http_mgmt::Service::new(&settings.base.endpoints.aziot_identityd_url, sender)
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

    Ok(())
}
