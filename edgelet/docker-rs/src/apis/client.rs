use serde::Deserialize;

use super::configuration::Configuration;
use crate::models;

type BoxFutureResult<'a, T> =
    std::pin::Pin<Box<dyn std::future::Future<Output = anyhow::Result<T>> + Send + 'a>>;

#[derive(Debug, serde_derive::Deserialize)]
#[cfg_attr(test, derive(PartialEq))]
pub struct ApiError {
    #[serde(deserialize_with = "try_from_u16")]
    pub code: hyper::StatusCode,
    pub message: String,
}

impl std::fmt::Display for ApiError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "HTTP {}: {}", &self.code, &self.message)
    }
}

impl std::error::Error for ApiError {}

fn try_from_u16<'de, D>(de: D) -> Result<hyper::StatusCode, D::Error>
where
    D: serde::de::Deserializer<'de>,
{
    let code = u16::deserialize(de)?;
    hyper::StatusCode::try_from(code).map_err(<D::Error as serde::de::Error>::custom)
}

impl ApiError {
    async fn try_from_response(value: hyper::Response<hyper::Body>) -> anyhow::Result<Self> {
        let (parts, body) = value.into_parts();
        let error_bytes = hyper::body::to_bytes(body).await?;
        let error_str = String::from_utf8(error_bytes.to_vec())?;
        Ok(Self {
            code: parts.status,
            message: if let Ok(mut obj) =
                serde_json::from_str::<serde_json::Map<String, serde_json::Value>>(&error_str)
            {
                if let Some(serde_json::Value::String(message)) = obj.remove("message") {
                    message
                } else {
                    error_str
                }
            } else {
                error_str
            },
        })
    }
}

#[derive(Clone)]
pub struct DockerApiClient<C> {
    client: hyper::Client<C>,
    configuration: std::sync::Arc<Configuration>,
}

impl<C> DockerApiClient<C>
where
    C: Clone + hyper::client::connect::Connect + Send + Sync + 'static,
{
    pub fn new(connector: C) -> Self {
        Self {
            client: hyper::Client::builder().build(connector),
            configuration: std::sync::Arc::new(Configuration::default()),
        }
    }

    pub fn with_configuration(mut self, configuration: Configuration) -> Self {
        self.configuration = std::sync::Arc::new(configuration);
        self
    }
}

pub trait DockerApi {
    fn system_info(&self) -> BoxFutureResult<'_, models::SystemInfo>;

    fn image_create<'a>(
        &'a self,
        from_image: &'a str,
        from_src: &'a str,
        repo: &'a str,
        tag: &'a str,
        input_image: &'a str,
        x_registry_auth: &'a str,
        platform: &'a str,
    ) -> BoxFutureResult<'a, ()>;

    fn images_list<'a>(
        &'a self,
        all: bool,
        filters: &'a str,
        digests: bool,
    ) -> BoxFutureResult<'a, Vec<models::ImageSummary>>;

    fn image_delete<'a>(
        &'a self,
        name: &'a str,
        force: bool,
        no_prune: bool,
    ) -> BoxFutureResult<'a, Vec<models::ImageDeleteResponseItem>>;

    fn container_create<'a>(
        &'a self,
        name: &'a str,
        body: models::ContainerCreateBody,
    ) -> BoxFutureResult<'a, models::InlineResponse201>;

    fn container_delete<'a>(
        &'a self,
        id: &'a str,
        verbose: bool,
        force: bool,
        link: bool,
    ) -> BoxFutureResult<'a, ()>;

    fn container_inspect<'a>(
        &'a self,
        id: &'a str,
        size: bool,
    ) -> BoxFutureResult<'a, models::InlineResponse200>;

    fn container_list<'a>(
        &'a self,
        all: bool,
        limit: i32,
        size: bool,
        filters: &'a str,
    ) -> BoxFutureResult<'a, Vec<models::ContainerSummary>>;

    fn container_restart<'a>(
        &'a self,
        id: &'a str,
        timeout: Option<i32>,
    ) -> BoxFutureResult<'a, ()>;
    fn container_start<'a>(&'a self, id: &'a str, detach_keys: &'a str) -> BoxFutureResult<'a, ()>;
    fn container_stats<'a>(
        &'a self,
        id: &'a str,
        stream: bool,
    ) -> BoxFutureResult<'a, serde_json::Value>;
    fn container_stop<'a>(&'a self, id: &'a str, timeout: Option<i32>) -> BoxFutureResult<'a, ()>;
    fn container_top<'a>(
        &'a self,
        id: &'a str,
        ps_args: &'a str,
    ) -> BoxFutureResult<'a, models::InlineResponse2001>;

    fn container_logs<'a>(
        &'a self,
        id: &'a str,
        follow: bool,
        stdout: bool,
        stderr: bool,
        since: i32,
        until: Option<i32>,
        timestamps: bool,
        tail: &'a str,
    ) -> BoxFutureResult<'a, hyper::Body>;

    fn network_create(
        &self,
        network_config: models::NetworkConfig,
    ) -> BoxFutureResult<'_, models::InlineResponse2011>;

    fn network_list<'a>(&'a self, filters: &'a str) -> BoxFutureResult<'a, Vec<models::Network>>;
}

