use std::{
    collections::HashMap,
    convert::{TryFrom, TryInto},
    sync::Arc,
};

use tokio::sync::Mutex;

use edgelet_core::{Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleStatus};
use edgelet_settings::{module::ImagePullPolicy, DockerConfig, ModuleSpec};

use crate::deployment::{
    deployment::{ModuleConfig, ModuleStatus as DeploymentModuleStatus, RestartPolicy},
    DeploymentProvider,
};
use crate::reconcile::module_start_planner::ModuleStartPlanner;

type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync + 'static>>;

pub struct Reconciler<D, M, R> {
    deployment_provider: Arc<Mutex<D>>,
    runtime: M,
    registry: R,
    previous_config: HashMap<String, ModuleConfig>,
    start_planner: ModuleStartPlanner,
}

impl<D, M, R> Reconciler<D, M, R>
where
    D: DeploymentProvider,
    M: ModuleRuntime<Config = DockerConfig>,
    R: ModuleRegistry<Config = DockerConfig>,
{
    pub fn new(deployment_provider: Arc<Mutex<D>>, runtime: M, registry: R) -> Self {
        Self {
            deployment_provider,
            runtime,
            registry,
            previous_config: HashMap::new(),
            start_planner: ModuleStartPlanner::new(),
        }
    }

    pub async fn setup(&mut self) -> Result<()> {
        // Seed the previous config values with the desired values on disk.
        // This should be run before the twin is fetched from the cloud so
        // the values are based on the old twin.
        let previous_config = self
            .get_desired_modules()
            .await?
            .into_iter()
            .map(|m| (m.name, m.config))
            .collect();

        self.previous_config = previous_config;
        Ok(())
    }

    pub async fn reconcile(&mut self) -> Result<()> {
        println!("Starting Reconcile");

        let differance = self.get_differance().await?;

        // Note delete should come first, since hub has a limit of 50 identities
        self.delete_modules(&differance.modules_to_delete).await?;
        self.create_modules(differance.modules_to_create).await?;
        self.stop_modules(&differance.modules_to_stop).await?;

        // use start planner to only apply backoff on restarting modules
        let removed_modules: Vec<String> = differance
            .modules_to_delete
            .into_iter()
            .chain(differance.modules_to_stop.into_iter())
            .collect();
        let modules_to_start = self
            .start_planner
            .get_modules_to_start(&differance.modules_to_start, &removed_modules);
        self.start_modules(&modules_to_start).await?;

        Ok(())
    }

    async fn get_differance(&mut self) -> Result<ModuleDifference> {
        let mut current_modules = self.get_current_modules().await?;
        let desired_modules = self.get_desired_modules().await?;
        log::debug!("Got current modules:\n{:?}", current_modules);
        log::debug!("Got desired modules:\n{:?}", desired_modules);

        let mut modules_to_delete: Vec<String> = Vec::new();
        let mut modules_to_create: Vec<DesiredModule> = Vec::new();
        let mut modules_to_stop: Vec<String> = Vec::new();
        let mut modules_to_start: Vec<String> = Vec::new();

        // This loop will remove all modules in desired modules from current modules, resulting in a list of modules to remove.
        for desired in desired_modules {
            if let Some((_, current)) = current_modules.remove_entry(&desired.name) {
                // Module with same name exists, check if should be modified.

                if self
                    .previous_config
                    .entry(desired.name.clone())
                    .or_default()
                    == &desired.config
                {
                    // If the configuration has not changed since last deployment,
                    // validate that the current state is correct
                    match desired.config.status {
                        DeploymentModuleStatus::Running => {
                            match current.state.status() {
                                ModuleStatus::Running => { /* Do nothing, module is in correct state. */
                                }
                                // Module that should be running is not. This should be marked
                                ModuleStatus::Stopped
                                | ModuleStatus::Failed
                                | ModuleStatus::Unknown
                                | ModuleStatus::Dead => {
                                    // Module is in a bad state, send to restart planner
                                    modules_to_start.push(current.name);
                                }
                            }
                        }
                        DeploymentModuleStatus::Stopped => {
                            if current.state.status() == &ModuleStatus::Running {
                                // Set Module to stopped. It doesn't matter if module is failed, since we're stopping it anyway
                                modules_to_stop.push(current.name);
                            }
                        }
                    }
                } else {
                    // Config has changed, module must be re-created
                    self.previous_config
                        .insert(desired.name.clone(), desired.config.clone());

                    // Module should be modified to match desired, and the change requires a new container
                    modules_to_delete.push(current.name);
                    modules_to_create.push(desired);
                }
            } else {
                // Module doesn't exist, create it.
                modules_to_create.push(desired);
            }
        }

        // If a module is still in the current_modules list at this point, it should be deleted.
        // This is because we have removed all desired modules from the list in the above loop.
        for (name, _) in current_modules {
            modules_to_delete.push(name.to_owned());
        }

        let difference = ModuleDifference {
            modules_to_create,
            modules_to_delete,
            modules_to_stop,
            modules_to_start,
        };
        log::debug!("Found the following differences:\n{:?}", difference);
        Ok(difference)
    }

    async fn get_desired_modules(&self) -> Result<Vec<DesiredModule>> {
        let provider = self.deployment_provider.lock().await;
        let deployment = if let Some(d) = provider.get_deployment() {
            d
        } else {
            println!("todo: no deployment error");
            return Ok(vec![]);
        };

        let mut modules: Vec<DesiredModule> = deployment
            .properties
            .desired
            .modules
            .iter()
            .map(|(name, module)| DesiredModule {
                name: name.clone(),
                config: module.clone(),
            })
            .collect();

        modules.push(DesiredModule {
            name: "edgeHub".to_owned(),
            config: deployment
                .properties
                .desired
                .system_modules
                .edge_hub
                .clone(),
        });

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
                        config: module.config().clone(),
                        state: state.clone(),
                    },
                )
            })
            .collect();

        Ok(modules)
    }

    async fn delete_modules(&self, modules_to_delete: &[String]) -> Result<()> {
        log::debug!(
            "Deleting {} modules: {}",
            modules_to_delete.len(),
            modules_to_delete.join(", ")
        );
        for module in modules_to_delete {
            // TODO: Delete identity in hub

            self.runtime
                .remove(&module)
                .await
                .map_err(|e| format!("Error deleting container {}: {:?}", module, e))?;
        }

        Ok(())
    }

    async fn create_modules(&self, modules_to_create: Vec<DesiredModule>) -> Result<()> {
        log::debug!(
            "Creating {} modules: {}",
            modules_to_create.len(),
            modules_to_create
                .iter()
                .map(|m| m.name.clone())
                .collect::<Vec<String>>()
                .join(", ")
        );
        for module in modules_to_create {
            // TODO: Create identity in hub

            self.pull_and_make_module(module.clone()).await?;
            self.set_module_state(&module.name, &module.config.status)
                .await?;
        }

        Ok(())
    }

    async fn stop_modules(&self, modules_to_stop: &[String]) -> Result<()> {
        log::debug!(
            "Stopping {} modules: {}",
            modules_to_stop.len(),
            modules_to_stop.join(", ")
        );
        for module in modules_to_stop {
            self.set_module_state(&module, &DeploymentModuleStatus::Stopped)
                .await?;
        }

        Ok(())
    }

    async fn start_modules(&self, modules_to_start: &[String]) -> Result<()> {
        log::debug!(
            "Starting {} modules: {}",
            modules_to_start.len(),
            modules_to_start.join(", ")
        );
        for module in modules_to_start {
            self.set_module_state(&module, &DeploymentModuleStatus::Running)
                .await?;
        }

        Ok(())
    }

    async fn pull_and_make_module(&self, module: DesiredModule) -> Result<()> {
        let name = module.name.clone();

        if module.config.image_pull_policy == ImagePullPolicy::OnCreate {
            // TODO: Docker login
            let config: DockerConfig = module.config.clone().try_into()?;
            if let Err(e) = self.registry.pull(&config).await {
                log::warn!(
                    "Could not pull image {}. Attempting to use existing image. Reason: {:?}",
                    config.image(),
                    e
                );
            }
        }

        self.runtime
            .create(module.try_into()?)
            .await
            .map_err(|e| format!("Error creating container {}: {:?}", name, e))?;

        Ok(())
    }

    async fn set_module_state(
        &self,
        id: &str,
        desired_state: &DeploymentModuleStatus,
    ) -> Result<()> {
        match desired_state {
            DeploymentModuleStatus::Running => {
                self.runtime
                    .start(id)
                    .await
                    .map_err(|e| format!("Error starting module {}: {:?}", id, e))?;
            }
            DeploymentModuleStatus::Stopped => {
                // TODO: get stop before kill duration
                self.runtime
                    .stop(id, None)
                    .await
                    .map_err(|e| format!("Error stopping module {}: {:?}", id, e))?;
            }
        }

        Ok(())
    }

    // async fn set_modules_state(&self, modules_to_stop: Vec<StateChangeModule>) -> Result<()> {
    //     log::debug!(
    //         "Changing state for {} modules: {}",
    //         modules_to_stop.len(),
    //         modules_to_stop
    //             .iter()
    //             .map(|m| m.module.name.clone())
    //             .collect::<Vec<String>>()
    //             .join(", ")
    //     );
    //     for state_change_module in modules_to_stop {
    //         let name = &state_change_module.module.name.clone();
    //         if state_change_module.reset_container {
    //             // Module must be removed and restarted
    //             self.runtime
    //                 .remove(name)
    //                 .await
    //                 .map_err(|e| format!("Error deleting container {}: {:?}", name, e))?;

    //             self.pull_and_make_module(state_change_module.module.clone())
    //                 .await?;
    //         }

    //         self.set_module_state(name, &state_change_module.module.config.status)
    //             .await?;
    //     }

    //     Ok(())
    // }

    // async fn handle_modules_to_start(&self, _modules_to_start: Vec<FailedModule>) -> Result<()> {
    //     match desired.config.restart_policy {
    //         RestartPolicy::Always
    //         | RestartPolicy::OnFailure
    //         | RestartPolicy::OnUnhealthy => {
    //             log::debug!(
    //     "Module {} failed with restart policy {}. Adding to health monitor.",
    //     current.name,
    //     desired.config.restart_policy,
    // );
    //             modules_to_start.push(current);
    //         }
    //         RestartPolicy::Never => {
    //             log::debug!(
    //                 r#"Module {} failed but has restart policy "never". No restart will occur."#,
    //                 current.name
    //             );
    //         }
    //     }
    // }
    //     Ok(())
    // }
}

