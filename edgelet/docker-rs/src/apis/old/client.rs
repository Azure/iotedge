// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use super::configuration::Configuration;
use hyper;

pub struct APIClient<C: hyper::client::connect::Connect> {
    configuration: Arc<Configuration<C>>,
    container_api: Box<dyn crate::apis::ContainerApi>,
    image_api: Box<dyn crate::apis::ImageApi>,
    network_api: Box<dyn crate::apis::NetworkApi>,
    system_api: Box<dyn crate::apis::SystemApi>,
    volume_api: Box<dyn crate::apis::VolumeApi>,
}

impl<C: hyper::client::connect::Connect + 'static> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> Self {
        let configuration = Arc::new(configuration);

        APIClient {
            configuration: configuration.clone(),
            container_api: Box::new(crate::apis::ContainerApiClient::new(configuration.clone())),
            image_api: Box::new(crate::apis::ImageApiClient::new(configuration.clone())),
            network_api: Box::new(crate::apis::NetworkApiClient::new(configuration.clone())),
            system_api: Box::new(crate::apis::SystemApiClient::new(configuration.clone())),
            volume_api: Box::new(crate::apis::VolumeApiClient::new(configuration)),
        }
    }

    pub fn container_api(&self) -> &dyn crate::apis::ContainerApi {
        self.container_api.as_ref()
    }

    pub fn image_api(&self) -> &dyn crate::apis::ImageApi {
        self.image_api.as_ref()
    }

    pub fn network_api(&self) -> &dyn crate::apis::NetworkApi {
        self.network_api.as_ref()
    }

    pub fn system_api(&self) -> &dyn crate::apis::SystemApi {
        self.system_api.as_ref()
    }

    pub fn volume_api(&self) -> &dyn crate::apis::VolumeApi {
        self.volume_api.as_ref()
    }
}
