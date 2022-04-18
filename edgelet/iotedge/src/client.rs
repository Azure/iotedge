use std::time::Duration;

use anyhow::Context;
use hyper::Uri;
use url::Url;

use edgelet_core::{
    LogOptions, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, SystemInfo,
    SystemResources, UrlExt,
};
use edgelet_http::{ListModulesResponse, ModuleDetails};
use edgelet_settings::module::Settings as ModuleSpec;
use http_common::{Connector, ErrorBody, HttpRequest};

use crate::error::Error;

const API_VERSION: &str = "2020-07-07";

#[derive(serde::Serialize, Clone)]
pub struct MgmtConfig {}

pub struct MgmtModule {
    pub details: ModuleDetails,
    pub image: String,
}

pub struct MgmtClient {
    connector: Connector,
    host: String,
}

impl MgmtClient {
    pub fn new(url: &Url) -> anyhow::Result<Self> {
        let connector = Connector::new(url).map_err(|e| Error::Misc(e.to_string()))?;

        let base_path = url
            .to_base_path()
            .context(Error::ModuleRuntime)?
            .to_str()
            .ok_or(Error::ModuleRuntime)?
            .to_string();
        let host = hex::encode(base_path.as_bytes());

        Ok(Self { connector, host })
    }

    fn get_uri(&self, path: &str) -> anyhow::Result<String> {
        let host_str = format!("unix://{}:0{}", self.host, path);
        let uri: std::result::Result<Uri, _> = host_str.parse();
        let uri = uri.context(Error::ModuleRuntime)?;
        let uri = uri.to_string();

        Ok(uri)
    }
}

#[async_trait::async_trait]
impl ModuleRuntime for MgmtClient {
    type Config = MgmtConfig;
    type Module = MgmtModule;
    type ModuleRegistry = Self;

    async fn restart(&self, id: &str) -> anyhow::Result<()> {
        let path = format!("/modules/{}/restart?api-version={}", id, API_VERSION);
        let uri = self.get_uri(&path)?;

        let request: HttpRequest<(), _> = HttpRequest::post(self.connector.clone(), &uri, None);

        request
            .no_content_response()
            .await
            .context(Error::ModuleRuntime)?;

        Ok(())
    }

    async fn list(&self) -> anyhow::Result<Vec<Self::Module>> {
        let path = format!("/modules?api-version={}", API_VERSION);
        let uri = self.get_uri(&path)?;

        let request: HttpRequest<(), _> = HttpRequest::get(self.connector.clone(), &uri);

        let response = request
            .json_response()
            .await
            .context(Error::ModuleRuntime)?;
        let response = response
            .parse_expect_ok::<ListModulesResponse, ErrorBody<'_>>()
            .context(Error::ModuleRuntime)?;

        let modules = response.modules.into_iter().map(MgmtModule::new).collect();
        Ok(modules)
    }

    async fn list_with_details(&self) -> anyhow::Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        let modules = self
            .list()
            .await?
            .into_iter()
            .map(|module| (module, ModuleRuntimeState::default()))
            .collect();

        Ok(modules)
    }

    async fn logs(&self, id: &str, options: &LogOptions) -> anyhow::Result<hyper::Body> {
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
        let client = self.connector.clone().into_client();
        let resp = client.request(req).await.context(Error::ModuleRuntime)?;

        let (hyper::http::response::Parts { status, .. }, body) = resp.into_parts();
        if status.is_success() {
            Ok(body)
        } else {
            Err(Error::Misc(format!("Bad status code when calling logs: {}", status)).into())
        }
    }

    async fn create(&self, _module: ModuleSpec<Self::Config>) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn get(&self, _id: &str) -> anyhow::Result<(Self::Module, ModuleRuntimeState)> {
        unimplemented!()
    }
    async fn start(&self, _id: &str) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn remove(&self, _id: &str) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn system_info(&self) -> anyhow::Result<SystemInfo> {
        unimplemented!()
    }
    async fn system_resources(&self) -> anyhow::Result<SystemResources> {
        unimplemented!()
    }
    async fn remove_all(&self) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn stop_all(&self, _wait_before_kill: Option<Duration>) -> anyhow::Result<()> {
        unimplemented!()
    }
    async fn module_top(&self, _id: &str) -> anyhow::Result<Vec<i32>> {
        unimplemented!()
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        unimplemented!()
    }
}

#[async_trait::async_trait]
impl Module for MgmtModule {
    type Config = MgmtConfig;

    fn name(&self) -> &str {
        &self.details.name
    }
    fn type_(&self) -> &str {
        &self.details.r#type
    }
    fn config(&self) -> &Self::Config {
        &MgmtConfig {}
    }

    async fn runtime_state(&self) -> anyhow::Result<ModuleRuntimeState> {
        unimplemented!();
    }
}

#[async_trait::async_trait]
impl ModuleRegistry for MgmtClient {
    type Config = MgmtConfig;

    async fn pull(&self, _config: &Self::Config) -> anyhow::Result<()> {
        Ok(())
    }

    async fn remove(&self, _name: &str) -> anyhow::Result<()> {
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
