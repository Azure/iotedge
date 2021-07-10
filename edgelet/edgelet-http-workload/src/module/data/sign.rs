// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route {
    module_id: String,
    pid: libc::pid_t,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct SignRequest {
    #[serde(rename = "keyId")]
    key_id: String,

    algo: String,
    data: String,
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct SignResponse {
    digest: String,
}

#[async_trait::async_trait]
impl http_common::server::Route for Route {
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service;
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
            module_id: module_id.into_owned(),
            pid,
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
        edgelet_http::auth_caller(&self.module_id, self.pid)?;

        let body = match body {
            Some(body) => body,
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        todo!()
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}
