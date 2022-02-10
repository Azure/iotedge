// Copyright (c) Microsoft. All rights reserved.

#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use test_common::client::KeyClient;

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
#[cfg_attr(test, derive(serde::Deserialize))]
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

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule/genid/1/encrypt";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!("testModule", &route.module_id);
        assert_eq!("1", &route.gen_id);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/modules//genid/1/encrypt");

        // Missing generation ID
        test_route_err!("/modules/testModule/genid//encrypt");

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", TEST_PATH));
    }

    #[tokio::test]
    async fn auth() {
        async fn post(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            let body = super::EncryptRequest {
                plaintext: base64::encode("plaintext"),
                iv: base64::encode("iv"),
            };

            route.post(Some(body)).await
        }

        edgelet_test_utils::test_auth_caller!(TEST_PATH, "testModule", post);
    }

    #[tokio::test]
    async fn encoding() {
        // Body is required
        let route = test_route_ok!(TEST_PATH);
        let response = route.post(None).await.unwrap_err();
        assert_eq!(hyper::StatusCode::BAD_REQUEST, response.status_code);

        // plaintext must be base64-encoded
        let body = super::EncryptRequest {
            plaintext: "~".to_string(),
            iv: base64::encode("~"),
        };

        let route = test_route_ok!(TEST_PATH);
        let response = route.post(Some(body)).await.unwrap_err();
        assert_eq!(hyper::StatusCode::BAD_REQUEST, response.status_code);

        // iv must be base64-encoded
        let body = super::EncryptRequest {
            plaintext: base64::encode("~"),
            iv: "~".to_string(),
        };

        let route = test_route_ok!(TEST_PATH);
        let response = route.post(Some(body)).await.unwrap_err();
        assert_eq!(hyper::StatusCode::BAD_REQUEST, response.status_code);

        // Response ciphertext is base64-encoded
        let body = super::EncryptRequest {
            plaintext: base64::encode("~"),
            iv: base64::encode("~"),
        };

        let route = test_route_ok!(TEST_PATH);
        let response = route.post(Some(body)).await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let response: super::EncryptResponse = serde_json::from_slice(&body).unwrap();
        base64::decode(response.ciphertext).unwrap();
    }
}
