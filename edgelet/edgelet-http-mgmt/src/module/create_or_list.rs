// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    pid: libc::pid_t,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct CreateModuleRequest {}

#[derive(Debug, serde::Serialize)]
pub(crate) struct CreateModuleResponse {}

#[derive(Debug, serde::Serialize)]
pub(crate) struct ListModulesResponse {}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        if path != "/modules" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            runtime: service.runtime.clone(),
            pid,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type GetResponse = ListModulesResponse;
    async fn get(self) -> http_common::server::RouteResponse<Self::GetResponse> {
        let runtime = self.runtime.lock().await;

        todo!()
    }

    type PostBody = CreateModuleRequest;
    type PostResponse = CreateModuleResponse;
    async fn post(
        self,
        body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_agent(self.pid, &self.runtime)?;

        let body = match body {
            Some(body) => body,
            None => {
                return Err(edgelet_http::error::bad_request("missing request body"));
            }
        };

        let runtime = self.runtime.lock().await;

        todo!()
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
