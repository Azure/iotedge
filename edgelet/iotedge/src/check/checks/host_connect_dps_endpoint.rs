use std::convert::TryInto;
use std::net::TcpStream;
use std::str::FromStr;

use failure::{format_err, Context, Error, ResultExt};
use futures::Future;
use hyper::client::connect::{Connect, Destination};
use hyper::Uri;
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;
use typed_headers::Credentials;

use edgelet_core::{self, ProvisioningType, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(serde_derive::Serialize, Default)]
pub(crate) struct HostConnectDpsEndpoint {
    dps_endpoint: Option<String>,
    dps_hostname: Option<String>,
    proxy: Option<String>,
}

impl Checker for HostConnectDpsEndpoint {
    fn id(&self) -> &'static str {
        "host-connect-dps-endpoint"
    }
    fn description(&self) -> &'static str {
        "host can connect to and perform TLS handshake with DPS endpoint"
    }
    fn execute(&mut self, check: &mut Check, runtime: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check, runtime)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl HostConnectDpsEndpoint {
    fn inner_execute(
        &mut self,
        check: &mut Check,
        runtime: &mut tokio::runtime::Runtime,
    ) -> Result<CheckResult, failure::Error> {
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

        if let Some(proxy) = &self.proxy {
            runtime.block_on(resolve_and_tls_handshake_proxy(
                dps_endpoint.as_str().to_owned(),
                dps_endpoint.port(),
                proxy.clone(),
            ))?;
        } else {
            resolve_and_tls_handshake(&dps_endpoint, dps_hostname, dps_hostname)?;
        }

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

pub fn resolve_and_tls_handshake_proxy(
    tls_hostname: String,
    port: Option<u16>,
    proxy: String,
) -> impl Future<Item = (), Error = Error> {
    futures::future::ok(())
        .and_then({
            move |_| -> Result<_, Error> {
                let proxy_uri = Uri::from_str(&proxy)
                    .with_context(|_| format!("Could not make proxi uri from {}", proxy))?;

                let credentials = proxy_uri
                    .authority_part()
                    .and_then(|authority| {
                        if let [userpass, _] =
                            &authority.as_str().split('@').collect::<Vec<&str>>()[..]
                        {
                            if let [username, password] =
                                &userpass.split(':').collect::<Vec<&str>>()[..]
                            {
                                return Some(Credentials::basic(username, password));
                            }
                        }
                        None
                    })
                    .transpose()
                    .with_context(|e| format!("Could not parse credentuals. Reason: {}", e))?;

                let mut proxy_obj = Proxy::new(Intercept::All, proxy_uri);
                if let Some(credentials) = credentials {
                    proxy_obj.set_authorization(credentials);
                }

                let http_connector = HttpsConnector::new(1)
                    .with_context(|_| "Could not make https connector".to_owned())?;
                let proxy_connector = ProxyConnector::from_proxy(http_connector, proxy_obj)
                    .with_context(|_| "Could not make proxy connector".to_owned())?;

                let port = port.map(|p| format!(":{}", p)).unwrap_or_default();
                let hostname = Uri::from_str(&format!("https://{}{}", tls_hostname, port))
                    .with_context(|_| {
                        format!("Could not make uri from {}{}", tls_hostname, port)
                    })?;

                let hostname: Result<Destination, _> = hostname.try_into();
                let hostname = hostname.with_context(|_| "Could not make hostname uri")?;
                Ok((proxy_connector, hostname))
            }
        })
        .and_then(move |(proxy_connector, hostname)| {
            proxy_connector
                .connect(hostname)
                .map_err(|e| format_err!("Could not connect: {}", e))
        })
        .map(drop)
}
