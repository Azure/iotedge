// Copyright (c) Microsoft. All rights reserved.

mod module;

#[derive(Clone)]
pub struct Service {}

http_common::make_service! {
    service: Service,
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::list::Route,
    ],
}
