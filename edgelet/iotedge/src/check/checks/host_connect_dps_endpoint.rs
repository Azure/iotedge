use std::net::TcpStream;

use failure::{Context, ResultExt};

use edgelet_core::{self, ProvisioningType, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct HostConnectDpsEndpoint {
    dps_endpoint: Option<String>,
    dps_hostname: Option<String>,
}

impl Checker for HostConnectDpsEndpoint {
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

impl HostConnectDpsEndpoint {
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
) -> Result<(), failure::Error> {
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
