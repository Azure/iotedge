use std::borrow::Borrow;
use std::error::Error;
use std::sync::Arc;

use futures::{Future, Stream};
use serde_json;

use http_common::{Connector, ErrorBody, HttpRequest};
use hyper::{Body, Client, Uri};

use super::configuration::Configuration;
use super::ApiError;

use crate::models;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

#[derive(Clone)]
pub struct DockerApiClient {
    connector: Connector,
    configuration: Arc<Configuration>,
}

impl DockerApiClient {
    pub fn new(connector: Connector) -> Self {
        Self {
            connector,
            configuration: Arc::new(Configuration::default()),
        }
    }

    pub fn with_configuration(mut self, configuration: Configuration) -> Self {
        self.configuration = Arc::new(configuration);
        self
    }

    fn add_user_agent<TBody>(&self, request: &mut HttpRequest<TBody, Connector>) -> Result<()>
    where
        TBody: serde::Serialize,
    {
        if let Some(user_agent) = &self.configuration.user_agent {
            request.add_header(hyper::header::USER_AGENT, user_agent)?;
        }

        Ok(())
    }
}

#[async_trait::async_trait]
pub trait DockerApi {
    async fn system_info(&self) -> Result<models::SystemInfo>;

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
    ) -> Result<Vec<models::ImageDeleteResponseItem>>;

    async fn container_create(
        &self,
        body: models::ContainerCreateBody,
        name: &str,
    ) -> Result<models::InlineResponse201>;

    async fn container_delete(&self, id: &str, v: bool, force: bool, link: bool) -> Result<()>;

    async fn container_inspect(&self, id: &str, size: bool) -> Result<models::InlineResponse200>;

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<models::ContainerSummary>>;

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<()>;
    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<()>;
    async fn container_stats(&self, id: &str, stream: bool) -> Result<serde_json::Value>;
    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<()>;
    async fn container_top(&self, id: &str, ps_args: &str) -> Result<models::InlineResponse2001>;

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

    async fn network_create(
        &self,
        network_config: models::NetworkConfig,
    ) -> Result<models::InlineResponse2011>;

    async fn network_list(&self, filters: &str) -> Result<Vec<models::Network>>;
}

#[async_trait::async_trait]
impl DockerApi for DockerApiClient {
    async fn system_info(&self) -> Result<models::SystemInfo> {
        let uri_str = format!("/info");
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request.json_response().await?;
        let response = response.parse_expect_ok::<models::SystemInfo, ErrorBody<'_>>()?;

        Ok(response)
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
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("fromImage", &from_image.to_string())
            .append_pair("fromSrc", &from_src.to_string())
            .append_pair("repo", &repo.to_string())
            .append_pair("tag", &tag.to_string())
            .append_pair("platform", &platform.to_string())
            .finish();
        let uri_str = format!("/images/create?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request = HttpRequest::post(self.connector.clone(), &uri, Some(input_image));
        request.add_header(
            hyper::header::HeaderName::from_static("x-registry-auth"),
            x_registry_auth,
        )?;
        self.add_user_agent(&mut request)?;

        let response = request
            .response(true)
            .await
            .map_err(ApiError::with_context("Could not create image."))?;
        let (status, _) = response.into_parts();

        if status == hyper::StatusCode::OK {
            Ok(())
        } else {
            Err(ApiError::with_message(format!("Bad status code: {}", status)).into())
        }
    }

    async fn image_delete(
        &self,
        name: &str,
        force: bool,
        noprune: bool,
    ) -> Result<Vec<models::ImageDeleteResponseItem>> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("force", &force.to_string())
            .append_pair("noprune", &noprune.to_string())
            .finish();
        let uri_str = format!("/images/{name}?{}", query, name = name);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> =
            HttpRequest::delete(self.connector.clone(), &uri, None);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not delete image."))?;
        let response =
            response.parse_expect_ok::<Vec<models::ImageDeleteResponseItem>, ErrorBody<'_>>()?;

        Ok(response)
    }

    async fn container_create(
        &self,
        body: models::ContainerCreateBody,
        name: &str,
    ) -> Result<models::InlineResponse201> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("name", &name.to_string())
            .finish();
        let uri_str = format!("/containers/create?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request = HttpRequest::post(self.connector.clone(), &uri, Some(body));
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not create container."))?;
        let response = response
            .parse::<models::InlineResponse201, ErrorBody<'_>>(&[hyper::StatusCode::CREATED])?;

        Ok(response)
    }

    async fn container_delete(&self, id: &str, v: bool, force: bool, link: bool) -> Result<()> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("v", &v.to_string())
            .append_pair("force", &force.to_string())
            .append_pair("link", &link.to_string())
            .finish();
        let uri_str = format!("/containers/{id}?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> =
            HttpRequest::delete(self.connector.clone(), &uri, None);
        self.add_user_agent(&mut request)?;

        request
            .no_content_response()
            .await
            .map_err(ApiError::with_context("Could not delete container."))?;

        Ok(())
    }

