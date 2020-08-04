use crate::IntoResponse;
use crate::error::{Error, ErrorKind};

use std::sync::{Arc, Mutex};

use edgelet_core::{SecretManager, SecretOperation};
use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response};

pub struct RefreshSecret<S> {
    secret_manager: Arc<Mutex<S>>
}

impl<S> RefreshSecret<S> {
    pub fn new(secret_manager: S) -> Self {
        Self {
            secret_manager: Arc::new(Mutex::new(secret_manager))
        }
    }
}

impl<S> Handler<Parameters> for RefreshSecret<S>
where
    S: 'static + SecretManager + Send
{
    fn handle(&self, _req: Request<Body>, params: Parameters) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = params.name("id")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("id")))
            .map(|id| {
                let id = id.to_string();
                let secret_manager = self.secret_manager.lock().unwrap();
                secret_manager.refresh(&id)
                    .then(|result| {
                        result.context(ErrorKind::SecretOperation(SecretOperation::Refresh(id)))?;
                        Ok(())
                    })
            })
            .into_future()
            .flatten()
            .and_then(|_| Ok(Response::new(Body::empty())))
            .or_else(|e: Error| Ok(e.into_response()));

        Box::new(response)
    }
}
