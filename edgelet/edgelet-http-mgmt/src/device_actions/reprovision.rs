// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    reprovision: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
    pid: libc::pid_t,
    _runtime: std::marker::PhantomData<M>,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2019_10_22)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        if path != "/device/reprovision" {
            return None;
        }

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            reprovision: service.reprovision.clone(),
            pid,
            _runtime: std::marker::PhantomData,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type GetResponse = ();

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = ();
    async fn post(
        self,
        _body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_agent(self.pid)?;

        match self
            .reprovision
            .send(edgelet_core::ShutdownReason::Reprovision)
        {
            Ok(()) => Ok((http::StatusCode::OK, None)),
            Err(_) => Err(edgelet_http::error::server_error(
                "failed to send reprovision request",
            )),
        }
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