macro_rules! api_call {
    (@inner output_type) => { () };
    (@inner output_type $output:ty) => { $output };

    (@inner maybe_output $response:ident ;) => { Ok(()) };
    (@inner maybe_output $response:ident ; $output:ty) => {{
        let (parts, body) = $response.into_parts();
        ::anyhow::ensure!(
            parts.headers.get(::hyper::header::CONTENT_TYPE)
                .ok_or_else(|| ::anyhow::anyhow!("expected Content-Type"))?
                .to_str()?
                .contains("application/json"),
            "expected JSON Content-Type"
        );
        let response_bytes = ::hyper::body::to_bytes(body).await?;
        Ok(::serde_json::from_slice::<$output>(&response_bytes)?)
    }};
    (@inner maybe_output $response:ident => $transfer:ident $blk:block ; $($_output:ty)?) => {{
        let $transfer = $response;
        $blk
    }};

    (@inner build_request $builder:ident) => { $builder.body(::hyper::Body::empty()) };
    (@inner build_request $builder:ident $body:ident $_:ty) => {
        $builder
            .header(::hyper::header::CONTENT_TYPE, "application/json")
            .body(::hyper::Body::from(::serde_json::to_string(&$body)?))
    };

    (@inner query $param:ident &$($_:lifetime)? str) => { $param };
    (@inner query $param:ident Option<$type:ty>) => {
        &$param.as_ref().map(ToString::to_string).unwrap_or_default()
    };
    (@inner query $param:ident $type:ty) => { &$param.to_string() };

    (
        $name:ident : $method:ident $path:literal $(-> $output:ty)? ;
        $(path : [
            $($pparam:ident : $ptype:ty),*
        ] ;)?
        $(query : [
            $($qname:literal = ( $qparam:ident : $($qtype:tt)* ) ),*
        ] ;)?
        $(header : [
            $($hname:literal = ( $hparam:ident : $htype:ty ) ),*
        ] ;)?
        $(body : $btype:ty ;)?
        ok : [ $($code:ident),* ]
        $(; and_then($transfer:ident) : $blk:block)?
    ) => {
        fn $name<'a>(
            &'a self,
            $($($pparam : $ptype,)*)?
            $($($qparam : $($qtype)*,)*)?
            $($($hparam : $htype,)*)?
            $(body : $btype,)?
        ) -> ::std::pin::Pin<Box<dyn ::std::future::Future<Output = ::anyhow::Result<api_call!(@inner output_type $($output)?)>> + Send + 'a>> {
            const OK: &[::hyper::StatusCode] = &[$(::hyper::StatusCode::$code),*];

            Box::pin(async move {
                let query = ::url::form_urlencoded::Serializer::new(String::new())
                    $($(
                        .append_pair($qname, api_call!(@inner query $qparam $($qtype)*))
                    )*)?
                    .finish();
                let uri = (self.configuration.uri_composer)(
                    &self.configuration.base_path,
                    &format!("{}?{}", format_args!($path), query)
                )?;

                let mut builder = ::hyper::Request::$method(&uri)
                    $($(
                        .header(::hyper::header::HeaderName::from_static($hname), $hparam)
                    )*)?;
                if let Some(agent) = &self.configuration.user_agent {
                    builder = builder.header(::hyper::header::USER_AGENT, agent);
                }
                let request = api_call!(@inner build_request builder $(body $btype)?)?;

                let response = ::tokio::time::timeout(
                    ::std::time::Duration::from_secs(30),
                    self.client.request(request)
                )
                .await??;

                if OK.contains(&response.status()) {
                    api_call!(@inner maybe_output response $(=> $transfer $blk)? ; $($output)?)
                } else {
                    Err(anyhow::anyhow!(ApiError::try_from_response(response).await?))
                }
            })
        }
    }
}

