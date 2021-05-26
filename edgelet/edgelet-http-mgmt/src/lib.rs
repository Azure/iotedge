// Copyright (c) Microsoft. All rights reserved.

mod system_info;

#[derive(Clone)]
pub struct Service {}

http_common::make_service! {
    service: Service,
    api_version: edgelet_http::ApiVersion,
    routes: [
        system_info::get::Route,
    ],
}
