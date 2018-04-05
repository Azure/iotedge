// Copyright (c) Microsoft. All rights reserved.

use std::rc::Rc;

use hyper;
use super::configuration::Configuration;

pub struct APIClient<C: hyper::client::Connect> {
    configuration: Rc<Configuration<C>>,
    container_api: Box<::apis::ContainerApi>,
    image_api: Box<::apis::ImageApi>,
    network_api: Box<::apis::NetworkApi>,
    system_api: Box<::apis::SystemApi>,
    volume_api: Box<::apis::VolumeApi>,
}

impl<C: hyper::client::Connect> APIClient<C> {
    pub fn new(configuration: Configuration<C>) -> APIClient<C> {
        let rc = Rc::new(configuration);

        APIClient {
            configuration: rc.clone(),
            container_api: Box::new(::apis::ContainerApiClient::new(rc.clone())),
            image_api: Box::new(::apis::ImageApiClient::new(rc.clone())),
            network_api: Box::new(::apis::NetworkApiClient::new(rc.clone())),
            system_api: Box::new(::apis::SystemApiClient::new(rc.clone())),
            volume_api: Box::new(::apis::VolumeApiClient::new(rc.clone())),
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
