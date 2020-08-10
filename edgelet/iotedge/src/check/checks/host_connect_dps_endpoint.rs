use std::net::TcpStream;
use std::str::FromStr;

use failure::{Context, Error, ResultExt};
use futures::Future;
use hyper::client::HttpConnector;
use hyper::{Body, Client, StatusCode, Uri};
use hyper_proxy::{Intercept, Proxy, ProxyConnector};

use edgelet_core::{self, ProvisioningType, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(serde_derive::Serialize)]
pub(crate) struct HostConnectDpsEndpoint<'a> {
    dps_endpoint: Option<String>,
    dps_hostname: Option<String>,
    proxy: Option<String>,

    #[serde(skip)]
    runtime: &'a mut tokio::runtime::Runtime,
}

impl<'a> Checker for HostConnectDpsEndpoint<'a> {
    fn id(&self) -> &'static str {
        "host-connect-dps-endpoint"
    }
    fn description(&self) -> &'static str {
        "host can connect to and perform TLS handshake with DPS endpoint"
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl<'a> HostConnectDpsEndpoint<'a> {
    pub fn new(runtime: &'a mut tokio::runtime::Runtime) -> Self {
        Self {
            dps_endpoint: None,
            dps_hostname: None,
            proxy: None,
            runtime,
        }
    }

    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let dps_endpoint =
            if let ProvisioningType::Dps(dps) = settings.provisioning().provisioning_type() {
                dps.global_endpoint()
            } else {
                return Ok(CheckResult::Ignored);
            };
        self.dps_endpoint = Some(format!("{}", dps_endpoint));

        let dps_hostname = dps_endpoint.host_str().ok_or_else(|| {
            Context::new("URL specified in provisioning.global_endpoint does not have a host")
        })?;
        self.dps_hostname = Some(dps_hostname.to_owned());

        let proxy = settings
            .agent()
            .env()
            .get("https_proxy")
            .map(std::string::String::as_str);
        self.proxy = proxy.map(std::borrow::ToOwned::to_owned);

        resolve_and_tls_handshake(&dps_endpoint, dps_hostname, dps_hostname)?;

        Ok(CheckResult::Ok)
    }
}

// Resolves the given `ToSocketAddrs`, then connects to the first address via TCP and completes a TLS handshake.
//
// `tls_hostname` is used for SNI validation and certificate hostname validation.
//
// `hostname_display` is used for the error messages.
pub fn resolve_and_tls_handshake(
    to_socket_addrs: &impl std::net::ToSocketAddrs,
    tls_hostname: &str,
    hostname_display: &str,
) -> Result<(), Error> {
    let host_addr = to_socket_addrs
        .to_socket_addrs()
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not resolve hostname",
                hostname_display,
            )
        })?
        .next()
        .ok_or_else(|| {
            Context::new(format!(
                "Could not connect to {} : could not resolve hostname: no addresses found",
                hostname_display,
            ))
        })?;

    let stream = TcpStream::connect_timeout(&host_addr, std::time::Duration::from_secs(10))
        .with_context(|_| format!("Could not connect to {}", hostname_display))?;

    let tls_connector = native_tls::TlsConnector::new().with_context(|_| {
        format!(
            "Could not connect to {} : could not create TLS connector",
            hostname_display,
        )
    })?;
    let _ = tls_connector
        .connect(tls_hostname, stream)
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not complete TLS handshake",
                hostname_display,
            )
        })?;

    Ok(())
}

pub fn resolve_and_tls_handshake_proxy<'a>(
    tls_hostname: &'a str,
    port: u16,
    proxy: &'a str,
) -> impl Future<Item = (), Error = Error> + 'a {
    futures::future::ok(())
        .and_then(move |_| -> Result<_, Error> {
            let proxy_uri = Uri::from_str(proxy)
                .with_context(|_| format!("Could not make proxi uri from {}", proxy))?;

            println!("proxy uri {:#?}", proxy_uri);

            let proxy_obj = Proxy::new(Intercept::All, proxy_uri);
            let http_connector = HttpConnector::new(1);
            let proxy_connector = ProxyConnector::from_proxy(http_connector, proxy_obj)
                .with_context(|_| format!("Could not make proxy connector"))?;

            let hostname = Uri::from_str(&format!("http://{}:{}", tls_hostname, port))
                .with_context(|_| format!("Could not make uri from {}:{}", tls_hostname, port))?;
            println!("hostname: {}", hostname);

            let client: Client<_, Body> = Client::builder().build(proxy_connector);
            Ok((client, hostname))
        })
        .and_then(move |(client, hostname)| {
            client.get(hostname).then(move |r| {
                Ok(r.with_context(|_| {
                    format!(
                        "Could not get {}:{} through proxy {}",
                        tls_hostname, port, proxy
                    )
                })?)
            })
        })
        .and_then(|res| {
            let status = res.status();
            println!("Got response: {}", status);

            // Get request to iothub 443, 8883 and 5671 are bad gateway. Other ports have different errors
            if status.is_success() || status == StatusCode::BAD_GATEWAY {
                Ok(())
            } else {
                let reason = status.canonical_reason().unwrap();
                // Err(Error::from())
                panic!("Reason: {}", reason);
            }
        })
}
