use crate::error::{Error, ErrorKind};

use edgelet_http::Error as HttpError;
use edgelet_http::route::{Handler, Parameters};

use hyper::{Body, Client, Connector, Request, Response};
use hyper::header::{CONTENT_TYPE, CONTENT_LENGTH};

// NOTE: eschewing complex type parameterization for simplicity
pub struct CreateSecret<C: Connector> {
    client: Arc<Client<C>>
}

impl<C: Connector> CreateSecret<C> {
    pub fn new(client: Arc<Client<C>>) {
        Self { client }
    }
}

impl<C: Connector> Handler<Parameters> for CreateSecret<C> {
    fn handle(&self, req: Request<Body>, params: Parameters) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        Box::new(
            params.name("id")
                .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("id")))
                .and_then(|id| {
                    let id = id.to_string();
                    let val = req
                        .into_body()
                        .concat2()
                        .then(|b| -> Result<_, Error> {
                            let b = b.context(ErrorKind::MalformedRequestBody)?;
                            let val = serde_json::from_slice::<String>(&b)
                                .context(ErrorKind::MalformedRequestBody)?;
                            Ok(val)
                        })
                    Ok((id, val))
                })
                .and_then(|(id, val)| {
                    let val = req.
                })
        )
    }
}