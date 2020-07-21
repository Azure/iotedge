use std::{
    convert::TryInto,
    env,
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{bail, Context, Result};
use chrono::{DateTime, Duration, Utc};

use futures_util::{
    future::{self, Either},
    pin_mut, FutureExt,
};
use tokio::time;
use tracing::{info, warn};

use mqtt_broker::{Broker, BrokerBuilder, BrokerSnapshot, Server};
use mqtt_broker_core::{auth::Authorizer, settings::BrokerConfig};
use mqtt_edgehub::{
    auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer},
    edgelet,
    settings::Settings,
    tls::ServerCertificate,
};

pub fn config<P>(config_path: Option<P>) -> Result<Settings>
where
    P: AsRef<Path>,
{
    let config = if let Some(path) = config_path {
        info!("loading settings from a file {}", path.as_ref().display());
        Settings::from_file(path)?
    } else {
        info!("using default settings");
        Settings::default()
    };

    Ok(config)
}

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<LocalAuthorizer<EdgeHubAuthorizer>>> {
    let broker = BrokerBuilder::default()
        .with_authorizer(LocalAuthorizer::new(EdgeHubAuthorizer::default()))
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn start_server<Z, F>(
    config: &Settings,
    broker: Broker<Z>,
    shutdown_signal: F,
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()> + Unpin,
{
    let mut server = Server::from_broker(broker);

    // Add system transport to allow communication between edgehub components
    let authenticator = LocalAuthenticator::new();
    server.tcp(config.listener().system().addr(), authenticator);

    // Add regular MQTT over TCP transport
    let authenticator = EdgeHubAuthenticator::new(config.auth().url());
    if let Some(tcp) = config.listener().tcp() {
        server.tcp(tcp.addr(), authenticator.clone());
    }

    // Add regular MQTT over TLS transport
    let renewal_signal = match config.listener().tls() {
        Some(tls) => {
            let identity = if let Some(path) = tls.cert_path() {
                info!("loading identity from {}", path.display());
                ServerCertificate::from_file(path)
                    .with_context(|| ServerCertificateLoadError::File(path.to_path_buf()))?
            } else {
                info!("downloading identity from edgelet");
                download_server_certificate()
                    .await
                    .with_context(|| ServerCertificateLoadError::Edgelet)?
            };
            let renew_at = identity.not_after();
            server.tls(tls.addr(), identity.try_into()?, authenticator.clone())?;

            let renewal_signal = server_certificate_renewal(renew_at);
            Either::Left(renewal_signal)
        }
        None => Either::Right(future::pending()),
    };

    // Prepare shutdown signal which is either SYSTEM shutdown signal or cert renewal timout
    pin_mut!(renewal_signal);
    let shutdown = future::select(shutdown_signal, renewal_signal).map(drop);

    // Start serving new connections
    let state = server.serve(shutdown).await?;
    Ok(state)
}

async fn server_certificate_renewal(renew_at: DateTime<Utc>) {
    let delay = renew_at - Utc::now();
    if delay > Duration::zero() {
        info!(
            "scheduled server certificate renewal timer for {}",
            renew_at
        );
        let delay = delay.to_std().expect("duration must not be negative");
        time::delay_for(delay).await;

        info!("restarting the broker to perform certificate renewal");
    } else {
        warn!("server certificate expired at {}", renew_at);
    }
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateLoadError {
    #[error("unable to load server certificate from file {0}")]
    File(PathBuf),

    #[error("unable to download certificate from edgelet")]
    Edgelet,
}

pub const WORKLOAD_URI: &str = "IOTEDGE_WORKLOADURI";
pub const EDGE_DEVICE_HOST_NAME: &str = "EdgeDeviceHostName";
pub const MODULE_ID: &str = "IOTEDGE_MODULEID";
pub const MODULE_GENERATION_ID: &str = "IOTEDGE_MODULEGENERATIONID";

pub const CERTIFICATE_VALIDITY_DAYS: i64 = 90;

async fn download_server_certificate() -> Result<ServerCertificate> {
    let uri = env::var(WORKLOAD_URI)?;
    let hostname = env::var(EDGE_DEVICE_HOST_NAME)?;
    let module_id = env::var(MODULE_ID)?;
    let generation_id = env::var(MODULE_GENERATION_ID)?;
    let expiration = Utc::now() + Duration::days(CERTIFICATE_VALIDITY_DAYS);

    let client = edgelet::workload(&uri)?;
    let cert = client
        .create_server_cert(&module_id, &generation_id, &hostname, expiration)
        .await?;

    if cert.private_key().type_() != "key" {
        bail!(
            "unknown type of private key: {}",
            cert.private_key().type_()
        );
    }

    if let Some(private_key) = cert.private_key().bytes() {
        let identity = ServerCertificate::from_pem_pair(cert.certificate(), private_key)?;
        Ok(identity)
    } else {
        bail!("missing private key");
    }
}

#[cfg(test)]
mod tests {
    use std::{
        env,
        time::{Duration as StdDuration, Instant},
    };

    use chrono::{Duration, Utc};
    use mockito::mock;
    use serde_json::json;

    use super::{
        download_server_certificate, server_certificate_renewal, EDGE_DEVICE_HOST_NAME,
        MODULE_GENERATION_ID, MODULE_ID, WORKLOAD_URI,
    };

    const PRIVATE_KEY: &str = include_str!("../../../../mqtt-edgehub/test/tls/pkey.pem");

    const CERTIFICATE: &str = include_str!("../../../../mqtt-edgehub/test/tls/cert.pem");

    #[tokio::test]
    async fn it_downloads_server_cert() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": PRIVATE_KEY },
                "certificate": CERTIFICATE,
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        env::set_var(WORKLOAD_URI, mockito::server_url());
        env::set_var(EDGE_DEVICE_HOST_NAME, "localhost");
        env::set_var(MODULE_ID, "$edgeHub");
        env::set_var(MODULE_GENERATION_ID, "12345678");

        let res = download_server_certificate().await;
        assert!(res.is_ok());
    }

    #[tokio::test]
    async fn it_schedules_cert_renewal_in_future() {
        let now = Instant::now();

        let renew_at = Utc::now() + Duration::milliseconds(100);
        server_certificate_renewal(renew_at).await;

        let elapsed = now.elapsed();
        assert!(elapsed > StdDuration::from_millis(100));
        assert!(elapsed < StdDuration::from_millis(500));
    }

    #[tokio::test]
    async fn it_does_not_schedule_cert_renewal_in_past() {
        let now = Instant::now();

        let renew_at = Utc::now();
        server_certificate_renewal(renew_at).await;

        assert!(now.elapsed() < StdDuration::from_millis(100));
    }
}
