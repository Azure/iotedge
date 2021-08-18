// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryInto;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    pid: libc::pid_t,
    module: String,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime<Config = edgelet_settings::DockerConfig> + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2019_01_30)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<module>[^/]+)/prepareupdate$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module = &captures["module"];
        let module = percent_encoding::percent_decode_str(module)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            runtime: service.runtime.clone(),
            pid,
            module: module.into_owned(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = edgelet_http::ModuleSpec;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let body = match body {
            Some(body) => body,
            None => {
                return Err(edgelet_http::error::bad_request("missing request body"));
            }
        };

        if body.name() != &self.module {
            return Err(edgelet_http::error::bad_request(
                "module name in spec does not match URI",
            ));
        }

        let runtime = self.runtime.lock().await;

        let module: edgelet_http::DockerSpec = body
            .try_into()
            .map_err(|err| edgelet_http::error::server_error(err))?;

        super::pull_image(&*runtime, &module).await?;

        Ok(http_common::server::response::no_content())
    }

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule/prepareupdate";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", TEST_PATH));
    }
}
