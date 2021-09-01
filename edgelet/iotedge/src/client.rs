use std::time::Duration;

use failure::ResultExt;
use hyper::{Body, Client, Uri};
use url::Url;

use edgelet_core::{
    LogOptions, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, SystemInfo,
    SystemResources, UrlExt,
};
use edgelet_http::{ListModulesResponse, ModuleDetails};
use edgelet_settings::module::Settings as ModuleSpec;
use http_common::{request_with_headers, request_with_headers_no_content, Connector};

use crate::{Error, ErrorKind};
type Result<T> = std::result::Result<T, Error>;

const API_VERSION: &str = "2020-07-07";

#[derive(serde::Serialize, Clone)]
pub struct MgmtConfig {}

pub struct MgmtModule {
    pub details: ModuleDetails,
    pub image: String,
}

pub struct MgmtClient {
    client: Client<Connector, Body>,
    host: String,
}

impl MgmtClient {
    pub fn new(url: &Url) -> Result<Self> {
        let client: Client<_, Body> = Client::builder()
            .build(Connector::new(url).map_err(|e| Error::from(ErrorKind::Misc(e.to_string())))?);

        let base_path = url
            .to_base_path()
            .context(ErrorKind::ModuleRuntime)?
            .to_str()
            .ok_or(ErrorKind::ModuleRuntime)?
            .to_string();
        let host = hex::encode(base_path.as_bytes());

        Ok(Self { client, host })
    }

    fn get_uri(&self, path: &str) -> Result<Uri> {
        let host_str = format!("unix://{}:0{}", self.host, path);
        let uri: std::result::Result<Uri, _> = host_str.parse();
        let uri = uri.context(ErrorKind::ModuleRuntime)?;

        Ok(uri)
    }
}

#[async_trait::async_trait]
impl ModuleRuntime for MgmtClient {
    type Error = Error;
    type Config = MgmtConfig;
    type Module = MgmtModule;
    type ModuleRegistry = Self;

    async fn restart(&self, id: &str) -> Result<()> {
        let path = format!("/modules/{}/restart?api-version={}", id, API_VERSION);
        let uri = self.get_uri(&path)?;
        request_with_headers_no_content(
            &self.client,
            hyper::http::Method::POST,
            uri,
            None,
            None::<&()>,
        )
        .await
        .context(ErrorKind::ModuleRuntime)?;

        Ok(())
    }

    async fn list(&self) -> Result<Vec<Self::Module>> {
        let path = format!("/modules?api-version={}", API_VERSION);
        let uri = self.get_uri(&path)?;
        let response: ListModulesResponse = request_with_headers(
            &self.client,
            hyper::http::Method::GET,
            uri,
            None,
            None::<&()>,
        )
        .await
        .context(ErrorKind::ModuleRuntime)?;

        let modules = response.modules.into_iter().map(MgmtModule::new).collect();
        Ok(modules)
    }

    async fn list_with_details(&self) -> Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        let modules = self
            .list()
            .await?
            .into_iter()
            .map(|module| (module, ModuleRuntimeState::default()))
            .collect();

        Ok(modules)
    }

    async fn logs(&self, id: &str, options: &LogOptions) -> Result<hyper::Body> {
        let uri = {
            let mut query = ::url::form_urlencoded::Serializer::new(String::new());
            query
                .append_pair("api-version", API_VERSION)
                .append_pair("follow", &options.follow().to_string())
                .append_pair("tail", &options.tail().to_string())
                .append_pair("timestamps", &options.timestamps().to_string())
                .append_pair("since", &options.since().to_string());
            if let Some(until) = options.until() {
                query.append_pair("until", &until.to_string());
            }
            let query = query.finish();
            let path = format!("/modules/{}/logs?{}", id, query);
            self.get_uri(&path)?
        };

        let req = hyper::Request::builder()
            .method(hyper::Method::GET)
            .uri(uri)
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");
        let resp = self
            .client
            .request(req)
            .await
            .context(ErrorKind::ModuleRuntime)?;

        let (hyper::http::response::Parts { status, .. }, body) = resp.into_parts();
        if status.is_success() {
            Ok(body)
        } else {
            Err(Error::from(ErrorKind::Misc(format!(
                "Bad status code when calling logs: {}",
                status
            ))))
        }
    }

    async fn create(&self, _module: ModuleSpec<Self::Config>) -> Result<()> {
        unimplemented!()
    }
    async fn get(&self, _id: &str) -> Result<(Self::Module, ModuleRuntimeState)> {
        unimplemented!()
    }
    async fn start(&self, _id: &str) -> Result<()> {
        unimplemented!()
    }
    async fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Result<()> {
        unimplemented!()
    }
    async fn remove(&self, _id: &str) -> Result<()> {
        unimplemented!()
    }
    async fn system_info(&self) -> Result<SystemInfo> {
        unimplemented!()
    }
    async fn system_resources(&self) -> Result<SystemResources> {
        unimplemented!()
    }
    async fn remove_all(&self) -> Result<()> {
        unimplemented!()
    }
    async fn stop_all(&self, _wait_before_kill: Option<Duration>) -> Result<()> {
        unimplemented!()
    }
    async fn module_top(&self, _id: &str) -> Result<Vec<i32>> {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }
}

#[async_trait::async_trait]
impl Module for MgmtModule {
    type Config = MgmtConfig;
    type Error = Error;

    fn name(&self) -> &str {
        &self.details.name
    }
    fn type_(&self) -> &str {
        &self.details.r#type
    }
    fn config(&self) -> &Self::Config {
        &MgmtConfig {}
    }

    async fn runtime_state(&self) -> Result<ModuleRuntimeState> {
        unimplemented!();
    }
}

#[async_trait::async_trait]
impl ModuleRegistry for MgmtClient {
    type Error = Error;
    type Config = MgmtConfig;

    async fn pull(&self, _config: &Self::Config) -> Result<()> {
        Ok(())
    }

    async fn remove(&self, _name: &str) -> Result<()> {
        Ok(())
    }
}

impl MgmtModule {
    pub fn new(details: ModuleDetails) -> Self {
        let image = if let Ok(docker_config) = serde_json::from_value::<
            edgelet_settings::DockerConfig,
        >(details.config.settings.clone())
        {
            docker_config.image().to_owned()
        } else {
            "".to_owned()
        };

        Self { details, image }
    }
}
