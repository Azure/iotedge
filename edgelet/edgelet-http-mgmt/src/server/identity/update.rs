// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_identity_common::Identity as AziotIdentity;
use edgelet_core::{IdentityOperation, IdentitySpec};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;
use management::models::{Identity, UpdateIdentity as UpdateIdentitySpec};

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

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
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let id_mgr = self.id_manager.clone();
        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
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
                    .then(move |identity| -> Result<_, Error> {
                        let identity = identity.with_context(|_| ErrorKind::IotHub)?;

                        let (module_id, generation_id, auth) = match identity {
                            AziotIdentity::Aziot(spec) => (
                                spec.module_id.ok_or(ErrorKind::IotHub)?,
                                spec.gen_id.ok_or(ErrorKind::IotHub)?,
                                spec.auth.ok_or(ErrorKind::IotHub)?,
                            ),
                            AziotIdentity::Local(_) => {
                                return Err(Error::from(ErrorKind::InvalidIdentityType))
                            }
                        };

                        let identity = Identity::new(
                            module_id.0.clone(),
                            managed_by,
                            generation_id.0,
                            auth.auth_type.to_string(),
                        );

                        let b = serde_json::to_string(&identity).with_context(|_| {
                            ErrorKind::IdentityOperation(IdentityOperation::UpdateIdentity(
                                module_id.0.clone(),
                            ))
                        })?;
                        let response = Response::builder()
                            .status(StatusCode::OK)
                            .header(CONTENT_TYPE, "application/json")
                            .header(CONTENT_LENGTH, b.len().to_string().as_str())
                            .body(b.into())
                            .with_context(|_| {
                                ErrorKind::IdentityOperation(IdentityOperation::UpdateIdentity(
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
) -> impl Future<Item = IdentitySpec, Error = Error> {
    req.into_body().concat2().then(move |b| {
        let b = b.context(ErrorKind::MalformedRequestBody)?;
        let update_req = serde_json::from_slice::<UpdateIdentitySpec>(&b)
            .context(ErrorKind::MalformedRequestBody)?;
        let mut spec =
            IdentitySpec::new(name).with_generation_id(update_req.generation_id().to_string());
        if let Some(m) = update_req.managed_by() {
            spec = spec.with_managed_by(m.to_string());
        }
        Ok(spec)
    })
}
