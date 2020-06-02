// Copyright (c) Microsoft. All rights reserved.

use crate::error::{Error, ErrorKind};
use edgelet_core::WorkloadConfig;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use failure::ResultExt;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use identity::models::{IdentityResult, IdentitySpec};

use crate::IntoResponse;

const DEVICE_IDENTITY_TYPE: &str = "aziot";
const DEVICE_IDENTITY_AUTH_TYPE: &str = "sas";
const DEVICE_IDENTITY_KEY_HANDLE: &str = "primary";

pub struct IdentityHandler<W: WorkloadConfig> {
    config: W,
}

impl<W: WorkloadConfig> IdentityHandler<W> {
    pub fn new(config: W) -> Self {
        IdentityHandler { config }
    }
}

impl<W> Handler<Parameters> for IdentityHandler<W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let config = self.config.clone();

        let response = req
            .into_body()
            .concat2()
            .then(move |_| {
                let identity_auth = identity::models::Credentials::new(
                    DEVICE_IDENTITY_AUTH_TYPE.parse().unwrap(),
                    DEVICE_IDENTITY_KEY_HANDLE.parse().unwrap(),
                );
                let identity_spec = IdentitySpec::new(
                    config.iot_hub_name().to_string(),
                    config.device_id().to_string(),
                    identity_auth,
                );
                let identity_result =
                    IdentityResult::new(DEVICE_IDENTITY_TYPE.parse().unwrap(), identity_spec);

                let body = serde_json::to_string(&identity_result)
                    .context(ErrorKind::GetIdentity)
                    .unwrap();

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .with_context(|_| ErrorKind::GetIdentity)?;

                Ok(response)
            })
            .or_else(|e: Error| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use crate::identity::IdentityHandler;
    use edgelet_core::{CertificateType, WorkloadConfig};
    use edgelet_http::route::{Handler, Parameters};
    use futures::future::Future;
    use futures::Stream;
    use hyper::{Body, Request};
    use identity::models::IdentityResult;
    use std::sync::Arc;

    struct TestWorkloadConfig {
        iot_hub_name: String,
        device_id: String,
    }

    impl Default for TestWorkloadConfig {
        #[allow(clippy::cast_possible_wrap, clippy::cast_sign_loss)]
        fn default() -> Self {
            TestWorkloadConfig {
                iot_hub_name: String::from("testiothub"),
                device_id: String::from("testiothub_device"),
            }
        }
    }

    #[derive(Clone)]
    struct TestWorkloadData {
        data: Arc<TestWorkloadConfig>,
    }

    impl Default for TestWorkloadData {
        fn default() -> Self {
            TestWorkloadData {
                data: Arc::new(TestWorkloadConfig::default()),
            }
        }
    }

    impl WorkloadConfig for TestWorkloadData {
        fn iot_hub_name(&self) -> &str {
            self.data.iot_hub_name.as_str()
        }

        fn device_id(&self) -> &str {
            self.data.device_id.as_str()
        }

        fn get_cert_max_duration(&self, _cert_type: CertificateType) -> i64 {
            unimplemented!()
        }
    }

    #[test]
    fn success() {
        let config = TestWorkloadData::default();
        let handler = IdentityHandler::new(config);
        let request = Request::get("http://localhost/identity")
            .body(Body::default())
            .unwrap();
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let identity: IdentityResult = serde_json::from_slice(&b).unwrap();
                assert_eq!("aziot", identity._type());
                assert_eq!("testiothub", identity.spec().hub_name());
                assert_eq!("testiothub_device", identity.spec().device_id());

                Ok(())
            })
            .wait()
            .unwrap();
    }
}
