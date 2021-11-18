use std::sync::Arc;

use tokio::sync::Mutex;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState};
use edgelet_settings::DockerConfig;

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

#[cfg(test)]
mod tests {
    use super::*;

    use std::{fs::File, path::Path};

    use edgelet_test_utils::runtime::TestRuntime;

    use crate::deployment::deployment::Deployment;

    #[tokio::test]
    async fn test1() {
        let test_file = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/src/reconcile/test/deployment1.json"
        ));
        let provider = TestDeploymentProvider::from_file(test_file);
        let provider = Arc::new(Mutex::new(provider));

        let runtime = TestRuntime::<DockerConfig> {
            ..Default::default()
        };

        let reconciler = Reconciler::new(provider, runtime);
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
