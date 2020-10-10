// Copyright (c) Microsoft. All rights reserved.

use failure::{Fail};
use futures::future::Future;
use futures::prelude::*;
use hyper::client::{Client as HyperClient};
use hyper::{Body, Client};
use typed_headers::{self, http};

use edgelet_http::{UrlConnector};

use crate::error::{Error, ErrorKind, RequestType};
use url::Url;

#[derive(Clone)]
pub struct IdentityClient {
	api_version: aziot_identity_common_http::ApiVersion,
    client: HyperClient<UrlConnector, Body>,
    host: Url,
}

impl IdentityClient {
    pub fn new(api_version: aziot_identity_common_http::ApiVersion, url: &url::Url) -> Self {

        
        let client = Client::builder()
            .build(UrlConnector::new(
                &url).expect("Hyper client"));
        IdentityClient {
            api_version,
            client,
            host: url.clone(),
        }
    }

    pub fn get_device(
        &self,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send>
    {
        let uri = format!("{}identities/device?api-version={}", self.host.as_str(), self.api_version);
        let body = serde_json::json! {{ "type": "aziot" }};

        let identity = request(
            &self.client,
            hyper::Method::POST,
            &uri,
            Some(&body),
        )
        .and_then(|identity| {
            Ok(identity)
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::GetDevice))));

        Box::new(identity)

    }
    
    pub fn reprovision_device(
        &self,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send> 
    {
        let uri = format!("{}identities/device/reprovision?api-version={}", self.host.as_str(), self.api_version);
        let body = serde_json::json! {{ "type": "aziot" }};

        let res = request::<_, _, ()>(
            &self.client,
            hyper::Method::POST,
            &uri,
            Some(&body),
        )
        .and_then(|_| {
            Ok(())
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::ReprovisionDevice))));

        Box::new(res)
    }

    pub fn create_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send>
    {
        let uri = format!("{}identities/modules?api-version={}", self.host.as_str(), self.api_version);
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = request(
            &self.client,
            hyper::Method::POST,
            &uri,
            Some(&body),
        )
        .and_then(|identity| {
            Ok(identity)
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::CreateModule))));

        Box::new(identity)
    }

    pub fn update_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send>
    {
        let uri = format!("{}identities/modules/{}?api-version={}", self.host.as_str(), module_name, self.api_version);
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = request(
            &self.client,
            hyper::Method::PUT,
            &uri,
            Some(&body),
        )
        .and_then(|identity| {
            Ok(identity)
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::UpdateModule))));

        Box::new(identity)
    }

    pub fn delete_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send> 
    {       
        let uri = format!("{}identities/modules/{}?api-version={}", self.host.as_str(), module_name, self.api_version);

        let res = request::<_, (), ()>(
            &self.client,
            hyper::Method::DELETE,
            &uri,
            None,
        )
        .and_then(|_| {
            Ok(())
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::DeleteModule))));

        Box::new(res)
    }

    pub fn get_module(
        &self,
        module_name: &str,
    ) -> Box<dyn Future<Item = aziot_identity_common::Identity, Error = Error> + Send>
    {
        let uri = format!("{}identities/modules/{}?api-version={}", self.host.as_str(), module_name, self.api_version);
        let body = serde_json::json! {{ "type": "aziot", "moduleId" : module_name }};

        let identity = request(
            &self.client,
            hyper::Method::GET,
            &uri,
            Some(&body),
        )
        .and_then(|identity| {
            Ok(identity)
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::GetModule))));

        Box::new(identity)
    }

    pub fn get_modules(
        &self,
    ) -> Box<dyn Future<Item = Vec<aziot_identity_common::Identity>, Error = Error> + Send> 
    {
        let uri = format!("{}identities/modules?api-version={}", self.host.as_str(), self.api_version);

        let identities = request::<_, (), aziot_identity_common_http::get_module_identities::Response>(
            &self.client,
            hyper::Method::GET,
            &uri,
            None,
        )
        .and_then(|identities| {
            Ok(identities.identities)
        })
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::ListModules))));
        
        Box::new(identities)
    }
}

fn request<TConnect, TRequest, TResponse>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &str,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = TResponse, Error = Error> + Send>
where
    TConnect: hyper::client::connect::Connect + Clone + Send + Sync + 'static,
    TRequest: serde::Serialize,
    TResponse: serde::de::DeserializeOwned + Send + 'static,
{
    let mut builder = hyper::Request::builder();
    builder.method(method).uri(uri);
    
    let builder =
    if let Some(body) = body {
        let body = serde_json::to_vec(body).expect("serializing request body to JSON cannot fail").into();
        builder
            .header(hyper::header::CONTENT_TYPE, "application/json")
            .body(body)
    }
    else {
        builder.body(Default::default())
    };
    
    let req = builder.expect("cannot fail to create hyper request");
    
    Box::new(
        client
        .request(req)
        .map_err(|e| Error::from(e.context(ErrorKind::Request)))
        .and_then(|resp| {
            let (http::response::Parts { status, .. }, body) = resp.into_parts();
            body.concat2()
                .and_then(move |body| Ok((status, body)))
                .map_err(|e| Error::from(e.context(ErrorKind::Hyper)))
        })
        .and_then(|(status, body)| {
            if status.is_success() {
                Ok(body)
            } else {
                Err(Error::http_with_error_response(status, &*body))
            }
        })
        .and_then(|body| {
            let parsed: Result<TResponse, _> =
                serde_json::from_slice(&body);
            parsed.map_err(|e| Error::from(ErrorKind::Serde(e)))
        })
    )
}