    async fn container_restart(&self, id: &str, t: Option<i32>) -> Result<()> {
        let query = t.map_or(std::borrow::Cow::Borrowed(""), |t| {
            std::borrow::Cow::Owned(
                ::url::form_urlencoded::Serializer::new(String::new())
                    .append_pair("t", &t.to_string())
                    .finish(),
            )
        });
        let uri_str = format!("/containers/{id}/restart?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::post(self.connector.clone(), &uri, None);
        self.add_user_agent(&mut request)?;

        request
            .no_content_response()
            .await
            .map_err(ApiError::with_context("Could not restart container."))?;

        Ok(())
    }

    async fn container_inspect(&self, id: &str, size: bool) -> Result<models::InlineResponse200> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("size", &size.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/json?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not inspect container."))?;
        let response = response.parse_expect_ok::<models::InlineResponse200, ErrorBody<'_>>()?;

        Ok(response)
    }

    async fn container_list(
        &self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &str,
    ) -> Result<Vec<models::ContainerSummary>> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("all", &all.to_string())
            .append_pair("limit", &limit.to_string())
            .append_pair("size", &size.to_string())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/containers/json?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not list containers."))?;
        let response =
            response.parse_expect_ok::<Vec<models::ContainerSummary>, ErrorBody<'_>>()?;

        Ok(response)
    }

    async fn container_start(&self, id: &str, detach_keys: &str) -> Result<()> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("detachKeys", &detach_keys.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/start?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::post(self.connector.clone(), &uri, None);
        self.add_user_agent(&mut request)?;

        let response = request
            .response(false)
            .await
            .map_err(ApiError::with_context("Could not start container."))?;
        let (status, _) = response.into_parts();

        if status == hyper::StatusCode::NO_CONTENT || status == hyper::StatusCode::NOT_MODIFIED {
            Ok(())
        } else {
            Err(ApiError::with_message(format!("Bad status code: {}", status)).into())
        }
    }

    async fn container_stats(&self, id: &str, stream: bool) -> Result<serde_json::Value> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("stream", &stream.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/stats?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not collect container stats."))?;
        let response = response.parse_expect_ok::<serde_json::Value, ErrorBody<'_>>()?;

        Ok(response)
    }

    async fn container_stop(&self, id: &str, t: Option<i32>) -> Result<()> {
        let query = t.map_or(std::borrow::Cow::Borrowed(""), |t| {
            std::borrow::Cow::Owned(
                ::url::form_urlencoded::Serializer::new(String::new())
                    .append_pair("t", &t.to_string())
                    .finish(),
            )
        });
        let uri_str = format!("/containers/{id}/stop?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::post(self.connector.clone(), &uri, None);
        self.add_user_agent(&mut request)?;

        let response = request
            .response(false)
            .await
            .map_err(ApiError::with_context("Could not stop container."))?;
        let (status, _) = response.into_parts();

        if status == hyper::StatusCode::NO_CONTENT || status == hyper::StatusCode::NOT_MODIFIED {
            Ok(())
        } else {
            Err(ApiError::with_message(format!("Bad status code: {}", status)).into())
        }
    }

    async fn container_top(&self, id: &str, ps_args: &str) -> Result<models::InlineResponse2001> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("ps_args", &ps_args.to_string())
            .finish();
        let uri_str = format!("/containers/{id}/top?{}", query, id = id);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context(
                "Could not list container processes.",
            ))?;
        let response = response.parse_expect_ok::<models::InlineResponse2001, ErrorBody<'_>>()?;

        Ok(response)
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

        let mut req = hyper::Request::builder()
            .method(hyper::Method::GET)
            .uri(uri);

        let headers = req.headers_mut().expect("new request is invalid");

        if let Some(user_agent) = &self.configuration.user_agent {
            let user_agent = hyper::header::HeaderValue::from_str(user_agent)
                .map_err(|_| ApiError::with_message("Invalid user agent"))?;

            headers.insert(hyper::header::USER_AGENT, user_agent);
        };

        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        let client = self.connector.clone().into_client();
        let resp = client.request(req).await?;
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

    async fn network_create(
        &self,
        network_config: models::NetworkConfig,
    ) -> Result<models::InlineResponse2011> {
        let uri_str = format!("/networks/create");
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request = HttpRequest::post(self.connector.clone(), &uri, Some(network_config));
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not create network."))?;

        let response = response
            .parse::<models::InlineResponse2011, ErrorBody<'_>>(&[hyper::StatusCode::CREATED])?;

        Ok(response)
    }

    async fn network_list(&self, filters: &str) -> Result<Vec<models::Network>> {
        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/networks?{}", query);
        let uri = (self.configuration.uri_composer)(&self.configuration.base_path, &uri_str)?;
        let uri = uri.to_string();

        let mut request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);
        self.add_user_agent(&mut request)?;

        let response = request
            .json_response()
            .await
            .map_err(ApiError::with_context("Could not list networks."))?;
        let response = response.parse_expect_ok::<Vec<models::Network>, ErrorBody<'_>>()?;

        Ok(response)
    }
}
