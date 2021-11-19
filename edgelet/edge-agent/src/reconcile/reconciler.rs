use std::{collections::HashMap, sync::Arc};

use tokio::sync::Mutex;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState};
use edgelet_settings::{DockerConfig, ModuleSpec};

use crate::deployment::{
    deployment::{DockerSettings, ModuleConfig},
    DeploymentProvider,
};

type ModuleSettings = edgelet_settings::module::Settings<edgelet_settings::DockerConfig>;
type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync + 'static>>;

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

        let _differance = self.get_differance().await?;

        // for module_to_create in differance.modules_to_create {
        //     self.runtime.create(module_to_create).await.unwrap();
        // }

        Ok(())
    }

    async fn get_differance(&self) -> Result<ModuleDifferance> {
        let mut current_modules = self.get_current_modules().await?;
        let desired_modules = self.get_desired_modules().await?;
        println!("Got current modules: {:?}", current_modules);
        println!("Got desired modules: {:?}", desired_modules);

        let mut modules_to_create: Vec<DesiredModule> = Vec::new();
        let mut modules_to_delete: Vec<RunningModule> = Vec::new();
        let mut state_change_modules: Vec<DesiredModule> = Vec::new();
        let mut failed_modules: Vec<RunningModule> = Vec::new();

        // This loop will remove all modules in desired modules from current modules, resulting in a list of modules to remove.
        for desired in desired_modules {
            if let Some((_, current)) = current_modules.remove_entry(&desired.name) {
                // Module with same name exists, check if should be modified.

                // For this part, maybe make state change a type with container remove a bool

                // if true { // Check create options/env vars
                //    // module must be removed and re-created
                // } else if &desired.settings.status != current.state.status() {

                // }else {
                //     // No change needed
                // }
            } else {
                // Module doesn't exist, create it.
                modules_to_create.push(desired);
            }
        }

        // If a module is still in the current_modules list at this point, it should be deleted.
        modules_to_delete.extend(current_modules.into_iter().map(|(_, m)| m));

        Ok(Default::default())
    }

    async fn get_desired_modules(&self) -> Result<Vec<DesiredModule>> {
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
            .map(|(name, module)| DesiredModule {
                name: name.to_owned(),
                config: module.to_owned(),
            })
            .collect();

        Ok(modules)
    }

    async fn get_current_modules(&self) -> Result<HashMap<String, RunningModule>> {
        let modules = self
            .runtime
            .list_with_details()
            .await
            .unwrap()
            .iter()
            .map(|(module, state)| {
                (
                    module.name().to_owned(),
                    RunningModule {
                        name: module.name().to_owned(),
                        config: module.config().to_owned(),
                        state: state.to_owned(),
                    },
                )
            })
            .collect();

        Ok(modules)
    }

    async fn create_modules(&self, modules_to_create: Vec<DesiredModule>) -> Result<()> {
        for module in modules_to_create {
            let name = module.name.clone();
            let runtime_module = ModuleSpec::new(
                module.name,
                module.config.r#type.to_string(),
                module.config.settings.into(),
                module.config.env,
                module.config.image_pull_policy,
            )?;

            // TODO: Create identity in hub

            self.runtime
                .create(runtime_module)
                .await
                .map_err(|e| format!("Error creating module {}: {}", name, e))?;
        }

        Ok(())
    }
}

#[derive(Default, Debug)]
struct ModuleDifferance {
    modules_to_create: Vec<DesiredModule>,
    modules_to_delete: Vec<RunningModule>,
    state_change_modules: Vec<DesiredModule>,
    failed_modules: Vec<RunningModule>,
}

#[derive(Default, Debug)]
struct RunningModule {
    name: String,
    config: DockerConfig,
    state: ModuleRuntimeState,
}

#[derive(Default, Debug)]
struct DesiredModule {
    name: String,
    config: ModuleConfig,
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::{fs::File, path::Path};

    use edgelet_test_utils::runtime::{TestModule, TestRuntime};

    use crate::deployment::deployment::Deployment;

    #[tokio::test]
    async fn test1() {
        let test_file = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/src/reconcile/test/deployment1.json"
        ));
        let provider = TestDeploymentProvider::from_file(test_file);
        let provider = Arc::new(Mutex::new(provider));

        let temp_sensor = TestModule::<DockerConfig> {
            name: "SimulatedTemperatureSensor".to_owned(),
            ..Default::default()
        };
        let runtime = TestRuntime::<DockerConfig> {
            module_details: vec![(temp_sensor, ModuleRuntimeState::default())],
            ..Default::default()
        };

        let reconciler = Reconciler::new(provider, runtime);
        reconciler.reconcile().await.unwrap();
    }

    struct TestDeploymentProvider {
        deployment: Option<Deployment>,
    }

    impl TestDeploymentProvider {
        fn from_file<P>(path: P) -> Self
        where
            P: AsRef<Path>,
        {
            let file = File::open(path).expect("Could not read test deployment");
            let deployment =
                serde_json::from_reader(&file).expect("Could not parse test deployment");

            Self { deployment }
        }
    }

    impl DeploymentProvider for TestDeploymentProvider {
        fn get_deployment(&self) -> Option<&Deployment> {
            self.deployment.as_ref()
        }
    }
}
