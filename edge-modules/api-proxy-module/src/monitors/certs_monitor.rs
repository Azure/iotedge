use std::{env, sync::Arc};

use super::utils;
use anyhow::{Context, Error, Result};
use chrono::{DateTime, Duration, Utc};
use edgelet_client::CertificateResponse;
use futures_util::future::Either;
use tokio::{sync::Notify, task::JoinHandle, time};
use utils::ShutdownHandle;

const PROXY_SERVER_TRUSTED_CA_PATH: &str = "/app/trustedCA.crt";
const PROXY_SERVER_CERT_PATH: &str = "/app/server.crt";
const PROXY_SERVER_PRIVATE_KEY_PATH: &str = "/app/private_key_server.pem";
const PROXY_IDENTITY_CERT_PATH: &str = "/app/identity.crt";
const PROXY_IDENTITY_PRIVATE_KEY_PATH: &str = "/app/private_key_identity.pem";

const PROXY_SERVER_VALIDITY_DAYS: i64 = 90;
//Time in the past, to trigger an certificate expiration event so the system go pick up certificate
const EXPIRY_TIME_START_DATE: &str = "1996-12-19T00:00:00+00:00";

//Check for expiry of certificates. If certificates are expired: rotate.
pub fn start(
    notify_certs_rotated: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    const NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS: tokio::time::Duration =
        tokio::time::Duration::from_secs(1);

    let shutdown_signal = Arc::new(tokio::sync::Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    let module_id = env::var("IOTEDGE_MODULEID")
        .context(format!("Missing env var {:?}", "IOTEDGE_MODULEID"))?;
    let generation_id = env::var("IOTEDGE_MODULEGENERATIONID").context(format!(
        "Missing env var {:?}",
        "IOTEDGE_MODULEGENERATIONID"
    ))?;
    let gateway_hostname = env::var("IOTEDGE_GATEWAYHOSTNAME")
        .context(format!("Missing env var {:?}", "IOTEDGE_GATEWAYHOSTNAME"))?;
    let workload_url = env::var("IOTEDGE_WORKLOADURI")
        .context(format!("Missing env var {:?}", "IOTEDGE_WORKLOADURI"))?;
    let mut cert_monitor = CertificateMonitor::new(
        module_id,
        generation_id,
        gateway_hostname,
        workload_url,
        Duration::days(PROXY_SERVER_VALIDITY_DAYS),
    )
    .context("Could not create cert monitor client")?;

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        loop {
            let wait_shutdown = shutdown_signal.notified();
            futures::pin_mut!(wait_shutdown);

            match futures::future::select(
                time::delay_for(NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS),
                wait_shutdown,
            )
            .await
            {
                Either::Right(_) => {
                    log::warn!("Shutting down certs monitor!");
                    return Ok(());
                }
                Either::Left(_) => {}
            };

            //If root cert has rotated, we need to notify the watchdog to restart nginx.
            let mut need_notify = false;
            //Check for rotation. If rotated then we notify.
            match cert_monitor.has_bundle_of_trust_rotated().await {
                Ok((has_bundle_of_trust_rotated, trust_bundle)) => {
                    if has_bundle_of_trust_rotated == true {
                        //If we have a new cert, we need to write it in file system
                        let result = utils::write_binary_to_file(
                            trust_bundle.as_bytes(),
                            PROXY_SERVER_TRUSTED_CA_PATH,
                        );
                        match result {
                            Err(err) => panic!("{:?}", err),
                            Ok(_) => (),
                        }
                        need_notify = true;
                    }
                }
                Err(err) => log::error!("Error while trying to get trust bundle {:?}", err),
            };

            //Same thing as above but for private key and server cert
            match cert_monitor.need_to_rotate_server_cert(Utc::now()).await {
                Ok(certs) => {
                    if let Some((server_cert, private_key)) = certs {
                        //If we have a new cert, we need to write it in file system
                        let result = utils::write_binary_to_file(
                            server_cert.as_bytes(),
                            PROXY_SERVER_CERT_PATH,
                        );
                        match result {
                            Err(err) => panic!("{:?}", err),
                            Ok(_) => (),
                        }

                        //If we have a new cert, we need to write it in file system
                        let result = utils::write_binary_to_file(
                            private_key.as_bytes(),
                            PROXY_SERVER_PRIVATE_KEY_PATH,
                        );
                        match result {
                            Err(err) => panic!("{:?}", err),
                            Ok(_) => (),
                        }
                        need_notify = true;
                    }
                }

                Err(err) => {
                    need_notify = false;
                    log::error!("Error while trying to get server cert {:?}", err);
                }
            };

            //Same thing as above but for private key and identity cert
            match cert_monitor.need_to_rotate_identity_cert(Utc::now()).await {
                Ok(certs) => {
                    if let Some((identity_cert, private_key)) = certs {
                        //If we have a new cert, we need to write it in file system
                        let result = utils::write_binary_to_file(
                            identity_cert.as_bytes(),
                            PROXY_IDENTITY_CERT_PATH,
                        );
                        match result {
                            Err(err) => panic!("{:?}", err),
                            Ok(_) => (),
                        }

                        //If we have a new cert, we need to write it in file system
                        let result = utils::write_binary_to_file(
                            private_key.as_bytes(),
                            PROXY_IDENTITY_PRIVATE_KEY_PATH,
                        );
                        match result {
                            Err(err) => panic!("{:?}", err),
                            Ok(_) => (),
                        }
                        need_notify = true;
                    }
                }

                Err(err) => {
                    need_notify = false;
                    log::error!("Error while trying to get server cert {:?}", err);
                }
            };

            if need_notify == true {
                notify_certs_rotated.notify();
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
    server_cert_expiration_date: DateTime<Utc>,
    identity_cert_expiration_date: DateTime<Utc>,
    validity_days: Duration,
}

impl CertificateMonitor {
    pub fn new(
        module_id: String,
        generation_id: String,
        hostname: String,
        workload_url: String,
        validity_days: Duration,
    ) -> Result<Self, Error> {
        //Create expiry date in the past so cert has to be rotated now.
        let server_cert_expiration_date = DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE)
            .context("Error reading Start date")?
            .with_timezone(&Utc);
        let identity_cert_expiration_date = DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE)
            .context("Error reading Start date")?
            .with_timezone(&Utc);

        let work_load_api_client = match edgelet_client::workload(&workload_url) {
            Ok(work_load_api_client) => (work_load_api_client),
            Err(err) => {
                return Err(anyhow::anyhow!(format!(
                    "Could not get workload client {:?}",
                    err
                )))
            }
        };

        Ok(CertificateMonitor {
            module_id,
            generation_id,
            hostname,
            bundle_of_trust_hash: String::default(),
            work_load_api_client,
            server_cert_expiration_date,
            identity_cert_expiration_date,
            validity_days,
        })
    }

    async fn need_to_rotate_server_cert(
        &mut self,
        current_date: DateTime<Utc>,
    ) -> Result<Option<(String, String)>, anyhow::Error> {
        //If certificates are not expired, we don't need to make a query
        if current_date < self.server_cert_expiration_date {
            return Ok(None);
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

        let (certificates, expiration_date) = self
            .unwrap_certificate_response(resp)
            .context("could not extract server certificates")?;
        self.server_cert_expiration_date = expiration_date;

        Ok(Some(certificates))
    }

    async fn need_to_rotate_identity_cert(
        &mut self,
        current_date: DateTime<Utc>,
    ) -> Result<Option<(String, String)>, anyhow::Error> {
        //If certificates are not expired, we don't need to make a query
        if current_date < self.identity_cert_expiration_date {
            return Ok(None);
        }

        let new_expiration_date = Utc::now()
            .checked_add_signed(self.validity_days)
            .context("Could not compute new expiration date for certificate")?;

        let resp = self
            .work_load_api_client
            .create_identity_cert(&self.module_id, new_expiration_date)
            .await?;

        let (certificates, expiration_date) = self
            .unwrap_certificate_response(resp)
            .context("could not extract server certificates")?;
        self.identity_cert_expiration_date = expiration_date;

        //****** TEMPORARY FIX************//
        let new_expiration_date = Utc::now()
            .checked_add_signed(Duration::hours(1))
            .context("Could not compute new expiration date for certificate")?;
        self.identity_cert_expiration_date = new_expiration_date;
        //****** TEMPORARY FIX************//

        Ok(Some(certificates))
    }

    fn unwrap_certificate_response(
        &mut self,
        resp: CertificateResponse,
    ) -> Result<((String, String), DateTime<Utc>), anyhow::Error> {
        let server_crt = resp.certificate().to_string();
        let private_key_raw = resp.private_key();
        let private_key = match private_key_raw.bytes() {
            Some(val) => val.to_string(),
            None => return Err(anyhow::anyhow!("Private key field is empty")),
        };

        let datetime = DateTime::parse_from_rfc3339(&resp.expiration())
            .context("Error parsing certificate expiration date")?;
        // convert the string into DateTime<Utc> or other timezone
        let expiration_date = datetime.with_timezone(&Utc);

        Ok(((server_crt, private_key), expiration_date))
    }

    async fn has_bundle_of_trust_rotated(&mut self) -> Result<(bool, String), anyhow::Error> {
        let mut has_bundle_of_trust_rotated = false;
        let resp = self.work_load_api_client.trust_bundle().await?;

        let trust_bundle = resp.certificate().to_string();

        let bundle_of_trust_hash = format!("{:x}", md5::compute(&trust_bundle));

        if self.bundle_of_trust_hash.ne(&bundle_of_trust_hash) {
            has_bundle_of_trust_rotated = true;
            self.bundle_of_trust_hash = bundle_of_trust_hash;
        }

        Ok((has_bundle_of_trust_rotated, trust_bundle))
    }
}

#[cfg(test)]
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
            workload_url,
            Duration::days(PROXY_SERVER_VALIDITY_DAYS),
        )
        .unwrap();

        let start_time = DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE)
            .unwrap()
            .with_timezone(&Utc);
        let current_date = start_time.checked_add_signed(Duration::days(1)).unwrap();

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
    async fn test_get_identity_certs() {
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
            workload_url,
            Duration::days(PROXY_SERVER_VALIDITY_DAYS),
        )
        .unwrap();

        let start_time = DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE)
            .unwrap()
            .with_timezone(&Utc);
        let current_date = start_time.checked_add_signed(Duration::days(1)).unwrap();

        let _m = mock(
            "POST",
            "/modules/api_proxy/certificate/identity?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();
        let (identity_cert, private_key) = client
            .need_to_rotate_identity_cert(current_date)
            .await
            .unwrap()
            .unwrap();

        assert_eq!(identity_cert, "CERTIFICATE");
        assert_eq!(private_key, "PRIVATE KEY");

        //Try again, certificate should be rotated in memory.
        let result = client
            .need_to_rotate_identity_cert(current_date)
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
            workload_url,
            Duration::days(PROXY_SERVER_VALIDITY_DAYS),
        )
        .unwrap();

        let (need_rotation, bundle_of_trust) = client.has_bundle_of_trust_rotated().await.unwrap();

        assert_eq!(need_rotation, true);
        assert_eq!(bundle_of_trust, "CERTIFICATE");

        let (need_rotation, _) = client.has_bundle_of_trust_rotated().await.unwrap();

        assert_eq!(need_rotation, false);

        //Change the value of the certificate returned
        let res = json!( { "certificate": "CERTIFICATE2" } );
        let _m = mock("GET", "/trust-bundle?api-version=2019-01-30")
            .with_status(200)
            .with_body(serde_json::to_string(&res).unwrap())
            .create();
        let (need_rotation, _) = client.has_bundle_of_trust_rotated().await.unwrap();

        assert_eq!(need_rotation, true);
    }
}
