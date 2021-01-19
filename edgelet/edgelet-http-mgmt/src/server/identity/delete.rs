// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::future::IntoFuture;
use futures::Future;
use hyper::{Body, Request, Response, StatusCode};

use edgelet_core::IdentityOperation;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use identity_client::client::IdentityClient;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

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
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let id_mgr = self.id_manager.clone();
        let response =
            params
                .name("name")
                .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
                .map(move |name| {
                    let name = name.to_string();

                    id_mgr.lock().unwrap().delete_module(name.as_ref()).then(
                        |result| match result {
                            Ok(_) => Ok(name),
                            Err(_) => Err(Error::from(ErrorKind::IdentityOperation(
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
                        .context(ErrorKind::IdentityOperation(
                            IdentityOperation::DeleteIdentity(name),
                        ))?)
                })
                .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}
