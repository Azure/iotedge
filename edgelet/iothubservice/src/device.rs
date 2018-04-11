// Copyright (c) Microsoft. All rights reserved.

use hyper::{Error as HyperError, Request, Response, client::Service};

use client::Client;
use error::Result;

pub struct DeviceClient<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    _client: Client<S>,
    device_id: String,
}

impl<S> DeviceClient<S>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    pub fn new(client: Client<S>, device_id: &str) -> Result<DeviceClient<S>> {
        Ok(DeviceClient {
            _client: client,
            device_id: ensure_not_empty!(device_id).to_string(),
        })
    }

    pub fn device_id(&self) -> &str {
        self.device_id.as_ref()
    }
}
