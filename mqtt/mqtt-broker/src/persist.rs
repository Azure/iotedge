use std::fs::{self, OpenOptions};
use std::io::{Read, Write};
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
use tracing::{debug, info, span, Level};

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

#[derive(Debug)]
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

pub trait FileFormat {
    type Error: Into<Error>;

    fn load<R: Read>(&self, reader: R) -> Result<BrokerState, Self::Error>;
    fn store<W: Write>(&self, writer: W, state: BrokerState) -> Result<(), Self::Error>;
}

#[derive(Debug)]
pub struct FilePersistor<F> {
    dir: PathBuf,
    format: F,
}

impl<F> FilePersistor<F> {
    pub fn new<P: Into<PathBuf>>(dir: P, format: F) -> Self {
        FilePersistor {
            dir: dir.into(),
            format,
        }
    }
}

#[derive(Clone, Debug)]
pub struct BincodeFormat;

impl BincodeFormat {
    pub fn new() -> Self {
        Self
    }
}

impl FileFormat for BincodeFormat {
    type Error = Error;

    fn load<R: Read>(&self, reader: R) -> Result<BrokerState, Self::Error> {
        let decoder = GzDecoder::new(reader);
        let state = bincode::deserialize_from(decoder)
            .context(ErrorKind::Persist(ErrorReason::Deserialize))?;
        Ok(state)
    }

    fn store<W: Write>(&self, writer: W, state: BrokerState) -> Result<(), Self::Error> {
        let encoder = GzEncoder::new(writer, Compression::default());
        bincode::serialize_into(encoder, &state)
            .context(ErrorKind::Persist(ErrorReason::Serialize))?;
        Ok(())
    }
}

#[async_trait]
impl<F> Persist for FilePersistor<F>
where
    F: FileFormat + Clone + Send + 'static,
{
    type Error = Error;

    async fn load(&mut self) -> Result<BrokerState, Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();
        tokio::task::spawn_blocking(move || {
            let path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            if path.exists() {
                info!("loading state from file {}.", path.display());
                let file = OpenOptions::new()
                    .read(true)
                    .open(path)
                    .context(ErrorKind::Persist(ErrorReason::FileOpen))?;
                let state = format.load(file).map_err(|e| e.into())?;
                Ok(state)
            } else {
                info!(
                    "no state file found at {}. initializing with empty state.",
                    path.display()
                );
                Ok(BrokerState::default())
            }
        })
        .await
        .context(ErrorKind::TaskJoin)?
    }

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();
        tokio::task::spawn_blocking(move || {
            let span = span!(Level::INFO, "persistor", dir = %dir.display());
            let _guard = span.enter();

            let default_path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            let path = dir.join(format!(
                "{}.{}.{}",
                STATE_DEFAULT_STEM,
                chrono::Utc::now().format("%Y-%m-%dT%H:%M:%S%.3f%z"),
                STATE_EXTENSION
            ));

            info!(message="persisting state...", file=%path.display());
            debug!("opening {} for writing state...", path.display());
            let file = OpenOptions::new()
                .create(true)
                .write(true)
                .open(&path)
                .context(ErrorKind::Persist(ErrorReason::FileOpen))?;
            debug!("{} opened.", path.display());

            debug!("persisting state to {}...", path.display());
            match format.store(file, state).map_err(|e| e.into()) {
                Ok(_) => {
                    debug!("state persisted to {}.", path.display());

                    // Swap the symlink
                    //   - remove the old link if exists
                    //   - link the new file
                    if default_path.exists() {
                        fs::remove_file(&default_path)
                            .context(ErrorKind::Persist(ErrorReason::SymlinkUnlink))?;
                    }

                    debug!("linking {} to {}", default_path.display(), path.display());

                    #[cfg(unix)]
                    symlink(&path, &default_path)
                        .context(ErrorKind::Persist(ErrorReason::Symlink))?;

                    #[cfg(windows)]
                    symlink_file(&path, &default_path)
                        .context(ErrorKind::Persist(ErrorReason::Symlink))?;

                    // Prune old states
                    let mut entries = fs::read_dir(&dir)
                        .context(ErrorKind::Persist(ErrorReason::ReadDir))?
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
                            .context(ErrorKind::Persist(ErrorReason::FileUnlink))?;
                        debug!("{} pruned.", entry.file_name().to_string_lossy());
                    }
                }
                Err(e) => {
                    fs::remove_file(path).context(ErrorKind::Persist(ErrorReason::FileUnlink))?;
                    return Err(e.into());
                }
            }
            info!(message="persisted state.", file=%path.display());
            Ok(())
        })
        .await
        .context(ErrorKind::TaskJoin)?
    }
}

#[derive(Debug, PartialEq)]
pub enum ErrorReason {
    FileOpen,
    FileUnlink,
    ReadDir,
    Symlink,
    SymlinkUnlink,
    Serialize,
    Deserialize,
}

impl fmt::Display for ErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ErrorReason::FileOpen => write!(f, "failed to open file"),
            ErrorReason::FileUnlink => write!(f, "failed to remove file"),
            ErrorReason::ReadDir => write!(f, "failed to read contents of directory"),
            ErrorReason::Symlink => write!(f, "failed to create symlink"),
            ErrorReason::SymlinkUnlink => write!(f, "failed to remove symlink"),
            ErrorReason::Serialize => write!(f, "failed to serialize state"),
            ErrorReason::Deserialize => write!(f, "failed to deserialize state"),
        }
    }
}
