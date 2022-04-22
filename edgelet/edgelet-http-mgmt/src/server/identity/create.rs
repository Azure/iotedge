// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use anyhow::Context;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_identity_common::Identity as AziotIdentity;
use edgelet_core::{IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use identity_client::client::IdentityClient;
use management::models::{Identity, IdentitySpec as CreateIdentitySpec};

use crate::error::Error;

pub struct CreateIdentity {
    id_manager: Arc<Mutex<IdentityClient>>,
}

impl CreateIdentity {
    pub fn new(id_manager: Arc<Mutex<IdentityClient>>) -> Self {
        CreateIdentity { id_manager }
    }
}

impl Handler<Parameters> for CreateIdentity {
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let id_mgr = self.id_manager.clone();
        let response = read_request(req)
            .and_then(move |spec| {
                let rid = id_mgr.lock().unwrap();

                let module_id = spec.module_id().to_string();
                let managed_by = spec.managed_by().unwrap_or("iotedge").to_string();

                rid.create_module(module_id.as_ref())
                    .then(move |identity| -> anyhow::Result<_> {
                        let identity = identity.context(Error::IotHub)?;

                        let (module_id, generation_id, auth) = match identity {
                            AziotIdentity::Aziot(spec) => (
                                spec.module_id.ok_or(Error::IotHub)?,
                                spec.gen_id.ok_or(Error::IotHub)?,
                                spec.auth.ok_or(Error::IotHub)?,
                            ),
                            AziotIdentity::Local(_) => {
                                return Err(Error::InvalidIdentityType.into())
                            }
                        };

                        let identity = Identity::new(
                            module_id.0.clone(),
                            managed_by,
                            generation_id.0,
                            auth.auth_type.to_string(),
                        );

                        let b = serde_json::to_string(&identity).with_context(|| {
                            Error::IdentityOperation(IdentityOperation::CreateIdentity(
                                module_id.0.clone(),
                            ))
                        })?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, b.len().to_string().as_str())
                            .body(b.into())
                            .with_context(|| {
                                Error::IdentityOperation(IdentityOperation::CreateIdentity(
                                    module_id.0,
                                ))
                            })?;
                        Ok(response)
                    })
            })
            .or_else(|e| Ok(e.downcast::<Error>().map_or_else(edgelet_http::error::catchall_error_response, Into::into)));

        Box::new(response)
    }
}

fn read_request(req: Request<Body>) -> impl Future<Item = IdentitySpec, Error = anyhow::Error> {
    req.into_body().concat2().then(|b| {
        let b = b.context(Error::MalformedRequestBody)?;
        let create_req = serde_json::from_slice::<CreateIdentitySpec>(&b)
            .context(Error::MalformedRequestBody)?;
        let mut spec = IdentitySpec::new(create_req.module_id().to_string());
        if let Some(m) = create_req.managed_by() {
            spec = spec.with_managed_by(m.to_string());
        }
        Ok(spec)
    })
}
