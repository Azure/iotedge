// Copyright (c) Microsoft. All rights reserved.

mod device_actions;
mod identity;
mod module;
mod system_info;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use edgelet_test_utils::clients::IdentityClient;

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    identity: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
}

impl<M> Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    #[cfg(not(test))]
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

    // Test constructor used to create a test Management Service.
    #[cfg(test)]
    pub fn new(runtime: M) -> Self {
        let identity = edgelet_test_utils::clients::IdentityClient::default();
        let identity = std::sync::Arc::new(futures_util::lock::Mutex::new(identity));

        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        // We won't use the reprovision sender, but it must be created to construct the
        // Service struct. Note that we drop the reprovision receiver, which will cause
        // tests to panic if they use the reprovision sender.
        let (reprovision_tx, _) =
            tokio::sync::mpsc::unbounded_channel::<edgelet_core::ShutdownReason>();

        Service {
            identity,
            runtime,
            reprovision: reprovision_tx,
        }
    }

    // Test constructor that returns the reprovision receiver. Only used by the reprovision
    // API tests.
    #[cfg(test)]
    pub fn new_with_reprovision(
        runtime: M,
    ) -> (
        Self,
        tokio::sync::mpsc::UnboundedReceiver<edgelet_core::ShutdownReason>,
    ) {
        let identity = edgelet_test_utils::clients::IdentityClient::default();
        let identity = std::sync::Arc::new(futures_util::lock::Mutex::new(identity));

        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        let (reprovision_tx, reprovision_rx) =
            tokio::sync::mpsc::unbounded_channel::<edgelet_core::ShutdownReason>();

        (
            Service {
                identity,
                runtime,
                reprovision: reprovision_tx,
            },
            reprovision_rx,
        )
    }
}

http_common::make_service! {
    service: Service<M>,
    { <M> }
    {
        M: edgelet_core::ModuleRuntime + Send + Sync + 'static,
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
