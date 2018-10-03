// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient<C: hyper::client::connect::Connect> {
    configuration: Arc<Configuration<C>>,
    container_api: Box<::apis::ContainerApi>,
    image_api: Box<::apis::ImageApi>,
    network_api: Box<::apis::NetworkApi>,
    system_api: Box<::apis::SystemApi>,
    volume_api: Box<::apis::VolumeApi>,
}

impl<C: hyper::client::connect::Connect + 'static> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let configuration = Arc::new(configuration);

        APIClient {
            configuration: configuration.clone(),
            container_api: Box::new(::apis::ContainerApiClient::new(configuration.clone())),
            image_api: Box::new(::apis::ImageApiClient::new(configuration.clone())),
            network_api: Box::new(::apis::NetworkApiClient::new(configuration.clone())),
            system_api: Box::new(::apis::SystemApiClient::new(configuration.clone())),
            volume_api: Box::new(::apis::VolumeApiClient::new(configuration.clone())),
        }
    }

    pub fn container_api(&self) -> &::apis::ContainerApi {
        self.container_api.as_ref()
    }

    pub fn image_api(&self) -> &::apis::ImageApi {
        self.image_api.as_ref()
    }

    pub fn network_api(&self) -> &::apis::NetworkApi {
        self.network_api.as_ref()
    }

    pub fn system_api(&self) -> &::apis::SystemApi {
        self.system_api.as_ref()
    }

    pub fn volume_api(&self) -> &::apis::VolumeApi {
        self.volume_api.as_ref()
    }
}
