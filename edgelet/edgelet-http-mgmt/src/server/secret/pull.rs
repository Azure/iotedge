use crate::IntoResponse;
use crate::error::{Error, ErrorKind};

use std::sync::{Arc, Mutex};

use edgelet_core::{SecretManager, SecretOperation};
use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::{Body, Chunk, Request, Response};

pub struct PullSecret<S> {
    secret_manager: Arc<Mutex<S>>
}

impl<S> PullSecret<S> {
    pub fn new(secret_manager: S) -> Self {
        Self {
            secret_manager: Arc::new(Mutex::new(secret_manager))
        }
    }
}

impl<S> Handler<Parameters> for PullSecret<S>
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
                    .map(|akv_id| (id, akv_id))
            })
            .into_future()
            .flatten()
            .and_then(move |(id, akv_id)| {
                let secret_manager = secret_manager.lock().unwrap();
                secret_manager.pull(&id, &akv_id)
                    .then(|result| {
                        result.context(ErrorKind::SecretOperation(SecretOperation::Pull(id, akv_id)))?;
                        Ok(())
                    })
            })
            .and_then(|_| Ok(Response::new(Body::empty())))
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}
