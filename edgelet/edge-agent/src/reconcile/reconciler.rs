use std::path::PathBuf;

use edgelet_core::ModuleRuntime;
use edgelet_settings::DockerConfig;

use crate::deployment::DeploymentManager;

type ModuleSettings = edgelet_settings::module::Settings<edgelet_settings::DockerConfig>;
type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

pub struct Reconciler<M> {
    deployment_file: PathBuf,
    runtime: M,
}

impl<M> Reconciler<M>
where
    M: ModuleRuntime<Config = DockerConfig>,
{
    pub fn new(deployment_file: PathBuf, runtime: M) -> Self {
        Self {
            deployment_file,
            runtime,
        }
    }

    pub async fn reconcile(&self) -> Result<()> {
        let differance = self.get_differance().await?;

        for module_to_create in differance.modules_to_create {
            self.runtime.create(module_to_create).await.unwrap();
        }

        Ok(())
    }

    async fn get_differance(&self) -> Result<ModuleDifferance> {
        Ok(Default::default())
    }

    async fn get_expected_modules(&self) -> Result<Vec<ModuleSettings>> {
        let deployment = DeploymentManager::get_deployment(&self.deployment_file).await;

        Ok(vec![])
    }

    async fn get_current_modules(&self) -> Result<Vec<ModuleSettings>> {
        Ok(vec![])
    }
}

#[derive(Default)]
struct ModuleDifferance {
    modules_to_create: Vec<ModuleSettings>,
    modules_to_delete: Vec<ModuleSettings>,
    state_change_modules: Vec<ModuleSettings>,
    failed_modules: Vec<ModuleSettings>,
}
