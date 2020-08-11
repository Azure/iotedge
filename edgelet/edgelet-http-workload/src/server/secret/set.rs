use crate::IntoResponse;
use crate::error::{Error, ErrorKind};

use std::sync::{Arc, Mutex};

use edgelet_core::{SecretManager, SecretOperation};
use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
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
        let secret_manager = self.secret_manager.clone();

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
            .and_then(move |(id, val)| {
                let secret_manager = secret_manager.lock().unwrap();
                secret_manager.set(&id, &val)
                    .then(|result| {
                        result.context(ErrorKind::SecretOperation(SecretOperation::Set(id)))?;
                        Ok(())
                    })
            })
            .and_then(|_| Ok(Response::new(Body::empty())))
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}