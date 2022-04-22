// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use anyhow::Context;
use futures::future::IntoFuture;
use futures::Future;
use hyper::{Body, Request, Response, StatusCode};

use edgelet_core::IdentityOperation;
use edgelet_http::route::{Handler, Parameters};
use identity_client::client::IdentityClient;

use crate::error::Error;

pub struct DeleteIdentity {
    id_manager: Arc<Mutex<IdentityClient>>,
}

impl DeleteIdentity {
    pub fn new(id_manager: Arc<Mutex<IdentityClient>>) -> Self {
        DeleteIdentity { id_manager }
    }
}

impl Handler<Parameters> for DeleteIdentity {
    fn handle(
        &self,
        _req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let id_mgr = self.id_manager.clone();
        let response =
            params
                .name("name")
                .context(Error::MissingRequiredParameter("name"))
                .map(move |name| {
                    let name = name.to_string();

                    id_mgr.lock().unwrap().delete_module(name.as_ref()).then(
                        |result| match result {
                            Ok(_) => Ok(name),
                            Err(_) => Err(anyhow::anyhow!(Error::IdentityOperation(
                                IdentityOperation::DeleteIdentity(name),
                            ))),
                        },
                    )
                })
                .into_future()
                .flatten()
                .and_then(|name| {
                    Ok(Response::builder()
                        .status(StatusCode::NO_CONTENT)
                        .body(Body::default())
                        .context(Error::IdentityOperation(
                            IdentityOperation::DeleteIdentity(name),
                        ))?)
                })
                .or_else(|e| Ok(e.downcast::<Error>().map_or_else(edgelet_http::error::catchall_error_response, Into::into)));

        Box::new(response)
    }
}
