// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;

use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use futures::sync::mpsc::UnboundedSender;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct ReprovisionDevice {
    initiate_shutdown: UnboundedSender<()>,
}

impl ReprovisionDevice {
    pub fn new(initiate_shutdown: UnboundedSender<()>) -> Self {
        ReprovisionDevice { initiate_shutdown }
    }
}

impl Handler<Parameters> for ReprovisionDevice {
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Reprovision Device");
        let response = self
            .initiate_shutdown
            .unbounded_send(())
            .map_err(|_| Error::from(ErrorKind::ReprovisionDevice))
            .and_then(|_| -> Result<_, Error> {
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .body(Body::default())
                    .context(ErrorKind::ReprovisionDevice)?;

                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()))
            .into_future();

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use edgelet_http::route::Parameters;
    use futures::sync::mpsc;
    use futures::Stream;

    use super::*;
    use crate::error::{Error, ErrorKind};

    #[test]
    fn reprovision_device_success() {
        // arrange
        let (mgmt_stop_tx, mgmt_stop_rx) = mpsc::unbounded();

        let receiver_fut = mgmt_stop_rx
            .then(|res| match res {
                Ok(_) => Err(None),
                Err(_) => Err(Some(Error::from(ErrorKind::ReprovisionDevice))),
            })
            .for_each(move |_x: Option<Error>| Ok(()))
            .then(|res| match res {
                Ok(()) => Ok(None as Option<Error>),
                Err(None) => Ok(None),
                Err(Some(e)) => Err(Some(e)),
            });

        let handler = ReprovisionDevice::new(mgmt_stop_tx);
        let request = Request::get("http://localhost/info")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::OK, response.status());
        assert!(receiver_fut.wait().ok().unwrap().is_none())
    }

    #[test]
    fn reprovision_device_failed() {
        // arrange
        let (mgmt_stop_tx, mut mgmt_stop_rx) = mpsc::unbounded();
        mgmt_stop_rx.close();

        let handler = ReprovisionDevice::new(mgmt_stop_tx);
        let request = Request::get("http://localhost/info")
            .body(Body::default())
            .unwrap();

        // act
        let response = handler.handle(request, Parameters::new()).wait().unwrap();

        // assert
        assert_eq!(StatusCode::INTERNAL_SERVER_ERROR, response.status());
    }
}
