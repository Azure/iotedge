use std::{env, sync::Arc};

use anyhow::{Context, Error, Result};
use chrono::{DateTime, Duration, Utc};
use futures_util::{
    future::{self, Either},
    pin_mut,
};
use log::{error, info, warn};
use tokio::{sync::Notify, task::JoinHandle, time};

use crate::utils::file;
use crate::utils::shutdown_handle;
use edgelet_client::CertificateResponse;
use shutdown_handle::ShutdownHandle;

const PROXY_SERVER_TRUSTED_CA_PATH: &str = "/app/trustedCA.crt";
const PROXY_SERVER_CERT_PATH: &str = "/app/server.crt";
const PROXY_SERVER_PRIVATE_KEY_PATH: &str = "/app/private_key_server.pem";

const PROXY_SERVER_VALIDITY_DAYS: i64 = 90;
const CERTIFICATE_POLL_INTERVAL: tokio::time::Duration = tokio::time::Duration::from_secs(1);

//Check for expiry of certificates. If certificates are expired: rotate.
pub fn start(
    notify_server_cert_reload_api_proxy: Arc<Notify>,
    notify_trust_bundle_reload_api_proxy: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    info!("Initializing certs monitoring loop");

    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    let module_id =
        env::var("IOTEDGE_MODULEID").context(format!("Missing env var {}", "IOTEDGE_MODULEID"))?;
    let generation_id = env::var("IOTEDGE_MODULEGENERATIONID")
        .context(format!("Missing env var {}", "IOTEDGE_MODULEGENERATIONID"))?;
    let gateway_hostname = env::var("IOTEDGE_GATEWAYHOSTNAME")
        .context(format!("Missing env var {}", "IOTEDGE_GATEWAYHOSTNAME"))?;
    let workload_url = env::var("IOTEDGE_WORKLOADURI")
        .context(format!("Missing env var {}", "IOTEDGE_WORKLOADURI"))?;
    let mut cert_monitor = CertificateMonitor::new(
        module_id,
        generation_id,
        gateway_hostname,
        &workload_url,
        Duration::days(PROXY_SERVER_VALIDITY_DAYS),
    )
    .context("Could not create cert monitor client")?;

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        let mut new_trust_bundle = false;

        //Loop until trust bundle is received.
        while !new_trust_bundle {
            let wait_shutdown = shutdown_signal.notified();
            let timeout = time::sleep(CERTIFICATE_POLL_INTERVAL);
            pin_mut!(wait_shutdown, timeout);
            if let Either::Right(_) = future::select(timeout, wait_shutdown).await {
                warn!("Shutting down certs monitor!");
                return Ok(());
            }

            //Check for rotation. If rotated then we notify.
            new_trust_bundle = match cert_monitor.get_new_trust_bundle().await {
                Ok(Some(trust_bundle)) => {
                    //If we have a new cert, we need to write it in file system
                    file::write_binary_to_file(
                        trust_bundle.as_bytes(),
                        PROXY_SERVER_TRUSTED_CA_PATH,
                    )?;
                    true
                }
                Ok(None) => false,
                Err(err) => {
                    error!("Error while trying to get trust bundle {}", err);
                    false
                }
            };
        }

        //Trust bundle just received. Request for a reset of the API proxy.
        notify_trust_bundle_reload_api_proxy.notify_one();

        info!("Starting certs monitoring loop");

        //Loop to check if server certificate expired.
        //It is implemented as a polling instead of a delay until certificate expiry, because clocks are unreliable.
        //If the system clock gets readjusted while the task is sleeping, the system might wake up after the certificate expiry.
        loop {
            let wait_shutdown = shutdown_signal.notified();
            let timeout = time::sleep(CERTIFICATE_POLL_INTERVAL);
            pin_mut!(wait_shutdown, timeout);

            if let Either::Right(_) = future::select(timeout, wait_shutdown).await {
                warn!("Shutting down certs monitor!");
                return Ok(());
            }

            //Same thing as above but for private key and server cert
            let new_server_cert = match cert_monitor.need_to_rotate_server_cert(Utc::now()).await {
                Ok(Some((server_cert, private_key))) => {
                    //If we have a new cert, we need to write it in file system
                    file::write_binary_to_file(server_cert.as_bytes(), PROXY_SERVER_CERT_PATH)?;

                    //If we have a new cert, we need to write it in file system
                    file::write_binary_to_file(
                        private_key.as_bytes(),
                        PROXY_SERVER_PRIVATE_KEY_PATH,
                    )?;

                    true
                }
                Ok(None) => false,
                Err(err) => {
                    error!("Error while trying to get server cert {}", err);
                    false
                }
            };

            if new_server_cert {
                notify_server_cert_reload_api_proxy.notify_one();
            }
        }
    });

    Ok((monitor_loop, shutdown_handle))
}

struct CertificateMonitor {
    module_id: String,
    generation_id: String,
    hostname: String,
    bundle_of_trust_hash: String,
    work_load_api_client: edgelet_client::WorkloadClient,
    server_cert_expiration_date: Option<DateTime<Utc>>,
    validity_days: Duration,
}

