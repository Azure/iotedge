// Copyright (c) Microsoft. All rights reserved.

#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use edgelet_test_utils::clients::KeyClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    module_id: String,
    gen_id: String,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct EncryptRequest {
    plaintext: String,

    #[serde(rename = "initializationVector")]
    iv: String,
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct EncryptResponse {
    ciphertext: String,
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
            regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/genid/(?P<genId>[^/]+)/encrypt$")
                .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let gen_id = &captures["genId"];
        let gen_id = percent_encoding::percent_decode_str(gen_id)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            client: service.key_client.clone(),
            module_id: module_id.into_owned(),
            gen_id: gen_id.into_owned(),
            pid,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = EncryptRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

        let (plaintext, iv) = match body {
            Some(body) => {
                let plaintext = super::base64_decode(body.plaintext)?;
                let iv = super::base64_decode(body.iv)?;

                (plaintext, iv)
            }
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        let aad = format!("{}{}", self.module_id, self.gen_id).into_bytes();
        let parameters = aziot_key_common::EncryptMechanism::Aead { iv, aad };

        let client = self.client.lock().await;
        let key = super::master_encryption_key(&client).await?;

        match client.encrypt(&key, parameters, &plaintext).await {
            Ok(ciphertext) => {
                let ciphertext = base64::encode(ciphertext);

                let res = EncryptResponse { ciphertext };
                let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

                Ok(res)
            }
            Err(err) => Err(edgelet_http::error::server_error(err.to_string())),
        }
    }

    type PutBody = serde::de::IgnoredAny;
}
