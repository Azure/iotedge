
use super::utils;
use tokio::sync::Notify;
use std::sync::Arc;
use std::time::Duration;
use tokio::time::delay_for;
use anyhow::Result;
use chrono::Utc;
use futures_util::future::{self, Either};

const PROXY_SERVER_TRUSTED_CA_PATH:&str = "/app/trustedCA.crt";
const PROXY_SERVER_CERT_PATH:&str = "/app/server.crt"; 
const PROXY_SERVER_PRIVATE_KEY_PATH:&str = "/app/private_key.pem"; 

const PROXY_SERVER_CERT_VALIDITY_DAYS:i64 = 90; 
//Time in the past, to trigger an certificate expiration event so the system go pick up certificate
const EXPIRY_TIME_START_DATE:&str = "1996-12-19T00:00:00+00:00";

//Check for expiry of certificates. If certificates are expired: rotate.
pub async fn start(notify_certs_rotated: Arc<Notify>, shutdown_signal: Arc<Notify>) {
	const NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS: Duration = Duration::from_secs(1);

	let module_id =	std::env::var("IOTEDGE_MODULEID").expect(&format!("Missing env var {:?}","IOTEDGE_MODULEID"));
	let generation_id = std::env::var("IOTEDGE_MODULEGENERATIONID").expect(&format!("Missing env var {:?}","IOTEDGE_MODULEGENERATIONID"));
	let gateway_hostname = std::env::var("IOTEDGE_GATEWAYHOSTNAME").expect(&format!("Missing env var {:?}","IOTEDGE_GATEWAYHOSTNAME"));
	let workload_url = std::env::var("IOTEDGE_WORKLOADURI").expect(&format!("Missing env var {:?}","IOTEDGE_WORKLOADURI"));
	let mut cert_monitor = CertificateMonitor::new(module_id, generation_id, gateway_hostname,
		                                   workload_url,chrono::Duration::days(PROXY_SERVER_CERT_VALIDITY_DAYS));

    loop{
		let wait_shutdown = shutdown_signal.notified();
		futures::pin_mut!(wait_shutdown);
		match future::select(delay_for(NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS), wait_shutdown).await{
			Either::Right(_) => {
				log::warn!("Shutting down certs monitor!");
				return;
			},
			Either::Left(_) => {}
		};

		//If root cert has rotated, we need to notify the watchdog to restart nginx.
		let mut need_notify = false;
		//Check for rotation. If rotated then we notify.
        match cert_monitor.has_bundle_of_trust_rotated().await {
            Ok((has_bundle_of_trust_rotated, trust_bundle)) => {
                if has_bundle_of_trust_rotated == true
                {
					//If we have a new cert, we need to write it in file system
                    let result = utils::write_binary_to_file(trust_bundle.as_bytes(),PROXY_SERVER_TRUSTED_CA_PATH);
                    match result {
                        Err(err) => panic!("{:?}", err),
                        Ok(_) => (),
                    }
                    need_notify = true;						
                }
            },
            Err(err) => log::error!("Error while trying to get trust bundle {:?}", err),
        };

		//Same thing as above but for private key and server cert
        match cert_monitor.need_to_rotate_server_cert(Utc::now()).await {
            Ok((need_to_rotate_cert, server_cert, private_key)) => {
                if need_to_rotate_cert == true
                {
					//If we have a new cert, we need to write it in file system
                    let result = utils::write_binary_to_file(server_cert.as_bytes(),PROXY_SERVER_CERT_PATH);
                    match result {
                        Err(err) => panic!("{:?}", err),
                        Ok(_) => (),
                    }
					
					//If we have a new cert, we need to write it in file system
                    let result = utils::write_binary_to_file(private_key.as_bytes(),PROXY_SERVER_PRIVATE_KEY_PATH);
                    match result {
                        Err(err) => panic!("{:?}", err),
                        Ok(_) => (),
                    }
                    need_notify = true;						
                }
            },
            Err(err) => {
                need_notify = false;
                log::error!("Error while trying to get server cert {:?}", err);
            },
        };

        if need_notify == true
        {
            notify_certs_rotated.notify();
        }
    }
}

struct CertificateMonitor {
	module_id: String,
	generation_id: String,
	hostname: String,
	bundle_of_trust_hash: String,
	work_load_api_client: edgelet_client::WorkloadClient,
	cert_expiration_date: chrono::DateTime<Utc>,
	validity_days: chrono::Duration
}

impl CertificateMonitor{

	pub fn new(module_id:String, generation_id: String, hostname: String, workload_url:String,  validity_days: chrono::Duration) -> Self{
		//Create expiry date in the past so cert has to be rotated now.
		let cert_expiration_date = chrono::DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE).unwrap().with_timezone(&Utc);

