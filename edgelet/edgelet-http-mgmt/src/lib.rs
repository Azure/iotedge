// Copyright (c) Microsoft. All rights reserved.

mod device_actions;
mod identity;
mod module;
mod system_info;

#[derive(Clone)]
pub struct DeviceManagement {
    sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
}

impl DeviceManagement {
    pub fn new(sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>) -> Self {
        DeviceManagement { sender }
    }
}

http_common::make_service! {
    service: DeviceManagement,
    api_version: edgelet_http::ApiVersion,
    routes: [
        device_actions::reprovision::Route,
    ],
}

#[derive(Clone)]
pub struct ModuleManagement<M>
where
    M: edgelet_core::ModuleRuntime,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

http_common::make_service! {
    service: ModuleManagement<M>,
    { <M> }
    { M: edgelet_core::ModuleRuntime + Send + Sync + 'static }
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::create_or_list::Route<M>,

        system_info::get::Route<M>,
        system_info::resources::Route<M>,
        system_info::support_bundle::Route<M>,
    ],
}

#[derive(Clone)]
pub struct IdentityManagement {
    client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
}

impl IdentityManagement {
    pub fn new(identity_socket: &url::Url) -> Result<Self, http_common::ConnectorError> {
        let connector = http_common::Connector::new(identity_socket)?;

        let client = aziot_identity_client_async::Client::new(
            aziot_identity_common_http::ApiVersion::V2020_09_01,
            connector,
        );
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        Ok(IdentityManagement { client })
    }
}

http_common::make_service! {
    service: IdentityManagement,
    api_version: edgelet_http::ApiVersion,
    routes: [
        identity::create_or_list::Route,
        identity::delete_or_update::Route,
    ],
}
