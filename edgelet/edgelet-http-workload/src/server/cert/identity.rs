// Copyright (c) Microsoft. All rights reserved.
use std::sync::{Arc, Mutex};

use anyhow::Context;
use futures::{Future, IntoFuture};
use hyper::{Body, Request, Response};

use cert_client::client::CertificateClient;
use edgelet_core::{CertificateProperties, CertificateType, WorkloadConfig};
use edgelet_http::route::{Handler, Parameters};
use edgelet_utils::{ensure_not_empty, prepare_cert_uri_module};

use super::refresh_cert;
use crate::IntoResponse;
use crate::error::{CertOperation, Error};

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
        _req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let cfg = self.config.clone();
        let cert_client = self.cert_client.clone();
        let key_client = self.key_client.clone();

        let response = params
            .name("name")
            .context(Error::MissingRequiredParameter("name"))
            .map(std::string::ToString::to_string)
            .into_future()
            .and_then(|module_id| {
                let cn = module_id.clone();
                let alias = format!("aziot-edged/module/{}:identity", &module_id);
                let module_uri =
                    prepare_cert_uri_module(cfg.iot_hub_name(), cfg.device_id(), &module_id);

                ensure_not_empty(&cn).with_context(|| {
                    Error::MalformedRequestParameter("name")
                })?;

                let sans = vec![module_uri];
                let props = CertificateProperties::new(cn, CertificateType::Client, alias.clone())
                    .with_dns_san_entries(sans);
                Ok((alias, props, cfg))
            })
            .and_then(move |(alias, props, cfg)| {
                let response = refresh_cert(
                    key_client,
                    cert_client,
                    alias,
                    &props,
                    super::EdgeCaCertificate {
                        cert_id: cfg.edge_ca_cert().to_string(),
                        key_id: cfg.edge_ca_key().to_string(),
                        device_id: cfg.device_id().to_string(),
                    },
                    Error::CertOperation(CertOperation::CreateIdentityCert)
                )
                .map_err(|_| {
                    anyhow::anyhow!(Error::CertOperation(CertOperation::CreateIdentityCert))
                });
                Ok(response)
            })
            .flatten()
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}
