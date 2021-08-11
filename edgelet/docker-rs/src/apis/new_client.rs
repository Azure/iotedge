use std::borrow::Borrow;
use std::error::Error;
use std::sync::Arc;

use futures::{Future, Stream};
use serde_json;
use typed_headers::{self, mime, HeaderMapExt};

use http_common::{request, Connector};
use hyper::{Body, Client, Uri};

use super::configuration::Configuration;

pub struct DockerApiClient {
    client: Arc<Client<Connector, Body>>,
    configuration: Configuration,
}

impl DockerApiClient {
    pub fn new(client: Arc<Client<Connector, Body>>) -> Self {
        Self {
            client,
            configuration: Configuration::new(),
        }
    }

    async fn request<TRequest, TResponse>(
        &self,
        method: hyper::http::Method,
        uri: Uri,
        body: Option<&TRequest>,
    ) -> Result<TResponse, Box<dyn Error>>
    where
        TRequest: serde::Serialize,
        TResponse: serde::de::DeserializeOwned,
    {
        let headers = self
            .configuration
            .user_agent
            .as_ref()
            .map(|user_agent| [(hyper::header::USER_AGENT, user_agent)]);

        let response = request(
            &self.client,
            method,
            uri,
            headers.as_ref().map(|h| -> &[_] { &*h }),
            body,
        )
        .await?;
        Ok(response)
    }
}

#[async_trait::async_trait]
pub trait DockerApi {
    async fn system_info(&self) -> Result<crate::models::SystemInfo, Box<dyn Error>>;

    async fn container_create(
        &self,
        body: crate::models::ContainerCreateBody,
        name: &str,
    ) -> Result<crate::models::InlineResponse201, Box<dyn Error>>;

    async fn container_delete(
        &self,
        id: &str,
        v: bool,
        force: bool,
        link: bool,
    ) -> Result<(), Box<dyn Error>>;

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<crate::models::ContainerSummary>, Box<dyn Error>>;

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<(), Box<dyn Error>>;
    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<(), Box<dyn Error>>;
    async fn container_stats(
        &self,
        id: &str,
        stream: bool,
    ) -> Result<serde_json::Value, Box<dyn Error>>;
    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<(), Box<dyn Error>>;
    async fn container_top(
        &self,
        id: &str,
        ps_args: &str,
    ) -> Result<crate::models::InlineResponse2001, Box<dyn Error>>;

    async fn container_logs(
        &self,
        id: &str,
        follow: bool,
        stdout: bool,
        stderr: bool,
        since: i32,
        until: Option<i32>,
        timestamps: bool,
        tail: &str,
    ) -> Result<hyper::Body, Box<dyn Error>>;
}

#[async_trait::async_trait]
impl DockerApi for DockerApiClient {
    async fn system_info(&self) -> Result<crate::models::SystemInfo, Box<dyn Error>> {
        let method = hyper::Method::GET;
        let uri_str = format!("/info");
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_create(
        &self,
        body: crate::models::ContainerCreateBody,
        name: &str,
    ) -> Result<crate::models::InlineResponse201, Box<dyn Error>> {
        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("name", &name.to_string())
            .finish();
        let uri_str = format!("/containers/create?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, Some(&body)).await
    }

    async fn container_delete(
        &self,
        id: &str,
        v: bool,
        force: bool,
        link: bool,
    ) -> Result<(), Box<dyn Error>> {
        let method = hyper::Method::DELETE;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("v", &v.to_string())
            .append_pair("force", &force.to_string())
            .append_pair("link", &link.to_string())
            .finish();
        let uri_str = format!("/containers/{id}?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<(), Box<dyn Error>> {
        let method = hyper::Method::POST;

        let query = t.map_or(std::borrow::Cow::Borrowed(""), |t| {
            std::borrow::Cow::Owned(
                ::url::form_urlencoded::Serializer::new(String::new())
                    .append_pair("t", &t.to_string())
                    .finish(),
            )
        });
        let uri_str = format!("/containers/{id}/restart?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<crate::models::ContainerSummary>, Box<dyn Error>> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("all", &all.to_string())
            .append_pair("limit", &limit.to_string())
            .append_pair("size", &size.to_string())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/containers/json?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<(), Box<dyn Error>> {
        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("detachKeys", &detach_keys.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/start?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_stats(
        &self,
        id: &str,
        stream: bool,
    ) -> Result<serde_json::Value, Box<dyn Error>> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("stream", &stream.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/stats?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<(), Box<dyn Error>> {
        let method = hyper::Method::POST;

        let query = t.map_or(std::borrow::Cow::Borrowed(""), |t| {
            std::borrow::Cow::Owned(
                ::url::form_urlencoded::Serializer::new(String::new())
                    .append_pair("t", &t.to_string())
                    .finish(),
            )
        });
        let uri_str = format!("/containers/{id}/stop?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_top(
        &self,
        id: &str,
        ps_args: &str,
    ) -> Result<crate::models::InlineResponse2001, Box<dyn Error>> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("ps_args", &ps_args.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/top?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn container_logs(
        &self,
        id: &str,
        follow: bool,
        stdout: bool,
        stderr: bool,
        since: i32,
        until: Option<i32>,
        timestamps: bool,
        tail: &str,
    ) -> Result<hyper::Body, Box<dyn Error>> {
        let method = hyper::Method::GET;

        let query = {
            let mut serializer = ::url::form_urlencoded::Serializer::new(String::new());
            serializer
                .append_pair("follow", &follow.to_string())
                .append_pair("stdout", &stdout.to_string())
                .append_pair("stderr", &stderr.to_string())
                .append_pair("since", &since.to_string())
                .append_pair("timestamps", &timestamps.to_string())
                .append_pair("tail", tail);
            if let Some(until) = until {
                serializer.append_pair("until", &until.to_string());
            }

            serializer.finish()
        };
        let uri_str = format!("/containers/{id}/logs?{}", query, id = id);

        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let req = hyper::Request::builder().method(method).uri(uri);

        let req = if let Some(ref user_agent) = self.configuration.user_agent {
            req.header(hyper::header::USER_AGENT, &**user_agent)
        } else {
            req
        };

        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        let resp = self.client.request(req).await?;
        let (hyper::http::response::Parts { status, .. }, body) = resp.into_parts();
        if status.is_success() {
            Ok(body)
        } else {
            Err(simple_error::SimpleError::new(format!(
                "Bad status code: {}",
                status
            )))?
        }
    }
}
