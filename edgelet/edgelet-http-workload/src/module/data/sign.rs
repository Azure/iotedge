// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    key_client: std::sync::Arc<futures_util::lock::Mutex<aziot_key_client_async::Client>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
    module_id: String,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct SignRequest {
    data: String,
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct SignResponse {
    digest: String,
}

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
        let uri_regex =
            regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/genid/(?P<genId>[^/]+)/sign$")
                .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            key_client: service.key_client.clone(),
            identity_client: service.identity_client.clone(),
            module_id: module_id.into_owned(),
            pid,
            runtime: service.runtime.clone(),
        })
    }

    type GetResponse = ();

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type PostBody = SignRequest;
    type PostResponse = SignResponse;
    async fn post(
        self,
        body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime)?;

        let data = match body {
            Some(body) => super::base64_decode(body.data)?,
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        let module_key = get_module_key(self.identity_client, &self.module_id).await?;

        let key_client = self.key_client.lock().await;

        let digest = key_client
            .sign(
                &module_key,
                aziot_key_common::SignMechanism::HmacSha256,
                &data,
            )
            .await
            .map_err(|err| edgelet_http::error::server_error(format!("failed to sign: {}", err)))?;
        let digest = base64::encode(digest);

        Ok((http::StatusCode::OK, Some(SignResponse { digest })))
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}

async fn get_module_key(
    client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,
    module_id: &str,
) -> Result<aziot_key_common::KeyHandle, http_common::server::Error> {
    let identity = {
        let client = client.lock().await;

        client.get_identity(module_id).await.map_err(|err| {
            edgelet_http::error::server_error(format!(
                "failed to get module identity for {}: {}",
                module_id, err
            ))
        })
    }?;

    let identity = match identity {
        aziot_identity_common::Identity::Aziot(identity) => identity,
        aziot_identity_common::Identity::Local(_) => {
            return Err(edgelet_http::error::server_error("invalid identity type"))
        }
    };

    let auth = identity
        .auth
        .ok_or_else(|| edgelet_http::error::server_error("module identity missing auth"))?;

    auth.key_handle
        .ok_or_else(|| edgelet_http::error::server_error("module identity missing key"))
}