impl<C> DockerApi for DockerApiClient<C>
where
    C: Clone + hyper::client::connect::Connect + Send + Sync + 'static,
{
    api_call! {
        system_info : get "/info" -> models::SystemInfo ;
        ok : [OK]
    }

    api_call! {
        image_delete : delete "/images/{name}" -> Vec<models::ImageDeleteResponseItem> ;
        path : [ name: &'a str ] ;
        query : [ "force" = (force: bool), "noprune" = (no_prune: bool)] ;
        ok : [OK]
    }

    api_call! {
        images_list : get "/images/json" -> Vec<models::ImageSummary> ;
        query : [ "all" = (all: bool), "filters" = (filters: &'a str), "digests" = (digests: bool)] ;
        ok : [OK]
    }

    api_call! {
        container_create : post "/containers/create" -> models::InlineResponse201 ;
        query : [ "name" = (name: &'a str) ] ;
        body : models::ContainerCreateBody ;
        ok : [CREATED]
    }

    api_call! {
        container_delete : delete "/containers/{id}" ;
        path : [ id: &'a str ] ;
        query : [ "v" = (verbose: bool), "force" = (force: bool), "link" = (link: bool) ] ;
        ok : [NO_CONTENT]
    }

    api_call! {
        container_restart : post "/containers/{id}/restart" ;
        path : [ id: &'a str ] ;
        query : [ "t" = (timeout: Option<i32>) ] ;
        ok : [NO_CONTENT]
    }

    api_call! {
        container_inspect : get "/containers/{id}/json" -> models::InlineResponse200 ;
        path : [ id: &'a str ] ;
        query : [ "size" = (size: bool) ] ;
        ok : [OK]
    }

    api_call! {
        container_list : get "/containers/json" -> Vec<models::ContainerSummary> ;
        query : [
            "all" = (all: bool),
            "limit" = (limit: i32),
            "size" = (size: bool),
            "filters" = (filters: &'a str)
        ] ;
        ok : [OK]
    }

    api_call! {
        container_start : post "/containers/{id}/start" ;
        path : [ id: &'a str ] ;
        query : [ "detachKeys" = (detach_keys: &'a str) ] ;
        ok : [NO_CONTENT, NOT_MODIFIED]
    }

    api_call! {
        container_stats : get "/containers/{id}/stats" -> serde_json::Value ;
        path : [ id: &'a str ] ;
        query : [ "stream" = (stream: bool) ] ;
        ok : [OK]
    }

    api_call! {
        container_stop : post "/containers/{id}/stop" ;
        path : [ id: &'a str ] ;
        query : [ "t" = (timeout: Option<i32>) ] ;
        ok : [NO_CONTENT, NOT_MODIFIED]
    }

    api_call! {
        container_top : get "/containers/{id}/top" -> models::InlineResponse2001 ;
        path : [ id: &'a str ] ;
        query : [ "ps_args" = (ps_args: &'a str) ] ;
        ok : [OK]
    }

    api_call! {
        network_create : post "/networks/create" -> models::InlineResponse2011 ;
        body : models::NetworkConfig ;
        ok : [CREATED]
    }

    api_call! {
        network_list : get "/networks" -> Vec<models::Network> ;
        query : [ "filters" = (filters: &'a str) ] ;
        ok : [OK]
    }

    api_call! {
        image_create : post "/images/create" ;
        query : [
            "fromImage" = (from_image: &'a str),
            "fromSrc" = (from_src: &'a str),
            "repo" = (repo: &'a str),
            "tag" = (tag: &'a str),
            "platform" = (platform: &'a str)
        ] ;
        header : [
            "x-registry-auth" = (x_registry_auth: &'a str)
        ] ;
        body : &'a str ;
        ok : [OK] ;
        and_then(response) : {
            let (parts, body) = response.into_parts();

            anyhow::ensure!(
                parts.headers.get(hyper::header::CONTENT_TYPE)
                    .ok_or_else(|| anyhow::anyhow!("expected Content-Type"))?
                    .to_str()?
                    .contains("application/json"),
                "expected JSON Content-Type"
            );

            let response_bytes = hyper::body::to_bytes(body).await?;
            let mut last = serde_json::Deserializer::from_slice(&response_bytes)
                .into_iter::<serde_json::Map<String, serde_json::Value>>()
                .last()
                .ok_or_else(|| {
                    anyhow::anyhow!("received empty response from container runtime")
                })??;

            if let Some(detail) = last.remove("errorDetail") {
                let fallback_msg = serde_json::to_string(&detail)?;
                Err(anyhow::anyhow!(
                    serde_json::from_value(detail)
                        .unwrap_or(ApiError {
                            code: hyper::StatusCode::INTERNAL_SERVER_ERROR,
                            message: fallback_msg
                        })
                ))
            } else {
                Ok(())
            }
        }
    }

    api_call! {
        container_logs : get "/containers/{id}/logs" -> hyper::Body ;
        path : [ id: &'a str ] ;
        query : [
            "follow" = (follow: bool),
            "stdout" = (stdout: bool),
            "stderr" = (stderr: bool),
            "since" = (since: i32),
            "until" = (until: Option<i32>),
            "timestamps" = (timestamps: bool),
            "tail" = (tail: &'a str)
        ] ;
        ok : [OK] ;
        and_then(response) : { Ok(response.into_body()) }
    }
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::JsonConnector;

    use super::{ApiError, DockerApi, DockerApiClient};

    #[tokio::test]
    async fn image_create_stream_ok() {
        let payload = format!(
            "{}{}",
            serde_json::to_string(&serde_json::json!({"status":"STATUS"})).unwrap(),
            serde_json::to_string(&serde_json::json!({"status":"STATUS"})).unwrap(),
        );
        let client = DockerApiClient::new(JsonConnector::ok(&payload));
        assert!(client
            .image_create("", "", "", "", "", "", "")
            .await
            .is_ok());
    }

    #[tokio::test]
    async fn image_create_stream_error() {
        let payload = format!(
            "{}{}",
            serde_json::to_string(&serde_json::json!({"status":"STATUS"})).unwrap(),
            serde_json::to_string(
                &serde_json::json!({"errorDetail":{"code":418,"message":"MESSAGE"}})
            )
            .unwrap()
        );
        let client = DockerApiClient::new(JsonConnector::ok(&payload));
        assert_eq!(
            client
                .image_create("", "", "", "", "", "", "")
                .await
                .unwrap_err()
                .downcast::<ApiError>()
                .unwrap(),
            ApiError {
                code: hyper::StatusCode::IM_A_TEAPOT,
                message: "MESSAGE".to_owned()
            }
        );
    }

    #[tokio::test]
    async fn image_create_stream_error_unrecognized_structure() {
        let payload = format!(
            "{}{}",
            serde_json::to_string(&serde_json::json!({"status":"STATUS"})).unwrap(),
            serde_json::to_string(
                &serde_json::json!({"errorDetail":{"code":"NOT U16","foo":"bar"}})
            )
            .unwrap()
        );
        let client = DockerApiClient::new(JsonConnector::ok(&payload));
        assert_eq!(
            client
                .image_create("", "", "", "", "", "", "")
                .await
                .unwrap_err()
                .downcast::<ApiError>()
                .unwrap(),
            ApiError {
                code: hyper::StatusCode::INTERNAL_SERVER_ERROR,
                message: r#"{"code":"NOT U16","foo":"bar"}"#.to_owned()
            }
        );
    }

    #[tokio::test]
    async fn images_list_null_repo_tags() {
        let payload = format!(
            "[{}]",
            serde_json::to_string(&serde_json::json!(
            {
                "Containers": -1,
                "Created": 1654129518,
                "Id": "sha256:f9a33e4c293fec36a69475f48c2f3fb9dc4db9970befb7296ce52551254c42df",
                "Labels": null,
                "ParentId": "",
                "RepoDigests": [
                  "mcr.microsoft.com/azureiotedge-agent@sha256:7bdfb97647005b697259434bffcd1584ea2610210109012608029f7c7ab0fdcd"
                ],
                "RepoTags": null,
                "SharedSize": -1,
                "Size": 180383211,
                "VirtualSize": 180383211
              }))
            .unwrap()
        );
        let client = DockerApiClient::new(JsonConnector::ok(&payload));
        assert!(client.images_list(false, "", false).await.is_ok());
    }

    #[tokio::test]
    async fn container_inspect_not_found() {
        let payload = serde_json::to_string(&serde_json::json!({
            "message": "MESSAGE",
        }))
        .unwrap();
        let client = DockerApiClient::new(JsonConnector::not_found(&payload));
        assert_eq!(
            client
                .container_inspect("foo", false)
                .await
                .unwrap_err()
                .downcast::<ApiError>()
                .unwrap(),
            ApiError {
                code: hyper::StatusCode::NOT_FOUND,
                message: "MESSAGE".to_owned()
            }
        );
    }
}
