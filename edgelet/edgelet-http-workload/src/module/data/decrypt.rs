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
pub(crate) struct DecryptRequest {
    ciphertext: String,

    #[serde(rename = "initializationVector")]
    iv: String,
}

#[derive(Debug, serde::Serialize)]
pub(crate) struct DecryptResponse {
    plaintext: String,
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
            regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/genid/(?P<genId>[^/]+)/decrypt$")
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

    type PostBody = DecryptRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

        let (ciphertext, iv) = match body {
            Some(body) => {
                let ciphertext = super::base64_decode(body.ciphertext)?;
                let iv = super::base64_decode(body.iv)?;

                (ciphertext, iv)
            }
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        let aad = format!("{}{}", self.module_id, self.gen_id).into_bytes();
        let parameters = aziot_key_common::EncryptMechanism::Aead { iv, aad };

        let client = self.client.lock().await;
        let key = super::master_encryption_key(&client).await?;

        match client.decrypt(&key, parameters, &ciphertext).await {
            Ok(plaintext) => {
                let plaintext = base64::encode(plaintext);

                let res = DecryptResponse { plaintext };
                let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

                Ok(res)
            }
            Err(err) => Err(edgelet_http::error::server_error(err.to_string())),
        }
    }

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!("/modules/testModule/genid/1/decrypt");
        assert_eq!("testModule", &route.module_id);
        assert_eq!("1", &route.gen_id);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/modules//genid/1/decrypt");

        // Missing generation ID
        test_route_err!("/modules/testModule/genid//decrypt");

        // Extra character at beginning of URI
        test_route_err!("a/modules/testModule/genid/1/decrypt");

        // Extra character at end of URI
        test_route_err!("/modules/testModule/genid/1/decrypta");
    }

    #[tokio::test]
    async fn auth() {
        async fn post(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            let body = super::DecryptRequest {
                ciphertext: base64::encode("ciphertext"),
                iv: base64::encode("iv"),
            };

            route.post(Some(body)).await
        }

        edgelet_test_utils::test_auth_caller!(
            "/modules/testModule/genid/1/decrypt",
            "testModule",
            post
        );
    }
}
