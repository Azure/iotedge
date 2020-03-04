use std::fs::{self, OpenOptions};
#[cfg(unix)]
use std::os::unix::fs::symlink;
#[cfg(windows)]
use std::os::windows::fs::symlink_file;
use std::path::PathBuf;
use std::{cmp, fmt};

use async_trait::async_trait;
use failure::ResultExt;
use flate2::read::GzDecoder;
use flate2::write::GzEncoder;
use flate2::Compression;
use tracing::debug;

use crate::error::{Error, ErrorKind};
use crate::BrokerState;

/// sets the number of past states to save - 2 means we save the current and the pervious
const STATE_COUNT: usize = 2;
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
                    .context(ErrorKind::Persist(ErrorReason::Blah))?;
                let decoder = GzDecoder::new(file);
                let state = bincode::deserialize_from(decoder)
                    .context(ErrorKind::Persist(ErrorReason::Blah))?;
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
            let default_path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            let path = dir.join(format!(
                "{}.{}.{}",
                STATE_DEFAULT_STEM,
                chrono::Utc::now().format("%Y-%m-%dT%H:%M:%S%.3f%z"),
                STATE_EXTENSION
            ));
            debug!("opening {} for writing state...", path.display());
            let file = OpenOptions::new()
                .create(true)
                .write(true)
                .open(&path)
                .context(ErrorKind::Persist(ErrorReason::Blah))?;
            debug!("{} opened.", path.display());

            debug!("persisting state to {}...", path.display());
            let encoder = GzEncoder::new(file, Compression::default());
            match bincode::serialize_into(encoder, &state)
                .context(ErrorKind::Persist(ErrorReason::Blah))
            {
                Ok(_) => {
                    debug!("state persisted to {}.", path.display());

                    // Swap the symlink
                    //   - remove the old link if exists
                    //   - link the new file
                    if default_path.exists() {
                        fs::remove_file(&default_path)
                            .context(ErrorKind::Persist(ErrorReason::Blah))?;
                    }

                    debug!("linking {} to {}", default_path.display(), path.display());

                    #[cfg(unix)]
                    symlink(&path, &default_path).context(ErrorKind::Persist(ErrorReason::Blah))?;

                    #[cfg(windows)]
                    symlink_file(&path, &default_path)
                        .context(ErrorKind::Persist(ErrorReason::Blah))?;

                    // Prune old states
                    let mut entries = fs::read_dir(&dir)
                        .context(ErrorKind::Persist(ErrorReason::Blah))?
                        .filter_map(|maybe_entry| maybe_entry.ok())
                        .filter(|entry| {
                            entry.file_type().ok().map(|e| e.is_file()).unwrap_or(false)
                        })
                        .filter(|entry| {
                            entry
                                .file_name()
                                .to_string_lossy()
                                .starts_with(STATE_DEFAULT_STEM)
                        })
                        .collect::<Vec<fs::DirEntry>>();

                    entries.sort_unstable_by(|a, b| {
                        b.file_name()
                            .partial_cmp(&a.file_name())
                            .unwrap_or(cmp::Ordering::Equal)
                    });

                    for entry in entries.iter().skip(STATE_COUNT) {
                        debug!(
                            "pruning old state file {}...",
                            entry.file_name().to_string_lossy()
                        );
                        fs::remove_file(entry.file_name())
                            .context(ErrorKind::Persist(ErrorReason::Blah))?;
                        debug!("{} pruned.", entry.file_name().to_string_lossy());
                    }
                }
                Err(e) => {
                    fs::remove_file(path).context(ErrorKind::Persist(ErrorReason::Blah))?;
                    return Err(e.into());
                }
            }
            Ok(())
        })
        .await
        .context(ErrorKind::TaskJoin)?
    }
}

#[derive(Debug, PartialEq)]
pub enum ErrorReason {
    Blah,
}

impl fmt::Display for ErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ErrorReason::Blah => write!(f, "blah"),
        }
    }
}