impl CertificateMonitor {
    pub fn new(
        module_id: String,
        generation_id: String,
        hostname: String,
        workload_url: &str,
        validity_days: Duration,
    ) -> Result<Self, Error> {
        //Create expiry date in the past so cert has to be rotated now.
        let server_cert_expiration_date = None;

        let work_load_api_client =
            edgelet_client::workload(workload_url).context("Could not get workload client")?;

        Ok(CertificateMonitor {
            module_id,
            generation_id,
            hostname,
            bundle_of_trust_hash: String::default(),
            work_load_api_client,
            server_cert_expiration_date,
            validity_days,
        })
    }

    async fn need_to_rotate_server_cert(
        &mut self,
        current_date: DateTime<Utc>,
    ) -> Result<Option<(String, String)>, anyhow::Error> {
        //If certificates are not expired, we don't need to make a query

        if let Some(expiration_date) = self.server_cert_expiration_date {
            if current_date < expiration_date {
                return Ok(None);
            }
        }

        let new_expiration_date = Utc::now()
            .checked_add_signed(self.validity_days)
            .context("Could not compute new expiration date for certificate")?;
        let resp = self
            .work_load_api_client
            .create_server_cert(
                &self.module_id,
                &self.generation_id,
                &self.hostname,
                new_expiration_date,
            )
            .await?;

        let (certificates, expiration_date) =
            unwrap_certificate_response(&resp).context("could not extract server certificates")?;
        self.server_cert_expiration_date = Some(expiration_date);

        Ok(Some(certificates))
    }

    async fn get_new_trust_bundle(&mut self) -> Result<Option<String>, anyhow::Error> {
        let resp = self.work_load_api_client.trust_bundle().await?;

        let trust_bundle = resp.certificate().to_string();

        let bundle_of_trust_hash = format!("{:x}", md5::compute(&trust_bundle));

        if self.bundle_of_trust_hash.ne(&bundle_of_trust_hash) {
            self.bundle_of_trust_hash = bundle_of_trust_hash;
            return Ok(Some(trust_bundle));
        }

        Ok(None)
    }
}

fn unwrap_certificate_response(
    resp: &CertificateResponse,
) -> Result<((String, String), DateTime<Utc>), anyhow::Error> {
    let server_crt = resp.certificate().to_string();
    let private_key_raw = resp.private_key();
    let private_key = match private_key_raw.bytes() {
        Some(val) => val.to_string(),
        None => return Err(anyhow::anyhow!("Private key field is empty")),
    };

    let datetime = DateTime::parse_from_rfc3339(resp.expiration())
        .context("Error parsing certificate expiration date")?;
    // convert the string into DateTime<Utc> or other timezone
    let expiration_date = datetime.with_timezone(&Utc);

    Ok(((server_crt, private_key), expiration_date))
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use super::*;
    use mockito::mock;
    use serde_json::json;

    #[tokio::test]
    async fn test_get_server_certs() {
        let expiration = Utc::now() + Duration::days(PROXY_SERVER_VALIDITY_DAYS);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "PRIVATE KEY" },
                "certificate": "CERTIFICATE",
                "expiration": expiration.to_rfc3339()
            }
        );

        let module_id = String::from("api_proxy");
        let generation_id = String::from("0000");
        let gateway_hostname = String::from("dummy");
        let workload_url = mockito::server_url();

        let mut client = CertificateMonitor::new(
            module_id,
            generation_id,
            gateway_hostname,
            &workload_url,
            Duration::days(PROXY_SERVER_VALIDITY_DAYS),
        )
        .unwrap();

        let current_date = Utc::now();

        let _m = mock(
            "POST",
            "/modules/api_proxy/genid/0000/certificate/server?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();
        let (server_cert, private_key) = client
            .need_to_rotate_server_cert(current_date)
            .await
            .unwrap()
            .unwrap();

        assert_eq!(server_cert, "CERTIFICATE");
        assert_eq!(private_key, "PRIVATE KEY");

        //Try again, certificate should be rotated in memory.
        let result = client
            .need_to_rotate_server_cert(current_date)
            .await
            .unwrap();

        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_get_bundle_of_trust() {
        let res = json!( { "certificate": "CERTIFICATE" } );

        let _m = mock("GET", "/trust-bundle?api-version=2019-01-30")
            .with_status(200)
            .with_body(serde_json::to_string(&res).unwrap())
            .create();

        let module_id = String::from("api_proxy");
        let generation_id = String::from("0000");
        let gateway_hostname = String::from("dummy");
        let workload_url = mockito::server_url();

        let mut client = CertificateMonitor::new(
            module_id,
            generation_id,
            gateway_hostname,
            &workload_url,
            Duration::days(PROXY_SERVER_VALIDITY_DAYS),
        )
        .unwrap();

        let bundle_of_trust = client.get_new_trust_bundle().await.unwrap().unwrap();

        assert_eq!(bundle_of_trust, "CERTIFICATE");

        let res = client.get_new_trust_bundle().await.unwrap();
        assert!(res.is_none());

        //Change the value of the certificate returned
        let res = json!( { "certificate": "CERTIFICATE2" } );
        let _m = mock("GET", "/trust-bundle?api-version=2019-01-30")
            .with_status(200)
            .with_body(serde_json::to_string(&res).unwrap())
            .create();

        assert!(client.get_new_trust_bundle().await.unwrap().is_some());
    }
}
