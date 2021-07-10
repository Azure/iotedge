// Copyright (c) Microsoft. All rights reserved.

mod module;
mod trust_bundle;

#[derive(Clone)]
pub struct Service {}

impl Service {
    pub fn new() -> Self {
        Service {}
    }
}

http_common::make_service! {
    service: Service,
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::list::Route,

        trust_bundle::Route,
    ],
}
