// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use anyhow::Context;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use aziot_identity_common::Identity as AziotIdentity;
use edgelet_core::IdentityOperation;
use edgelet_http::route::{Handler, Parameters};
use identity_client::client::IdentityClient;
use management::models::{Identity, IdentityList};

use crate::IntoResponse;
use crate::error::Error;

pub struct ListIdentities {
    id_manager: Arc<Mutex<IdentityClient>>,
}

impl ListIdentities {
    pub fn new(id_manager: Arc<Mutex<IdentityClient>>) -> Self {
        ListIdentities { id_manager }
    }
}

impl Handler<Parameters> for ListIdentities {
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let id_mgr = self.id_manager.clone();
        let response = id_mgr
            .lock()
            .unwrap()
            .get_modules()
            .then(|identities| -> anyhow::Result<_> {
                let identities = identities.context(Error::IdentityOperation(
                    IdentityOperation::ListIdentities,
                ))?;
                let body = IdentityList::new(
                    identities
                        .into_iter()
                        .map(|identity| {
                            let (module_id, generation_id) = match identity {
                                AziotIdentity::Aziot(spec) => (
                                    spec.module_id.ok_or(Error::IotHub),
                                    spec.gen_id.ok_or(Error::IotHub),
                                ),
                                AziotIdentity::Local(_) => (
                                    Err(Error::InvalidIdentityType),
                                    Err(Error::InvalidIdentityType),
                                ),
                            };

                            let module_id = module_id.expect("failed to get module_id");
                            let generation_id = generation_id.expect("failed to get generation_id");

                            Identity::new(
                                module_id.0,
                                "iotedge".to_string(),
                                generation_id.0,
                                "sas".to_string(),
                            )
                        })
                        .collect(),
                );
                let b = serde_json::to_string(&body).context(Error::IdentityOperation(
                    IdentityOperation::ListIdentities,
                ))?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, b.len().to_string().as_str())
                    .body(b.into())
                    .context(Error::IdentityOperation(
                        IdentityOperation::ListIdentities,
                    ))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}
