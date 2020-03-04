use std::fs::{self, OpenOptions};
use std::path::PathBuf;

use async_trait::async_trait;
use failure::ResultExt;
use tracing::debug;

use crate::error::{Error, ErrorKind};
use crate::BrokerState;

static STATE_DEFAULT_STEM: &str = "state";
static STATE_EXTENSION: &str = "dat";

#[async_trait]
pub trait Persist {
    type Error: Into<Error>;

    async fn load(&mut self) -> Result<BrokerState, Self::Error>;

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error>;
}

pub struct NullPersistor;

#[async_trait]
impl Persist for NullPersistor {
    type Error = Error;

    async fn load(&mut self) -> Result<BrokerState, Self::Error> {
        Ok(BrokerState::default())
    }

    async fn store(&mut self, _: BrokerState) -> Result<(), Self::Error> {
        Ok(())
    }
}

pub struct FilePersistor {
    dir: PathBuf,
}

impl FilePersistor {
    pub fn new<P: Into<PathBuf>>(dir: P) -> Self {
        FilePersistor { dir: dir.into() }
    }
}

#[async_trait]
impl Persist for FilePersistor {
    type Error = Error;

    async fn load(&mut self) -> Result<BrokerState, Self::Error> {
        let dir = self.dir.clone();
        tokio::task::spawn_blocking(move || {
            let path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            if path.exists() {
                let file = OpenOptions::new()
                    .read(true)
                    .open(path)
                    .context(ErrorKind::General)?;
                let state = bincode::deserialize_from(file).context(ErrorKind::General)?;
                Ok(state)
            } else {
                Ok(BrokerState::default())
            }
        })
        .await
        .context(ErrorKind::TaskJoin)?
    }

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error> {
        let dir = self.dir.clone();
        tokio::task::spawn_blocking(move || {
            let path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            debug!("opening {} for writing state...", path.display());
            let file = OpenOptions::new()
                .create(true)
                .write(true)
                .open(&path)
                .context(ErrorKind::General)?;
            debug!("{} opened.", path.display());
            if let Err(e) = bincode::serialize_into(file, &state).context(ErrorKind::General) {
                fs::remove_file(path).context(ErrorKind::General)?;
                return Err(e.into());
            }
            Ok(())
        })
        .await
        .context(ErrorKind::TaskJoin)?
    }
}
