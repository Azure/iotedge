// Copyright (c) Microsoft. All rights reserved.

use std::io::Read;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<tokio::sync::Mutex<M>>,

    since: Option<String>,
    until: Option<String>,
    iothub_hostname: Option<String>,
    edge_only: Option<String>,
}

const PATH: &str = "/systeminfo/supportbundle";

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2020_07_07)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        if path != PATH {
            return None;
        }

        let since = edgelet_http::find_query("since", query);
        let until = edgelet_http::find_query("until", query);
        let iothub_hostname = edgelet_http::find_query("iothub_hostname", query);
        let edge_only = edgelet_http::find_query("edge_runtime_only", query);

        Some(Route {
            runtime: service.runtime.clone(),

            since,
            until,
            iothub_hostname,
            edge_only,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let log_options = self.log_options()?;

        let edge_only = if let Some(edge_only) = &self.edge_only {
            std::str::FromStr::from_str(edge_only).map_err(|_| {
                edgelet_http::error::bad_request("invalid parameter: edge_runtime_only")
            })?
        } else {
            false
        };

        let (support_bundle, bundle_size) = {
            let runtime = self.runtime.lock().await;

            support_bundle::make_bundle(
                support_bundle::OutputLocation::Memory,
                log_options,
                edge_only,
                false,
                self.iothub_hostname,
                &(*runtime),
            )
            .await
            .map_err(edgelet_http::error::server_error)
        }?;

        let bundle_size = usize::try_from(bundle_size)
            .map_err(|_| edgelet_http::error::bad_request("invalid parameter: bundle size"))?;

        let support_bundle = ReadStream(support_bundle);

        let res =
            http_common::server::response::zip(hyper::StatusCode::OK, bundle_size, support_bundle);
        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
}

impl<M> Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    fn log_options(&self) -> Result<edgelet_core::LogOptions, http_common::server::Error> {
        let mut log_options = edgelet_core::LogOptions::new();

        if let Some(since) = &self.since {
            let since = edgelet_core::parse_since(since)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: since"))?;

            log_options = log_options.with_since(since);
        }

        if let Some(until) = &self.until {
            let until = edgelet_core::parse_since(until)
                .map_err(|_| edgelet_http::error::bad_request("invalid parameter: until"))?;

            log_options = log_options.with_until(until);
        }

        Ok(log_options)
    }
}

struct ReadStream(Box<dyn Read + Send>);

impl futures_util::stream::Stream for ReadStream {
    type Item = Result<Vec<u8>, std::io::Error>;

    fn poll_next(
        mut self: std::pin::Pin<&mut Self>,
        _cx: &mut futures_util::task::Context<'_>,
    ) -> futures_util::task::Poll<Option<Self::Item>> {
        let mut buf: Vec<u8> = vec![0; 1024];

        let bytes_read = self.as_mut().0.read(&mut buf)?;

        let res = if bytes_read > 0 {
            buf.resize(bytes_read, 0);

            Some(Ok(buf))
        } else {
            None
        };

        futures_util::task::Poll::Ready(res)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        test_route_ok!(super::PATH);

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", super::PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", super::PATH));
    }
}
