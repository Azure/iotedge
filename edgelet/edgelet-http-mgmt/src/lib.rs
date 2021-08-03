// Copyright (c) Microsoft. All rights reserved.

mod device_actions;
mod identity;
mod module;
mod system_info;

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    identity: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
}

impl<M> Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    pub fn new(
        identity_socket: &url::Url,
        runtime: M,
        reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
    ) -> Result<Self, http_common::ConnectorError> {
        let connector = http_common::Connector::new(identity_socket)?;

        let identity = aziot_identity_client_async::Client::new(
            aziot_identity_common_http::ApiVersion::V2020_09_01,
            connector,
        );

        let identity = std::sync::Arc::new(futures_util::lock::Mutex::new(identity));
        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        Ok(Service {
            identity,
            runtime,
            reprovision,
        })
    }
}

http_common::make_service! {
    service: Service<M>,
    { <M> }
    {
        M: edgelet_core::ModuleRuntime + Send + Sync + 'static,
        M::Config: serde::Serialize,
        M::Logs: Send,
    }
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::create_or_list::Route<M>,
        module::delete_or_get_or_update::Route<M>,
        module::restart_or_start_or_stop::Route<M>,
        module::logs::Route<M>,
        module::prepare_update::Route<M>,

        identity::create_or_list::Route<M>,
        identity::delete_or_update::Route<M>,

        system_info::get::Route<M>,
        system_info::resources::Route<M>,
        system_info::support_bundle::Route<M>,

        device_actions::reprovision::Route<M>,
    ],
}
