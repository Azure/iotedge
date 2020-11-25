mod cleanup;
mod shutdown;
mod snapshot;

cfg_if! {
    if #[cfg(feature = "edgehub")] {
        mod edgehub;

        pub fn new() -> App<edgehub::EdgeHubBootstrap> {
            App::new(edgehub::EdgeHubBootstrap::default())
        }
    } else {
        mod generic;

        pub fn new() -> App<generic::GenericBootstrap> {
            App::new(generic::GenericBootstrap::default())
        }
    }
}

use std::{path::Path, time::Duration};

use anyhow::{Context, Result};
use async_trait::async_trait;
use cfg_if::cfg_if;
use tracing::{error, info};

use mqtt_broker::{
    auth::Authorizer, Broker, BrokerSnapshot, FilePersistor, Persist, VersionedFileFormat,
};

/// Main entrypoint to the app.
pub struct App<B>
where
    B: Bootstrap,
{
    bootstrap: B,
    settings: B::Settings,
}

impl<B> App<B>
where
    B: Bootstrap,
{
    /// Returns a new instance of the app.
    pub fn new(bootstrap: B) -> Self {
        Self {
            bootstrap,
            settings: B::Settings::default(),
        }
    }

    /// Configures app with settings.
    pub fn setup<P>(&mut self, config_path: P) -> Result<()>
    where
        P: AsRef<Path>,
    {
        self.settings = self
            .bootstrap
            .load_config(config_path)
            .context("An error occurred loading configuration.")?;
        Ok(())
    }

    /// Starts up all routines and runs MQTT server.
    pub async fn run(self) -> Result<()> {
        let (broker, persistor) = self.bootstrap.make_broker(&self.settings).await?;

        let snapshot_interval = self.bootstrap.snapshot_interval(&self.settings);
        let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
            snapshot::start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

        let expiration = self.bootstrap.session_expiration(&self.settings);
        let cleanup_interval = self.bootstrap.session_cleanup_interval(&self.settings);
        cleanup::start_cleanup(broker.handle(), cleanup_interval, expiration).await?;

        let state = self.bootstrap.run(self.settings, broker).await?;

        snapshotter_shutdown_handle.shutdown().await?;
        let mut persistor = snapshotter_join_handle.await?;
        info!("state snapshotter shutdown.");

        info!("persisting state before exiting...");
        persistor.store(state).await?;
        info!("state persisted.");

        info!("exiting... goodbye");
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
#[error("An error occurred loading configuration.")]
pub struct LoadConfigurationError;

/// Defines a common steps for an app to start.
#[async_trait]
pub trait Bootstrap {
    /// A type describing app configuration.
    type Settings: Default;

    /// Reads configuration from the file on disk and returns settings.
    fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings>;

    /// An `Authorizer` type.
    type Authorizer: Authorizer + Send + 'static;

    /// Creates a new instance of the `Broker` configured for the app.
    async fn make_broker(
        &self,
        settings: &Self::Settings,
    ) -> Result<(Broker<Self::Authorizer>, FilePersistor<VersionedFileFormat>)>;

    /// Returns update interval for snapshotter.
    fn snapshot_interval(&self, settings: &Self::Settings) -> Duration;

    /// Returns session expiration.
    fn session_expiration(&self, settings: &Self::Settings) -> Duration;

    /// Returns session cleanup interval.
    fn session_cleanup_interval(&self, settings: &Self::Settings) -> Duration;

    /// Runs all configured routines: MQTT server, sidecars, etc..
    async fn run(
        self,
        config: Self::Settings,
        broker: Broker<Self::Authorizer>,
    ) -> Result<BrokerSnapshot>;
}
