use std::sync::Arc;

use tokio::sync::Mutex;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState};
use edgelet_settings::DockerConfig;

use crate::deployment::{
    deployment::{DockerSettings, ModuleConfig},
    DeploymentProvider,
};

type ModuleSettings = edgelet_settings::module::Settings<edgelet_settings::DockerConfig>;
type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

pub struct Reconciler<D, M> {
    deployment_provider: Arc<Mutex<D>>,
    runtime: M,
}

impl<D, M> Reconciler<D, M>
where
    D: DeploymentProvider,
    M: ModuleRuntime<Config = DockerConfig>,
{
    pub fn new(deployment_provider: Arc<Mutex<D>>, runtime: M) -> Self {
        Self {
            deployment_provider,
            runtime,
        }
    }

    pub async fn reconcile(&self) -> Result<()> {
        println!("Starting Reconcile");
        println!(
            "Got current modules: {:#?}",
            self.get_current_modules().await
        );
        println!(
            "Got expected modules: {:#?}",
            self.get_expected_modules().await
        );

        // let differance = self.get_differance().await?;

        // for module_to_create in differance.modules_to_create {
        //     self.runtime.create(module_to_create).await.unwrap();
        // }

        Ok(())
    }

    async fn get_differance(&self) -> Result<ModuleDifferance> {
        Ok(Default::default())
    }

    async fn get_expected_modules(&self) -> Result<Vec<PlannedModule>> {
        let provider = self.deployment_provider.lock().await;
        let deployment = if let Some(d) = provider.get_deployment() {
            d
        } else {
            println!("todo: no deployment error");
            return Ok(vec![]);
        };

        let modules = deployment
            .properties
            .desired
            .modules
            .iter()
            .map(|(name, module)| PlannedModule {
                name: name.to_owned(),
                settings: module.to_owned(),
            });

        Ok(vec![])
    }

    async fn get_current_modules(&self) -> Result<Vec<RunningModule>> {
        let modules = self
            .runtime
            .list_with_details()
            .await
            .unwrap()
            .iter()
            .map(|(module, state)| RunningModule {
                name: module.name().to_owned(),
                state: state.to_owned(),
            })
            .collect();

        Ok(modules)
    }
}

#[derive(Default, Debug)]
struct ModuleDifferance {
    modules_to_create: Vec<PlannedModule>,
    modules_to_delete: Vec<RunningModule>,
    state_change_modules: Vec<PlannedModule>,
    failed_modules: Vec<RunningModule>,
}

#[derive(Default, Debug)]
struct RunningModule {
    name: String,
    state: ModuleRuntimeState,
}

#[derive(Default, Debug)]
struct PlannedModule {
    name: String,
    settings: ModuleConfig,
}
