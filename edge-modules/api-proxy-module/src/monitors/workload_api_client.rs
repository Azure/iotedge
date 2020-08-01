use chrono::Utc;
use anyhow::{Context, Result};

const PROXY_SERVER_CERT_VALIDITY_DAYS:i64 = 90; 

#[derive(Clone, Copy, Debug)]
enum Scheme {
	Http,
	Unix,
}

pub struct WorkloadAPIClient {
	module_id: String,
	generation_id: String,
	gateway_hostname: String,
	url_base: String,
	url_scheme: Scheme
}

#[derive(Debug, serde::Deserialize)]
struct TrustBundle{
	certificate: String
}

#[derive(Debug, serde::Deserialize)]
#[allow(non_snake_case)]
pub struct PrivateKey{
	bytes: String
}

#[derive(Debug, serde::Deserialize)]
#[allow(non_snake_case)]
pub struct ServerCerts{
	certificate: String,
	privateKey: PrivateKey
}


impl WorkloadAPIClient{

	pub fn new() -> Result<Self, anyhow::Error>{
		let module_id =	std::env::var("IOTEDGE_MODULEID")?;
		let generation_id = std::env::var("IOTEDGE_MODULEGENERATIONID")?;
		let gateway_hostname = std::env::var("IOTEDGE_GATEWAYHOSTNAME")?;

		let workload_url = std::env::var("IOTEDGE_WORKLOADURI")?;
		let workload_url: url::Url = workload_url.parse()?;
		
		let (url_scheme, url_base) = match workload_url.scheme() {
			"http" => (Scheme::Http, workload_url.to_string()),
			"unix" =>
				if cfg!(windows) {
					let mut workload_url = workload_url.clone();
					workload_url.set_scheme("file").expect(r#"changing the scheme of workload URI to "file" should not fail"#);
					let base = workload_url.to_file_path().ok().expect("path sources must have a valid path");
					let base = base.to_str().ok_or_else(|| anyhow::format_err!("Can't extract string"))?;
					(Scheme::Unix, base.to_owned())
				}
				else {
					(Scheme::Unix, workload_url.path().to_owned())
				},
			scheme => return Err(anyhow::anyhow!("Error {}", scheme.to_owned())),
		};

		Ok(WorkloadAPIClient{
			module_id,
			generation_id,
			gateway_hostname,
			url_base,
			url_scheme,
		})
	}

	fn make_hyper_uri(&self, scheme: Scheme, base: &str, path: &str) -> Result<hyper::Uri, Box<dyn std::error::Error + Send + Sync>> {
		match scheme {
			Scheme::Http => {
				let base = url::Url::parse(base)?;
				let url = base.join(path)?;
				let url = url.as_str().parse()?;
				Ok(url)
			},

			Scheme::Unix => Ok(hyper_uds::make_hyper_uri(base, path)?),
		}
	}


	pub fn  get_bundle_of_trust(&self)-> Result<String, anyhow::Error> {
		//Get Bundle of trust
		let url = match self.make_hyper_uri(self.url_scheme, &self.url_base, "/trust-bundle?api-version=2019-01-30") {
			Ok(url) => url,
			Err(_) => return Err(anyhow::anyhow!("Error")),
		};

		let resp: TrustBundle = reqwest::blocking::get(&url.to_string())?.json()?;

		Ok(resp.certificate)

	}

	pub fn get_server_cert_and_private_key(&self)-> Result<(String, String, chrono::DateTime<Utc>), anyhow::Error>{
		let args = format!("/modules/{}/genid/{}/certificate/server?api-version=2019-01-30", self.module_id, self.generation_id);
		let expiration = Utc::now().checked_add_signed(chrono::Duration::days(PROXY_SERVER_CERT_VALIDITY_DAYS))
		.context("Error could not generate expiration date for server certificate")?;
		let expiration_str = expiration.to_rfc3339();
		let body = format!("{{\"commonName\":\"{}\", \"expiration\":\"{}\"}}", self.gateway_hostname, expiration_str);
		let url = match self.make_hyper_uri(self.url_scheme, &self.url_base, &args) {
			Ok(url) => url,
			Err(_) => return Err(anyhow::anyhow!("Error")),
		};

		let client = reqwest::blocking::Client::new();
		let resp: ServerCerts = client.post(&url.to_string()).body(body).send()?.json()?;
		
		Ok((resp.certificate, resp.privateKey.bytes, expiration))
	}
}