		let work_load_api_client = match  edgelet_client::workload(&workload_url){		
			Ok(work_load_api_client) => (work_load_api_client),
			Err(err) => panic!(err),
		};

		CertificateMonitor{
			module_id,
			generation_id,
			hostname,
			bundle_of_trust_hash: String::from(""),
			work_load_api_client,
			cert_expiration_date,
			validity_days
		}
	}

	async fn need_to_rotate_server_cert(&mut self, current_date: chrono::DateTime<Utc> )-> Result<(bool, String, String), anyhow::Error>
	{
		let mut need_to_rotate = false;
		let server_crt;
		let private_key;

		if current_date >= self.cert_expiration_date
		{
			let new_expiration_date = Utc::now().checked_add_signed(self.validity_days).expect("Could not compute new expiration date for certificate");
			let resp = self.work_load_api_client.create_server_cert(&self.module_id, &self.generation_id, &self.hostname, new_expiration_date).await?;
						
			server_crt = resp.certificate().to_string();
			let private_key_raw = resp.private_key();
			private_key = match  private_key_raw.bytes(){
				Some(val)=> val.to_string(),
				None =>	return Err(anyhow::anyhow!("Private key field is empty"))			
			};
			need_to_rotate = true;

			let datetime = chrono::DateTime::parse_from_rfc3339(&resp.expiration()).unwrap();
			// convert the string into DateTime<Utc> or other timezone
			self.cert_expiration_date = datetime.with_timezone(&Utc);
		}else
		{
			server_crt = "".to_string();
			private_key = "".to_string();
		}
			
		Ok((need_to_rotate, server_crt, private_key))
	}

	async fn has_bundle_of_trust_rotated(&mut self)-> Result<(bool, String), anyhow::Error>
	{
		let mut has_bundle_of_trust_rotated = false;
		let resp = self.work_load_api_client.trust_bundle().await?;

		let trust_bundle = resp.certificate().to_string();
		 
		let bundle_of_trust_hash = format!("{:x}", md5::compute(&trust_bundle));

		if self.bundle_of_trust_hash.ne(&bundle_of_trust_hash)
		{
			has_bundle_of_trust_rotated = true;
			self.bundle_of_trust_hash = bundle_of_trust_hash;
		}  	

		Ok((has_bundle_of_trust_rotated, trust_bundle))
	}
} 


#[cfg(test)]
mod tests {
    use chrono::{Duration, Utc};
    /*use http::StatusCode;
    use matches::assert_matches;*/
    use mockito::mock;
    use serde_json::json;
	use super::{*};

	#[tokio::test]
	async fn  test_get_server_cert_and_private_key(){
		let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "PRIVATE KEY" },
                "certificate": "CERTIFICATE",
                "expiration": expiration.to_rfc3339()
            }
		);
		
        let _m = mock(
            "POST",
            "/modules/api_proxy/genid/0000/certificate/server?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
		.create();
		
		let module_id = String::from("api_proxy");
		let generation_id = String::from("0000");
		let gateway_hostname = String::from("dummy");
		let workload_url = mockito::server_url();

		let mut client =CertificateMonitor::new(module_id, generation_id, gateway_hostname,
		                                   workload_url,chrono::Duration::days(PROXY_SERVER_CERT_VALIDITY_DAYS));

		
		let start_time = chrono::DateTime::parse_from_rfc3339(EXPIRY_TIME_START_DATE).unwrap().with_timezone(&Utc);
		let current_date = start_time.checked_add_signed(Duration::days(1)).unwrap();

		let (need_rotation, server_cert, private_key) = client.need_to_rotate_server_cert(current_date).await.unwrap();

		assert_eq!(need_rotation, true);
        assert_eq!(server_cert, "CERTIFICATE");
        assert_eq!(private_key, "PRIVATE KEY");
	}

	#[tokio::test]
	async fn  test_get_bundle_of_trust(){
        let res = json!( { "certificate": "CERTIFICATE" } );

        let _m = mock("GET", "/trust-bundle?api-version=2019-01-30")
            .with_status(200)
            .with_body(serde_json::to_string(&res).unwrap())
            .create();
		
		let module_id = String::from("api_proxy");
		let generation_id = String::from("0000");
		let gateway_hostname = String::from("dummy");
		let workload_url = mockito::server_url();

		let mut client =CertificateMonitor::new(module_id, generation_id, gateway_hostname,
		                                   workload_url,chrono::Duration::days(PROXY_SERVER_CERT_VALIDITY_DAYS));

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