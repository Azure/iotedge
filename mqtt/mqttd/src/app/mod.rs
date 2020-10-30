mod shutdown;
mod snapshot;

cfg_if! {
    if #[cfg(feature = "edgehub")] {
        mod edgehub;

        pub fn new() -> App<edgehub::EdgeHubBootstrap> {
            App::new(edgehub::EdgeHubBootstrap)
        }
    } else {
        mod generic;

        pub fn new() -> App<generic::GenericBootstrap> {
            App::new(generic::GenericBootstrap)
        }
    }
}

use std::path::Path;

use anyhow::{Context, Result};
use async_trait::async_trait;
use cfg_if::cfg_if;
use tracing::{error, info};

use mqtt_broker::{
    auth::Authorizer, Broker, BrokerReady, BrokerSnapshot, FilePersistor, Persist,
    VersionedFileFormat,
};

pub struct App<B>
where
    B: Bootstrap,
{
    bootstrap: B,
    settings: B::Settings,
}

impl<B: Bootstrap> App<B> {
    pub fn new(bootstrap: B) -> Self {
        Self {
            bootstrap,
            settings: B::Settings::default(),
        }
    }

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

    pub async fn run(self) -> Result<()> {
        let broker_ready = BrokerReady::new();

        let (broker, persistor) = self
            .bootstrap
            .make_broker(&self.settings, &broker_ready)
            .await?;

        let snapshot_interval = self.bootstrap.snapshot_interval(&self.settings);
        let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
            snapshot::start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

        let state = self
            .bootstrap
            .run(self.settings, broker, broker_ready)
            .await?;

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

#[async_trait]
pub trait Bootstrap {
    type Settings: Default;

    fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings>;

    type Authorizer: Authorizer + Send + 'static;

    async fn make_broker(
        &self,
        settings: &Self::Settings,
        broker_ready: &BrokerReady,
    ) -> Result<(Broker<Self::Authorizer>, FilePersistor<VersionedFileFormat>)>;

    fn snapshot_interval(&self, settings: &Self::Settings) -> std::time::Duration;

    async fn run(
        self,
        config: Self::Settings,
        broker: Broker<Self::Authorizer>,
        broker_ready: BrokerReady,
    ) -> Result<BrokerSnapshot>;
}
