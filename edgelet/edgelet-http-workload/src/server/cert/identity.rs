// Copyright (c) Microsoft. All rights reserved.
use std::sync::{Arc, Mutex};

use super::{compute_validity, refresh_cert};
use failure::ResultExt;
use futures::{Future, IntoFuture, Stream};
use hyper::{Body, Request, Response};

use cert_client::client::CertificateClient;
use edgelet_core::{CertificateProperties, CertificateType, WorkloadConfig};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use edgelet_utils::{ensure_not_empty_with_context, prepare_cert_uri_module};
use workload::models::IdentityCertificateRequest;

use crate::error::{CertOperation, Error, ErrorKind};
use crate::IntoResponse;

pub struct IdentityCertHandler<W: WorkloadConfig> {
    cert_client: Arc<Mutex<CertificateClient>>,
    key_client: Arc<aziot_key_client::Client>,
    config: W,
}

impl<W: WorkloadConfig> IdentityCertHandler<W> {
    pub fn new(
        key_client: Arc<aziot_key_client::Client>,
        cert_client: Arc<Mutex<CertificateClient>>,
        config: W,
    ) -> Self {
        IdentityCertHandler {
            key_client,
            cert_client,
            config,
        }
    }
}

impl<W> Handler<Parameters> for IdentityCertHandler<W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let cfg = self.config.clone();
        let cert_client = self.cert_client.clone();
        let key_client = self.key_client.clone();

        let response = params
            .name("name")
            .ok_or_else(|| Error::from(ErrorKind::MissingRequiredParameter("name")))
            .map(|module_id| {
                let cn = module_id.to_string();
                let alias = format!("aziot-edge/module/{}:identity", module_id);
                let module_uri =
                    prepare_cert_uri_module(cfg.iot_hub_name(), cfg.device_id(), module_id);

                req.into_body().concat2().then(|body| {
                    let body =
                        body.context(ErrorKind::CertOperation(CertOperation::CreateIdentityCert))?;
                    Ok((cn, alias, module_uri, body))
                })
            })
            .into_future()
            .flatten()
            .and_then(move |(cn, alias, module_uri, body)| {
                let max_duration = cfg.get_cert_max_duration(CertificateType::Client);
                let cert_req: IdentityCertificateRequest =
                    serde_json::from_slice(&body).context(ErrorKind::MalformedRequestBody)?;

                let expiration = cert_req.expiration().map_or_else(
                    || Ok(max_duration),
                    |exp| compute_validity(exp, max_duration, ErrorKind::MalformedRequestBody),
                )?;
                #[allow(clippy::cast_sign_loss)]
                let expiration = match expiration {
                    expiration if expiration < 0 || expiration > max_duration => {
                        return Err(Error::from(ErrorKind::MalformedRequestBody));
                    }
                    expiration => expiration as u64,
                };

                ensure_not_empty_with_context(&cn, || {
                    ErrorKind::MalformedRequestParameter("name")
                })?;

                let sans = vec![module_uri];
                let props = CertificateProperties::new(
                    expiration,
                    cn,
                    CertificateType::Client,
                    alias.clone(),
                )
                .with_dns_san_entries(sans);
                Ok((alias, props, cfg))
            })
            .and_then(move |(alias, props, cfg)| {
                let response = refresh_cert(
                    &key_client,
                    cert_client,
                    alias,
                    &props,
                    super::EdgeCaCertificate {
                        cert_id: cfg.edge_ca_cert().to_string(),
                        key_id: cfg.edge_ca_key().to_string(),
                    },
                    &ErrorKind::CertOperation(CertOperation::CreateIdentityCert),
                )
                .map_err(|_| {
                    Error::from(ErrorKind::CertOperation(CertOperation::CreateIdentityCert))
                });
                Ok(response)
            })
            .flatten()
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}
