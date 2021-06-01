// Copyright (c) Microsoft. All rights reserved.

mod system_info;

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    pub runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

http_common::make_service! {
    service: Service<M>,
    { <M> }
    { M: edgelet_core::ModuleRuntime + Send + Sync + 'static }
    api_version: edgelet_http::ApiVersion,
    routes: [
        system_info::get::Route<M>,
        system_info::resources::Route<M>,
        system_info::support_bundle::Route<M>,
    ],
}
