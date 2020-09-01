use crate::IntoResponse;
use crate::error::{Error, ErrorKind};

use std::sync::{Arc, Mutex};

use edgelet_core::{SecretManager, SecretOperation};
use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response};

pub struct DeleteSecret<S> {
    secret_manager: Arc<Mutex<S>>
}

impl<S> DeleteSecret<S> {
    pub fn new(secret_manager: S) -> Self {
        Self {
            secret_manager: Arc::new(Mutex::new(secret_manager))
        }
    }
}

impl<S> Handler<Parameters> for DeleteSecret<S>
where
    S: 'static + SecretManager + Send
{
    fn handle(&self, _req: Request<Body>, params: Parameters) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let secret_manager = self.secret_manager.clone();

        let response = params.name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .and_then(|name| {
                let id = params.name("id")
                    .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("id")))?;
                Ok((name, id))
            })
            .map(|(name, id)| {
                let id = format!("{}/{}", name, id);
                let secret_manager = secret_manager.lock().unwrap();
                secret_manager.delete(&id)
                    .then(|result| {
                        result.context(ErrorKind::SecretOperation(SecretOperation::Delete(id)))?;
                        Ok(())
                    })
            })
            .into_future()
            .flatten()
            .and_then(|_| Ok(Response::builder().status(204).body(Body::empty()).unwrap()))
            .or_else(|e: Error| Ok(e.into_response()));

        Box::new(response)
    }
}


