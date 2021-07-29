// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    pid: libc::pid_t,
    module: String,
    start: Option<String>,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
    M::Config: serde::Serialize,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<module>[^/]+)$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module = &captures["module"];
        let module = percent_encoding::percent_decode_str(module)
            .decode_utf8()
            .ok()?;

        let start = edgelet_http::find_query("start", query);

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            runtime: service.runtime.clone(),
            pid,
            module: module.into_owned(),
            start,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();
    async fn delete(
        self,
        _body: Option<Self::DeleteBody>,
    ) -> http_common::server::RouteResponse<Option<Self::DeleteResponse>> {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let runtime = self.runtime.lock().await;

        match runtime.remove(&self.module).await {
            Ok(_) => Ok((http::StatusCode::NO_CONTENT, None)),
            Err(err) => Err(edgelet_http::error::server_error(err.to_string())),
        }
    }

    type GetResponse = edgelet_http::ModuleDetails;
    async fn get(self) -> http_common::server::RouteResponse<Self::GetResponse> {
        let runtime = self.runtime.lock().await;

        let module_info = runtime
            .get(&self.module)
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;

        Ok((http::StatusCode::OK, module_info.into()))
    }

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
    async fn put(
        self,
        _body: Self::PutBody,
    ) -> http_common::server::RouteResponse<Self::PutResponse> {
        edgelet_http::auth_agent(self.pid, &self.runtime).await?;

        let start = if let Some(start) = &self.start {
            std::str::FromStr::from_str(start)
                .map_err(|err| edgelet_http::error::bad_request("invalid parameter: start"))?
        } else {
            false
        };

        todo!()
    }
}
