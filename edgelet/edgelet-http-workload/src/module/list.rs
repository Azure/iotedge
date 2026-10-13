// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    runtime: std::sync::Arc<tokio::sync::Mutex<M>>,
}

const PATH: &str = "/modules";

// Module settings fields that are safe to return to any caller of the workload API.
// Other fields (such as createOptions and its labels) may contain another module's
// deployment configuration, so they are only available through the management API.
const ALLOWED_SETTINGS: &[&str] = &["image", "imageHash"];

fn redact_settings(settings: serde_json::Value) -> serde_json::Value {
    let mut redacted = serde_json::Map::new();

    if let serde_json::Value::Object(mut settings) = settings {
        for key in ALLOWED_SETTINGS {
            if let Some(value) = settings.remove(*key) {
                redacted.insert((*key).to_string(), value);
            }
        }
    }

    serde_json::Value::Object(redacted)
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
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        // A bug in certain versions of the diagnostics image causes it to make requests to "/modules/"
        // instead of "/modules". To maintain compatibility with these versions of diagnostics, this API
        // will allow both endpoints.
        if path != PATH && path != "/modules/" {
            return None;
        }

        Some(Route {
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let runtime = self.runtime.lock().await;

        let modules = runtime
            .list_with_details()
            .await
            .map_err(edgelet_http::error::server_error)?;

        let mut res: edgelet_http::ListModulesResponse = modules.into();

        for module in &mut res.modules {
            let settings = std::mem::take(&mut module.config.settings);
            module.config.settings = redact_settings(settings);
        }

        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use http_body_util::BodyExt as _;

    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URI
        test_route_ok!(super::PATH);

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", super::PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", super::PATH));
    }

    #[test]
    fn redact_settings() {
        let settings = serde_json::json!({
            "image": "mcr.microsoft.com/azureiotedge-agent:1.6",
            "imageHash": "sha256:1234",
            "digest": "sha256:5678",
            "createOptions": {
                "Labels": {
                    "net.azure-devices.edge.create-options": "{\"secret\":\"value\"}",
                    "net.azure-devices.edge.env": "{\"SECRET\":{\"value\":\"value\"}}",
                    "custom": "value"
                },
                "Env": ["SECRET=value"]
            },
            "auth": {
                "username": "user",
                "password": "password"
            }
        });

        assert_eq!(
            serde_json::json!({
                "image": "mcr.microsoft.com/azureiotedge-agent:1.6",
                "imageHash": "sha256:1234"
            }),
            super::redact_settings(settings)
        );

        // Missing fields are not added.
        assert_eq!(
            serde_json::json!({ "image": "image" }),
            super::redact_settings(serde_json::json!({ "image": "image" }))
        );

        // Non-object settings are replaced with an empty object.
        assert_eq!(
            serde_json::json!({}),
            super::redact_settings(serde_json::Value::Null)
        );
    }

    #[tokio::test]
    async fn get_redacts_settings() {
        let route = test_route_ok!(super::PATH);
        let names = ["module1", "module2"];

        {
            let mut runtime = route.runtime.lock().await;

            runtime.modules = names
                .iter()
                .map(|name| {
                    let settings = serde_json::json!({
                        "image": format!("{name}:1.0"),
                        "imageHash": format!("sha256:{name}"),
                        "createOptions": {
                            "Labels": { "net.azure-devices.edge.env": "{\"SECRET\":\"value\"}" },
                            "Env": ["SECRET=value"]
                        },
                        "auth": { "username": "user", "password": "password" }
                    });

                    edgelet_test_utils::runtime::Module {
                        name: (*name).to_string(),
                        type_: "docker".to_string(),
                        config: edgelet_test_utils::runtime::Config {
                            settings: serde_json::from_value(settings).unwrap(),
                        },
                    }
                })
                .collect();
        }

        let response = route.get().await.unwrap();
        let body = response.into_body().collect().await.unwrap().to_bytes();
        let response: edgelet_http::ListModulesResponse = serde_json::from_slice(&body).unwrap();

        assert_eq!(names.len(), response.modules.len());

        for (name, module) in names.iter().zip(&response.modules) {
            assert_eq!(*name, module.name);
            assert_eq!(
                serde_json::json!({
                    "image": format!("{name}:1.0"),
                    "imageHash": format!("sha256:{name}")
                }),
                module.config.settings
            );
        }
    }
}