#[derive(Default, Debug)]
struct ModuleDifference {
    modules_to_create: Vec<DesiredModule>,
    modules_to_delete: Vec<String>,
    modules_to_stop: Vec<String>,
    modules_to_start: Vec<String>,
}

#[derive(Default, Debug, Clone)]
struct RunningModule {
    name: String,
    config: DockerConfig,
    state: ModuleRuntimeState,
}

#[derive(Default, Debug)]
struct StateChangeModule {
    module: DesiredModule,
    reset_container: bool,
}

#[derive(Default, Debug)]
struct FailedModule {
    module: RunningModule,
    should_start: bool,
    restart_policy: RestartPolicy,
}

#[derive(Default, Debug, Clone)]
struct DesiredModule {
    name: String,
    config: ModuleConfig,
}

impl TryFrom<DesiredModule> for ModuleSpec<DockerConfig> {
    type Error = Box<dyn std::error::Error + Send + Sync + 'static>;

    fn try_from(module: DesiredModule) -> Result<Self> {
        let spec = ModuleSpec::new(
            module.name,
            module.config.r#type.to_string(),
            module.config.clone().try_into()?,
            // env is already added to the docker create options by the settings.try_into above
            Default::default(),
            module.config.image_pull_policy,
        )?;

        Ok(spec)
    }
}

