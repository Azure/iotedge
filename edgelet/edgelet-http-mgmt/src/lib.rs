// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod system_info;

#[derive(Clone)]
pub struct ModuleManagement<M>
where
    M: edgelet_core::ModuleRuntime,
{
    pub runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Clone)]
pub struct IdentityManagement {
    pub client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
}

http_common::make_service! {
    service: ModuleManagement<M>,
    { <M> }
    { M: edgelet_core::ModuleRuntime + Send + Sync + 'static }
    api_version: edgelet_http::ApiVersion,
    routes: [
        system_info::get::Route<M>,
        system_info::resources::Route<M>,
        system_info::support_bundle::Route<M>,
    ],
}

http_common::make_service! {
    service: IdentityManagement,
    api_version: edgelet_http::ApiVersion,
    routes: [
        identity::create_or_list::Route,
        identity::delete_or_update::Route,
    ],
}

impl Default for IdentityManagement {
    fn default() -> Self {
        // Use default Identity Service socket path and latest API version.
        let socket = url::Url::parse("unix:///run/aziot/identityd.sock")
            .expect("cannot fail to parse hardcoded path");
        let connector =
            http_common::Connector::new(&socket).expect("cannot fail to create connector");

        let client = aziot_identity_client_async::Client::new(
            aziot_identity_common_http::ApiVersion::V2020_09_01,
            connector,
        );
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        IdentityManagement { client }
    }
}
