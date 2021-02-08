// Copyright (c) Microsoft. All rights reserved.

use std::str;
use std::sync::{Arc, Mutex};

use failure::ResultExt;
use futures::{Future, IntoFuture};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use cert_client::client::CertificateClient;
use edgelet_core::WorkloadConfig;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use workload::models::TrustBundleResponse;

use crate::error::{EncryptionOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct TrustBundleHandler<W: WorkloadConfig> {
    cert_client: Arc<Mutex<CertificateClient>>,
    config: W,
}

impl<W: WorkloadConfig> TrustBundleHandler<W> {
    pub fn new(cert_client: Arc<Mutex<CertificateClient>>, config: W) -> Self {
        TrustBundleHandler {
            cert_client,
            config,
        }
    }
}

impl<W> Handler<Parameters> for TrustBundleHandler<W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let config = self.config.clone();
        let response = self
            .cert_client
            .lock()
            .expect("cert client lock failed")
            .get_cert(config.trust_bundle_cert())
            .map_err(|_| Error::from(ErrorKind::GetIdentity))
            .and_then(|cert| -> Result<_, Error> {
                let cert = str::from_utf8(cert.as_ref())
                    .context(ErrorKind::EncryptionOperation(
                        EncryptionOperation::GetTrustBundle,
                    ))?
                    .to_string();
                let body = serde_json::to_string(&TrustBundleResponse::new(cert)).context(
                    ErrorKind::EncryptionOperation(EncryptionOperation::GetTrustBundle),
                )?;
                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .context(ErrorKind::EncryptionOperation(
                        EncryptionOperation::GetTrustBundle,
                    ))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()))
            .into_future();

        Box::new(response)
    }
}
