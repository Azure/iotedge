use crate::IntoResponse;
use crate::error::{Error, ErrorKind};

use std::sync::{Arc, Mutex};

use edgelet_core::SecretManager;
use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use failure::ResultExt;
use futures::{future, IntoFuture, Future, Stream};
use hyper::{Body, Chunk, Request, Response};

pub struct SetSecret<S> {
    secret_manager: Arc<Mutex<S>>
}

impl<S> SetSecret<S> {
    pub fn new(secret_manager: S) -> Self {
        Self {
            secret_manager: Arc::new(Mutex::new(secret_manager))
        }
    }
}

impl<S> Handler<Parameters> for SetSecret<S>
where
    S: 'static + SecretManager + Send
{
    fn handle(&self, req: Request<Body>, params: Parameters) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = params.name("id")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("id")))
            .map(|id| {
                let id = id.to_string();
                req.into_body()
                    .concat2()
                    .then(|b| -> Result<_, Error> {
                        let b: Chunk = b.context(ErrorKind::MalformedRequestBody)?;
                        Ok(serde_json::from_slice::<String>(&b)
                            .context(ErrorKind::MalformedRequestBody)?)
                    })
                    .map(|val| (id, val))
            })
            .into_future()
            .flatten()
            .and_then(|(id, val)| {
                let secret_manager = self.secret_manager.lock().unwrap();
                future::ok(())
            })
            .and_then(|_| Response::new(Body::empty()))
            .or_else(|e| e.into_response());
        Box::new(response)
    }
}
/*
            params.name("id")
                .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("id")))
                .and_then(|id| {
                    let id = id.to_string();
                    let val = req
                        .into_body()
                        .concat2()
                        .then(|b| -> Result<_, Error> {
                            let b: hyper::body::Chunk = b.context(ErrorKind::MalformedRequestBody)?;
                            let val = serde_json::from_slice::<String>(&b)
                                .context(ErrorKind::MalformedRequestBody)?;
                            Ok(val)
                        });
                    Ok((id, val))
                })
                .map(|(id, val)| set_secret(&self.client, API_VERSION, &id, val))
                .and_then(|res| match res {
                    Ok(()) => Ok(Response::new(Body::empty())),
                    Err(e) => Ok(Response::new(Body::empty()))
                })
                .or_else(|e| Ok(e.into_response()))
*/