// #[cfg(test)]
// mod tests {
//     use super::*;

//     use std::{fs::File, path::Path};

//     use docker::models::*;
//     use edgelet_test_utils::runtime::{TestModule, TestModuleRegistry, TestRuntime};

//     use crate::deployment::deployment::*;

//     mod deploy {
//         use super::*;
//         #[tokio::test]
//         async fn runs_without_error() {
//             let _ = simple_logger::SimpleLogger::new().init();
//             let test_file = std::path::Path::new(concat!(
//                 env!("CARGO_MANIFEST_DIR"),
//                 "/src/reconcile/test/basic_sim_temp_deployment.json"
//             ));
//             let provider = TestDeploymentProvider::from_file(test_file);
//             let provider = Arc::new(Mutex::new(provider));

//             let running = TestModule::<DockerConfig> {
//                 name: "SimulatedTemperatureSensor".to_owned(),
//                 ..Default::default()
//             };
//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(running, ModuleRuntimeState::default())],
//                 ..Default::default()
//             };
//             let registry = TestModuleRegistry::<DockerConfig>::default();

//             let mut reconciler = Reconciler::new(provider, runtime, registry);
//             reconciler
//                 .reconcile()
//                 .await
//                 .expect("Could not complete reconcile loop");
//         }

//         #[tokio::test]
//         async fn deploys_module() {
//             let _ = simple_logger::SimpleLogger::new().init();
//             let test_file = std::path::Path::new(concat!(
//                 env!("CARGO_MANIFEST_DIR"),
//                 "/src/reconcile/test/basic_sim_temp_deployment.json"
//             ));
//             let provider = TestDeploymentProvider::from_file(test_file);
//             let provider = Arc::new(Mutex::new(provider));

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![],
//                 ..Default::default()
//             };
//             let registry = TestModuleRegistry::<DockerConfig>::default();

