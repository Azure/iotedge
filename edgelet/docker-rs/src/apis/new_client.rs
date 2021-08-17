use std::borrow::Borrow;
use std::error::Error;
use std::sync::Arc;

use futures::{Future, Stream};
use serde_json;
use typed_headers::{self, mime, HeaderMapExt};

use http_common::{request, Connector};
use hyper::{Body, Client, Uri};

use super::configuration::Configuration;
use super::ApiError;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

#[derive(Clone)]
pub struct DockerApiClient {
    client: Arc<Client<Connector, Body>>,
    configuration: Arc<Configuration>,
}

impl DockerApiClient {
    pub fn new(client: Arc<Client<Connector, Body>>) -> Self {
        Self {
            client,
            configuration: Arc::new(Configuration::new()),
        }
    }

    async fn request<TRequest, TResponse>(
        &self,
        method: hyper::http::Method,
        uri: Uri,
        body: Option<&TRequest>,
    ) -> Result<TResponse>
    where
        TRequest: serde::Serialize,
        TResponse: serde::de::DeserializeOwned,
    {
        self.request_with_headers(method, uri, Vec::new(), body)
            .await
    }

    async fn request_with_headers<TRequest, TResponse>(
        &self,
        method: hyper::http::Method,
        uri: Uri,
        mut headers: Vec<(String, String)>,
        body: Option<&TRequest>,
    ) -> Result<TResponse>
    where
        TRequest: serde::Serialize,
        TResponse: serde::de::DeserializeOwned,
    {
        if let Some(user_agent) = self.configuration.user_agent.as_ref() {
            headers.push((hyper::header::USER_AGENT.to_string(), user_agent.clone()))
        }

        let headers = if !headers.is_empty() {
            Some(headers)
        } else {
            None
        };

        let response = request(
            &self.client,
            method,
            uri,
            headers.as_ref().map(|h| -> &[_] { &*h }), // Convert Option<Vec> in to Option<&[]>
            body,
        )
        .await?;
        Ok(response)
    }
}

#[async_trait::async_trait]
pub trait DockerApi {
    async fn system_info(&self) -> Result<crate::models::SystemInfo>;

    async fn image_create(
        &self,
        from_image: &str,
        from_src: &str,
        repo: &str,
        tag: &str,
        input_image: &str,
        x_registry_auth: &str,
        platform: &str,
    ) -> Result<()>;

    async fn image_delete(
        &self,
        name: &str,
        force: bool,
        noprune: bool,
    ) -> Result<Vec<crate::models::ImageDeleteResponseItem>>;

    async fn container_create(
        &self,
        body: crate::models::ContainerCreateBody,
        name: &str,
    ) -> Result<crate::models::InlineResponse201>;

    async fn container_delete(&self, id: &str, v: bool, force: bool, link: bool) -> Result<()>;

    async fn container_inspect(
        &self,
        id: &str,
        size: bool,
    ) -> Result<crate::models::InlineResponse200>;

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<crate::models::ContainerSummary>>;

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<()>;
    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<()>;
    async fn container_stats(&self, id: &str, stream: bool) -> Result<serde_json::Value>;
    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<()>;
    async fn container_top(
        &self,
        id: &str,
        ps_args: &str,
    ) -> Result<crate::models::InlineResponse2001>;

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
    ) -> Result<hyper::Body>;
}

#[async_trait::async_trait]
impl DockerApi for DockerApiClient {
    async fn system_info(&self) -> Result<crate::models::SystemInfo> {
        let method = hyper::Method::GET;
        let uri_str = format!("/info");
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>).await
    }

    async fn image_create(
        &self,
        from_image: &str,
        from_src: &str,
        repo: &str,
        tag: &str,
        input_image: &str,
        x_registry_auth: &str,
        platform: &str,
    ) -> Result<()> {
        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("fromImage", &from_image.to_string())
            .append_pair("fromSrc", &from_src.to_string())
            .append_pair("repo", &repo.to_string())
            .append_pair("tag", &tag.to_string())
            .append_pair("platform", &platform.to_string())
            .finish();
        let uri_str = format!("/images/create?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let headers = vec![("X-Registry-Auth".to_owned(), x_registry_auth.to_owned())];

        self.request_with_headers(method, uri, headers, Some(&input_image))
            .await
            .map_err(ApiError::with_context("Could not create image."))?;

        Ok(())
    }

    async fn image_delete(
        &self,
        name: &str,
        force: bool,
        noprune: bool,
    ) -> Result<Vec<crate::models::ImageDeleteResponseItem>> {
        let method = hyper::Method::DELETE;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("force", &force.to_string())
            .append_pair("noprune", &noprune.to_string())
            .finish();
        let uri_str = format!("/images/{name}?{}", query, name = name);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result = self
            .request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not delete image."))?;

        Ok(result)
    }

    async fn container_create(
        &self,
        body: crate::models::ContainerCreateBody,
        name: &str,
    ) -> Result<crate::models::InlineResponse201> {
        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("name", &name.to_string())
            .finish();
        let uri_str = format!("/containers/create?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result = self
            .request(method, uri, Some(&body))
            .await
            .map_err(ApiError::with_context("Could not create container."))?;

        Ok(result)
    }

    async fn container_delete(&self, id: &str, v: bool, force: bool, link: bool) -> Result<()> {
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

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<()> {
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

        self.request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not delete container."))?;

        Ok(())
    }

    async fn container_inspect(
        &self,
        id: &str,
        size: bool,
    ) -> Result<crate::models::InlineResponse200> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("size", &size.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/json?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result = self
            .request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not inspect container."))?;

        Ok(result)
    }

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<crate::models::ContainerSummary>> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("all", &all.to_string())
            .append_pair("limit", &limit.to_string())
            .append_pair("size", &size.to_string())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/containers/json?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result = self
            .request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not list containers."))?;

        Ok(result)
    }

    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<()> {
        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("detachKeys", &detach_keys.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/start?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        self.request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not start container."))?;

        Ok(())
    }

    async fn container_stats(&self, id: &str, stream: bool) -> Result<serde_json::Value> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("stream", &stream.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/stats?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result = self
            .request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not collect container stats."))?;

        Ok(result)
    }

    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<()> {
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

        self.request(method, uri, None::<&()>)
            .await
            .map_err(ApiError::with_context("Could not stop container."))?;

        Ok(())
    }

    async fn container_top(
        &self,
        id: &str,
        ps_args: &str,
    ) -> Result<crate::models::InlineResponse2001> {
        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("ps_args", &ps_args.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/top?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;

        let result =
            self.request(method, uri, None::<&()>)
                .await
                .map_err(ApiError::with_context(
                    "Could not list container processes.",
                ))?;

        Ok(result)
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
    ) -> Result<hyper::Body> {
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
            Err(ApiError::with_message(format!(
                "Bad status code: {}",
                status
            )))?
        }
    }
}
