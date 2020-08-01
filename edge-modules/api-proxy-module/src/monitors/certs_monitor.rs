
use super::utils;
use super::workload_api_client;
use tokio::sync::Notify;
use std::sync::Arc;
use std::time::Duration;
use tokio::time::delay_for;
use anyhow::Result;
use workload_api_client::WorkloadAPIClient;
use chrono::Utc;

const PROXY_SERVER_TRUSTED_CA_PATH:&str = "trustedCA.crt";
const PROXY_SERVER_CERT_PATH:&str = "server.crt"; 
const PROXY_SERVER_PRIVATE_KEY_PATH:&str = "private_key.pem"; 


pub fn start(runtime_cert_monitor: tokio::runtime::Handle, notify_certs_rotated: Arc<Notify>) {
    const NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS: Duration = Duration::from_secs(1);
    let mut cert_monitor = CertificateMonitor::new();

    loop{
        runtime_cert_monitor.block_on(delay_for(NGINX_CERTIFICATE_MONITOR_POLL_INTERVAL_SECS));
        let mut need_notify = false;
        match cert_monitor.has_bundle_of_trust_rotated() {
            Ok((has_bundle_of_trust_rotated, trust_bundle)) => {
                if has_bundle_of_trust_rotated == true
                {
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

        match cert_monitor.need_to_rotate_server_cert() {
            Ok((need_to_rotate_cert, server_cert, private_key)) => {
                if need_to_rotate_cert == true
                {
                    let result = utils::write_binary_to_file(server_cert.as_bytes(),PROXY_SERVER_CERT_PATH);
                    match result {
                        Err(err) => panic!("{:?}", err),
                        Ok(_) => (),
                    }
        
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
	
	bundle_of_trust_hash: u64,
	work_load_api_client: WorkloadAPIClient,
	cert_expiry_date: chrono::DateTime<Utc>,
}

impl CertificateMonitor{

	pub fn new() -> Self{
		let work_load_api_client = match  WorkloadAPIClient::new(){		
			Ok(work_load_api_client) => (work_load_api_client),
			Err(err) => panic!(err),
		};

		//Create expiry date in the past so cert has to be rotated now.
		let cert_expiry_date = match Utc::now().checked_sub_signed(chrono::Duration::days(1)) {
			Some(expiration) => (expiration),
			None => panic!("Could not get certificate expiry date"),
		}; 

		CertificateMonitor{
			bundle_of_trust_hash: 0,
			work_load_api_client,
			cert_expiry_date,
		}
	}

	fn need_to_rotate_server_cert(&mut self)-> Result<(bool, String, String), anyhow::Error>
	{
		let current_date = Utc::now();
		let mut need_to_rotate = false;
		let server_crt;
		let private_key;

		if current_date >= self.cert_expiry_date
		{
			let (tmp_server_crt, tmp_private_key, cert_expiry_date) = self.work_load_api_client.get_server_cert_and_private_key()?;
			server_crt = tmp_server_crt;
			private_key = tmp_private_key;
			need_to_rotate = true;
			self.cert_expiry_date = cert_expiry_date;
		}else
		{
			server_crt = "".to_string();
			private_key = "".to_string();
		}
			
		Ok((need_to_rotate, server_crt, private_key))
	}

	fn has_bundle_of_trust_rotated(&mut self)-> Result<(bool, String), anyhow::Error>
	{
		let mut has_bundle_of_trust_rotated = false;
		let trust_bundle = self.work_load_api_client.get_bundle_of_trust()?;
	
	
		let bundle_of_trust_hash = fasthash::xx::hash64(&trust_bundle);

		if bundle_of_trust_hash != self.bundle_of_trust_hash
		{
			has_bundle_of_trust_rotated = true;
			self.bundle_of_trust_hash = bundle_of_trust_hash;
		}  	

		Ok((has_bundle_of_trust_rotated, trust_bundle))
	}
} 