//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             reconciler
//                 .reconcile()
//                 .await
//                 .expect("Could not complete reconcile loop");

//             // Check that images are pulled.
//             let pulls = registry.pulls.lock().expect("Could not aquire pulls mutex");
//             assert_eq!(pulls.len(), 2);

//             let expected_images: &[&str] = &[
//                 "mcr.microsoft.com/azureiotedge-hub:1.2",
//                 "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0",
//             ];
//             let mut actual_images: Vec<&str> = pulls.iter().map(|c| c.image()).collect();
//             actual_images.sort();
//             assert_eq!(expected_images, &actual_images);

//             // Check that containers are created
//             let created = runtime
//                 .created
//                 .lock()
//                 .expect("Could not aquire created mutex");
//             assert_eq!(created.len(), 2);

//             let mut actual_images: Vec<&str> = created.iter().map(|c| c.config().image()).collect();
//             actual_images.sort();
//             assert_eq!(expected_images, &actual_images);

//             // Check that containers are started
//             let started = runtime
//                 .started
//                 .lock()
//                 .expect("Could not aquire started mutex");
//             assert_eq!(started.len(), 2);

//             let expected_names: &[&str] = &["SimulatedTemperatureSensor", "edgeHub"];
//             let mut actual_names: Vec<&str> = started.iter().map(String::as_str).collect();
//             actual_names.sort();
//             assert_eq!(expected_names, &actual_names);

//             // Check that no containers are stopped or removed
//             let stopped = runtime
//                 .stopped
//                 .lock()
//                 .expect("Could not aquire stopped mutex");
//             assert_eq!(stopped.len(), 0);
//             let removed = runtime
//                 .removed
//                 .lock()
//                 .expect("Could not aquire removed mutex");
//             assert_eq!(removed.len(), 0);
//         }
//     }

//     mod difference {
//         use super::*;
//         use std::collections::BTreeMap;

