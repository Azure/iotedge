// Copyright (c) Microsoft. All rights reserved.

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;
#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use test_common::client::IdentityClient;
#[cfg(test)]
use test_common::client::KeyClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
    module_id: String,
    pid: libc::pid_t,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct SignRequest {
    data: String,
}

#[derive(Debug, serde::Serialize)]
#[cfg_attr(test, derive(serde::Deserialize))]
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

        let pid = match extensions.get::<Option<libc::pid_t>>().copied().flatten() {
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

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = SignRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

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
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;
        let digest = base64::encode(digest);

        let res = SignResponse { digest };
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PutBody = serde::de::IgnoredAny;
}

async fn get_module_key(
    client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
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

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule/genid/1/sign";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!("testModule", &route.module_id);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/modules//genid/1/sign");

        // Missing generation ID
        test_route_err!("/modules/testModule/genid//sign");

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
            let body = super::SignRequest {
                data: base64::encode("data"),
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

        // data must be base64-encoded
        let body = super::SignRequest {
            data: "~".to_string(),
        };

        let route = test_route_ok!(TEST_PATH);
        let response = route.post(Some(body)).await.unwrap_err();
        assert_eq!(hyper::StatusCode::BAD_REQUEST, response.status_code);

        // Response digest is base64-encoded
        let body = super::SignRequest {
            data: base64::encode("~"),
        };

        let route = test_route_ok!(TEST_PATH);
        let response = route.post(Some(body)).await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let response: super::SignResponse = serde_json::from_slice(&body).unwrap();
        base64::decode(response.digest).unwrap();
    }

    #[tokio::test]
    async fn get_module_key() {
        // Identity doesn't exist: fail
        let client = super::IdentityClient::default();
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        let response = super::get_module_key(client, "invalid").await.unwrap_err();
        assert_eq!(
            hyper::StatusCode::INTERNAL_SERVER_ERROR,
            response.status_code
        );

        // Invalid Identity type: fail
        let identity = aziot_identity_common::Identity::Local(aziot_identity_common::LocalIdSpec {
            module_id: "testModule".to_string(),
            auth: aziot_identity_common::LocalAuthenticationInfo {
                private_key: "key".to_string(),
                certificate: "certificate".to_string(),
                expiration: "expiration".to_string(),
            },
        });

        let client = super::IdentityClient::default();

        {
            let identities = client.identities.lock().await;

            identities.replace_with(|identities| {
                identities.remove("testModule");

                assert!(identities
                    .insert("testModule".to_string(), identity)
                    .is_none());

                identities.to_owned()
            });
        }
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        let response = super::get_module_key(client, "testModule")
            .await
            .unwrap_err();
        assert_eq!(
            hyper::StatusCode::INTERNAL_SERVER_ERROR,
            response.status_code
        );

        // Identity missing auth: fail
        let client = super::IdentityClient::default();

        {
            let identities = client.identities.lock().await;

            identities.replace_with(|identities| {
                let mut identity = match identities.remove("testModule").unwrap() {
                    aziot_identity_common::Identity::Aziot(identity) => identity,
                    _ => panic!(),
                };

                identity.auth = None;
                let identity = aziot_identity_common::Identity::Aziot(identity);

                assert!(identities
                    .insert("testModule".to_string(), identity)
                    .is_none());

                identities.to_owned()
            });
        }
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        let response = super::get_module_key(client, "testModule")
            .await
            .unwrap_err();
        assert_eq!(
            hyper::StatusCode::INTERNAL_SERVER_ERROR,
            response.status_code
        );

        // Identity missing key: fail
        let client = super::IdentityClient::default();

        {
            let identities = client.identities.lock().await;

            identities.replace_with(|identities| {
                let mut identity = match identities.remove("testModule").unwrap() {
                    aziot_identity_common::Identity::Aziot(identity) => identity,
                    _ => panic!(),
                };

                let mut auth = identity.auth.clone().unwrap();
                auth.key_handle = None;
                identity.auth = Some(auth);

                let identity = aziot_identity_common::Identity::Aziot(identity);

                assert!(identities
                    .insert("testModule".to_string(), identity)
                    .is_none());

                identities.to_owned()
            });
        }
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        let response = super::get_module_key(client, "testModule")
            .await
            .unwrap_err();
        assert_eq!(
            hyper::StatusCode::INTERNAL_SERVER_ERROR,
            response.status_code
        );

        // Valid identity: succeed
        let client = super::IdentityClient::default();
        let client = std::sync::Arc::new(futures_util::lock::Mutex::new(client));

        let response = super::get_module_key(client, "testModule").await.unwrap();
        assert_eq!("testModule-key".to_string(), response.0);
    }
}
