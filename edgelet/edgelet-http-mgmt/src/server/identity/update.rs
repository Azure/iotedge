// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use anyhow::Context;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_identity_common::Identity as AziotIdentity;
use edgelet_core::{IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use identity_client::client::IdentityClient;
use management::models::{Identity, UpdateIdentity as UpdateIdentitySpec};

use crate::IntoResponse;
use crate::error::Error;

pub struct UpdateIdentity {
    id_manager: Arc<Mutex<IdentityClient>>,
}

impl UpdateIdentity {
    pub fn new(id_manager: Arc<Mutex<IdentityClient>>) -> Self {
        UpdateIdentity { id_manager }
    }
}

impl Handler<Parameters> for UpdateIdentity {
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let id_mgr = self.id_manager.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(Error::MissingRequiredParameter("name")))
            .map(|name| {
                let name = name.to_string();
                read_request(name.clone(), req).map(|spec| (spec, name))
            })
            .into_future()
            .flatten()
            .and_then(move |(spec, _name)| {
                let rid = id_mgr.lock().unwrap();

                let module_id = spec.module_id().to_string();
                let managed_by = spec.managed_by().unwrap_or("iotedge").to_string();

                rid.update_module(module_id.as_ref())
                    .then(move |identity| -> anyhow::Result<_> {
                        let identity = identity.context(Error::IotHub)?;

                        let (module_id, generation_id, auth) = match identity {
                            AziotIdentity::Aziot(spec) => (
                                spec.module_id.context(Error::IotHub)?,
                                spec.gen_id.context(Error::IotHub)?,
                                spec.auth.context(Error::IotHub)?,
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
                            Error::IdentityOperation(IdentityOperation::UpdateIdentity(
                                module_id.0.clone(),
                            ))
                        })?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, b.len().to_string().as_str())
                            .body(b.into())
                            .with_context(|| {
                                Error::IdentityOperation(IdentityOperation::UpdateIdentity(
                                    module_id.0,
                                ))
                            })?;
                        Ok(response)
                    })
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

fn read_request(
    name: String,
    req: Request<Body>,
) -> impl Future<Item = IdentitySpec, Error = anyhow::Error> {
    req.into_body().concat2().then(move |b| {
        let b = b.context(Error::MalformedRequestBody)?;
        let update_req = serde_json::from_slice::<UpdateIdentitySpec>(&b)
            .context(Error::MalformedRequestBody)?;
        let mut spec =
            IdentitySpec::new(name).with_generation_id(update_req.generation_id().to_string());
        if let Some(m) = update_req.managed_by() {
            spec = spec.with_managed_by(m.to_string());
        }
        Ok(spec)
    })
}