//         #[tokio::test]
//         async fn no_change() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("basic_sim_temp_deployment.json");

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(sim_temp_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_stop.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn create_module() {
//             let (provider, registry, _, _) = setup("basic_sim_temp_deployment.json");

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider.clone(), &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_create.len(), 2); // Edgehub and Sim Temp
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_stop.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn start_module() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("basic_sim_temp_deployment.json");

//             let stop_sim_temp_state = sim_temp_state.with_status(ModuleStatus::Stopped);

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(sim_temp_module, stop_sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             assert_eq!(difference.modules_to_stop[0].reset_container, false);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );
//             assert_eq!(
//                 difference.modules_to_stop[0].module.config.status,
//                 DeploymentModuleStatus::Running,
//             );

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn stop_module() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("stopped_sim_temp_deployment.json");

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(sim_temp_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             assert_eq!(difference.modules_to_stop[0].reset_container, false);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn change_module_image() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("basic_sim_temp_deployment.json");

//             let mut image_change_module = sim_temp_module;
//             image_change_module.config = image_change_module
//                 .config
//                 .with_image("this is the old module image. It should be replaced".to_string());

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(image_change_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             // Since a container config value was changed, the container must be reset
//             assert_eq!(difference.modules_to_stop[0].reset_container, true);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );
//             // The image should have been replaced with the image in the basic_sim_temp_deployment.json
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.config.settings.image,
//                 "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0"
//             );

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn change_docker_config() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("binding_sim_temp_deployment.json");

//             let mut docker_config_change_module = sim_temp_module;
//             docker_config_change_module.config =
//                 docker_config_change_module.config.with_create_options(
//                     ContainerCreateBody::new().with_host_config(
//                         HostConfig::new()
//                             .with_binds(vec!["5000:5000".to_string(), "443:443".to_string()]),
//                     ),
//                 );

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(docker_config_change_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             // Since a container config value was changed, the container must be reset
//             assert_eq!(difference.modules_to_stop[0].reset_container, true);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );
//             // The binds should be set to empty like in the deployment
//             assert_eq!(
//                 difference.modules_to_stop[0]
//                     .module
//                     .config
//                     .settings
//                     .create_option
//                     .create_options
//                     .as_ref()
//                     .expect("Create options should not be empty")
//                     .host_config()
//                     .expect("Host config should not be empty")
//                     .binds(),
//                 None
//             );
//             // The port bindings should be have the new deployments value
//             difference.modules_to_stop[0]
//                 .module
//                 .config
//                 .settings
//                 .create_option
//                 .create_options
//                 .as_ref()
//                 .expect("Create options should not be empty")
//                 .host_config()
//                 .expect("Host config should not be empty")
//                 .port_bindings()
//                 .expect("Port Bindings should not be empty");

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn set_docker_env() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("env_sim_temp_deployment.json");

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(sim_temp_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             // Since a container config value was changed, the container must be reset
//             assert_eq!(difference.modules_to_stop[0].reset_container, true);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );
//             // The new env should be set
//             let expected: BTreeMap<String, EnvHolder> = [
//                 (
//                     "Variable1".to_owned(),
//                     EnvHolder {
//                         value: EnvValue::Number(5.0),
//                     },
//                 ),
//                 (
//                     "Variable2".to_owned(),
//                     EnvHolder {
//                         value: EnvValue::String("Hello".to_owned()),
//                     },
//                 ),
//             ]
//             .iter()
//             .cloned()
//             .collect();
//             assert_eq!(difference.modules_to_stop[0].module.config.env, expected);

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn remove_docker_env() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("basic_sim_temp_deployment.json");

//             let mut remove_env_module = sim_temp_module;
//             remove_env_module.config = remove_env_module.config.with_create_options(
//                 ContainerCreateBody::new()
//                     .with_env(vec!["Variable1=5".to_owned(), "Variable2=Hello".to_owned()]),
//             );

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(remove_env_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             assert_eq!(difference.modules_to_stop.len(), 1);
//             // Since a container config value was changed, the container must be reset
//             assert_eq!(difference.modules_to_stop[0].reset_container, true);
//             assert_eq!(
//                 &difference.modules_to_stop[0].module.name,
//                 "SimulatedTemperatureSensor"
//             );
//             // The old env should be removed
//             assert_eq!(
//                 difference.modules_to_stop[0].module.config.env,
//                 Default::default()
//             );

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         #[tokio::test]
//         async fn keep_docker_env() {
//             let (provider, registry, sim_temp_module, sim_temp_state) =
//                 setup("env_sim_temp_deployment.json");

//             let mut env_module = sim_temp_module;
//             env_module.config = env_module.config.with_create_options(
//                 ContainerCreateBody::new()
//                     .with_env(vec!["Variable1=5".to_owned(), "Variable2=Hello".to_owned()]),
//             );

//             let runtime = TestRuntime::<DockerConfig> {
//                 module_details: vec![(env_module, sim_temp_state)],
//                 ..Default::default()
//             };
//             let mut reconciler = Reconciler::new(provider, &runtime, &registry);
//             let difference = reconciler
//                 .get_differance()
//                 .await
//                 .expect("Error getting difference");

//             // State should not change since deployment matches running
//             assert_eq!(difference.modules_to_stop.len(), 0);

//             assert_eq!(difference.modules_to_create.len(), 1); // Edgehub
//             assert_eq!(difference.modules_to_delete.len(), 0);
//             assert_eq!(difference.modules_to_start.len(), 0);
//         }

//         fn setup(
//             file: &str,
//         ) -> (
//             Arc<Mutex<TestDeploymentProvider>>,
//             TestModuleRegistry<DockerConfig>,
//             TestModule<DockerConfig>,
//             ModuleRuntimeState,
//         ) {
//             let _ = simple_logger::SimpleLogger::new().init();
//             let test_file = format!("{}/src/reconcile/test/{}", env!("CARGO_MANIFEST_DIR"), file);
//             let provider = TestDeploymentProvider::from_file(test_file);
//             let provider = Arc::new(Mutex::new(provider));
//             let registry = TestModuleRegistry::<DockerConfig>::default();

//             let sim_temp_module = TestModule::<DockerConfig> {
//                 name: "SimulatedTemperatureSensor".to_owned(),
//                 config: serde_json::from_str(
//                     r#"{"image": "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0"}"#,
//                 )
//                 .unwrap(),
//                 ..Default::default()
//             };
//             let sim_temp_state = ModuleRuntimeState::default().with_status(ModuleStatus::Running);

//             (provider, registry, sim_temp_module, sim_temp_state)
//         }
//     }

//     struct TestDeploymentProvider {
//         pub deployment: Option<Deployment>,
//     }

//     impl TestDeploymentProvider {
//         fn from_file<P>(path: P) -> Self
//         where
//             P: AsRef<Path>,
//         {
//             let file = File::open(path).expect("Could not read test deployment");
//             let deployment =
//                 serde_json::from_reader(&file).expect("Could not parse test deployment");

//             Self { deployment }
//         }
//     }

//     impl DeploymentProvider for TestDeploymentProvider {
//         fn get_deployment(&self) -> Option<&Deployment> {
//             self.deployment.as_ref()
//         }
//     }
// }
