use std::fs::{self, OpenOptions};
use std::io::{Read, Write};
#[cfg(unix)]
use std::os::unix::fs::symlink;
#[cfg(windows)]
use std::os::windows::fs::symlink_file;
use std::path::PathBuf;
use std::{cmp, fmt};

use async_trait::async_trait;
use fail::fail_point;
use failure::ResultExt;
use flate2::read::GzDecoder;
use flate2::write::GzEncoder;
use flate2::Compression;
use tracing::{debug, info, span, Level};

use crate::error::{Error, ErrorKind};
use crate::BrokerState;

/// sets the number of past states to save - 2 means we save the current and the pervious
const STATE_DEFAULT_COUNT: usize = 2;
static STATE_DEFAULT_STEM: &str = "state";
static STATE_EXTENSION: &str = "dat";

#[async_trait]
pub trait Persist {
    type Error: Into<Error>;

    async fn load(&mut self) -> Result<Option<BrokerState>, Self::Error>;

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error>;
}

#[derive(Debug)]
pub struct NullPersistor;

#[async_trait]
impl Persist for NullPersistor {
    type Error = Error;

    async fn load(&mut self) -> Result<Option<BrokerState>, Self::Error> {
        Ok(None)
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

/// Loads/stores the broker state to a file defined by `FileFormat`.
///
/// The FilePersistor works by storing to a new state file and then updating
/// a symlink to `state.dat`.
///
/// Loading always reads from `state.dat`. This allows for saving multiple
/// copies of the state and rollback in case something gets corrupted.
/// It also aids in "transactionally" saving the state. Updating the symlink
/// "commits" the changes. This is an attempt to prevent corrupting the state
/// file in case the process crashes in the middle of writing.
#[derive(Debug)]
pub struct FilePersistor<F> {
    dir: PathBuf,
    format: F,
    count: usize,
}

impl<F> FilePersistor<F> {
    pub fn new<P: Into<PathBuf>>(dir: P, format: F) -> Self {
        FilePersistor {
            dir: dir.into(),
            format,
            count: STATE_DEFAULT_COUNT,
        }
    }

    /// Sets the number of past states to save
    ///
    /// The intent is to allow rollback to a pervious state.
    /// The default is `2`, which saves the current and previous state.
    /// The minimum value is `1` (the current state).
    pub fn with_count(mut self, count: usize) -> Self {
        self.count = cmp::max(1, count);
        self
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
        fail_point!("bincodeformat.load.deserialize_from", |_| {
            Err(Error::from(ErrorKind::Persist(ErrorReason::Deserialize)))
        });
        let state = bincode::deserialize_from(decoder)
            .context(ErrorKind::Persist(ErrorReason::Deserialize))?;
        Ok(state)
    }

    fn store<W: Write>(&self, writer: W, state: BrokerState) -> Result<(), Self::Error> {
        let encoder = GzEncoder::new(writer, Compression::default());
        fail_point!("bincodeformat.store.serialize_into", |_| {
            Err(Error::from(ErrorKind::Persist(ErrorReason::Deserialize)))
        });
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

    async fn load(&mut self) -> Result<Option<BrokerState>, Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();

        let res = tokio::task::spawn_blocking(move || {
            let path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            if path.exists() {
                info!("loading state from file {}.", path.display());

                fail_point!("filepersistor.load.fileopen", |_| {
                    Err(Error::from(ErrorKind::Persist(ErrorReason::FileOpen)))
                });
                let file = OpenOptions::new()
                    .read(true)
                    .open(path)
                    .context(ErrorKind::Persist(ErrorReason::FileOpen))?;

                fail_point!("filepersistor.load.format", |_| {
                    Err(Error::from(ErrorKind::Persist(ErrorReason::Serialize)))
                });
                let state = format.load(file).map_err(|e| e.into())?;
                Ok(Some(state))
            } else {
                info!("no state file found at {}.", path.display());
                Ok(None)
            }
        })
        .await;

        fail_point!("filepersistor.load.spawn_blocking", |_| {
            Err(Error::from(ErrorKind::TaskJoin))
        });
        res.context(ErrorKind::TaskJoin)?
    }

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();
        let count = self.count;

        let res = tokio::task::spawn_blocking(move || {
            let span = span!(Level::INFO, "persistor", dir = %dir.display());
            let _guard = span.enter();

            if !dir.exists() {
                fail_point!("filepersistor.store.createdir", |_| {
                    Err(Error::from(ErrorKind::Persist(ErrorReason::CreateDir)))
                });
                fs::create_dir_all(&dir).context(ErrorKind::Persist(ErrorReason::CreateDir))?;
            }

            let link_path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            let temp_link_path =
                dir.join(format!("{}.{}.tmp", STATE_DEFAULT_STEM, STATE_EXTENSION));

            let path = dir.join(format!(
                "{}.{}.{}",
                STATE_DEFAULT_STEM,
                chrono::Utc::now().format("%Y%m%d%H%M%S%6f"),
                STATE_EXTENSION
            ));

            info!(message="persisting state...", file=%path.display());
            debug!("opening {} for writing state...", path.display());
            fail_point!("filepersistor.store.fileopen", |_| {
                Err(Error::from(ErrorKind::Persist(ErrorReason::FileOpen)))
            });
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
                    //   - remove the old link if it exists
                    //   - link the new file
                    if temp_link_path.exists() {
                        fail_point!("filepersistor.store.symlink_unlink", |_| {
                            Err(Error::from(ErrorKind::Persist(ErrorReason::SymlinkUnlink)))
                        });
                        fs::remove_file(&temp_link_path)
                            .context(ErrorKind::Persist(ErrorReason::SymlinkUnlink))?;
                    }

                    debug!("linking {} to {}", temp_link_path.display(), path.display());

                    fail_point!("filepersistor.store.symlink", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::Symlink)))
                    });

                    #[cfg(unix)]
                    symlink(&path, &temp_link_path)
                        .context(ErrorKind::Persist(ErrorReason::Symlink))?;

                    #[cfg(windows)]
                    symlink_file(&path, &temp_link_path)
                        .context(ErrorKind::Persist(ErrorReason::Symlink))?;

                    // Commit the updated link by renaming the temp link.
                    // This is the so-called "capistrano" trick for atomically updating links
                    // https://github.com/capistrano/capistrano/blob/d04c1e3ea33e84b183d056b71c7cacf7744ce7ad/lib/capistrano/tasks/deploy.rake
                    fail_point!("filepersistor.store.filerename", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::FileRename)))
                    });
                    fs::rename(&temp_link_path, &link_path)
                        .context(ErrorKind::Persist(ErrorReason::FileRename))?;

                    // Prune old states
                    fail_point!("filepersistor.store.readdir", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::ReadDir)))
                    });

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

                    for entry in entries.iter().skip(count) {
                        debug!(
                            "pruning old state file {}...",
                            entry.file_name().to_string_lossy()
                        );

                        fail_point!("filepersistor.store.entry_unlink", |_| {
                            Err(Error::from(ErrorKind::Persist(ErrorReason::FileUnlink)))
                        });
                        fs::remove_file(&entry.path())
                            .context(ErrorKind::Persist(ErrorReason::FileUnlink))?;
                        debug!("{} pruned.", entry.file_name().to_string_lossy());
                    }
                }
                Err(e) => {
                    fail_point!("filepersistor.store.new_file_unlink", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::FileUnlink)))
                    });
                    fs::remove_file(path).context(ErrorKind::Persist(ErrorReason::FileUnlink))?;
                    return Err(e.into());
                }
            }
            info!(message="persisted state.", file=%path.display());
            Ok(())
        })
        .await;

        fail_point!("filepersistor.store.spawn_blocking", |_| {
            Err(Error::from(ErrorKind::TaskJoin))
        });
        res.context(ErrorKind::TaskJoin)?
    }
}

#[derive(Debug, PartialEq)]
pub enum ErrorReason {
    FileOpen,
    FileRename,
    FileUnlink,
    CreateDir,
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
            ErrorReason::FileRename => write!(f, "failed to rename file"),
            ErrorReason::FileUnlink => write!(f, "failed to remove file"),
            ErrorReason::CreateDir => write!(f, "failed to create state directory"),
            ErrorReason::ReadDir => write!(f, "failed to read contents of directory"),
            ErrorReason::Symlink => write!(f, "failed to create symlink"),
            ErrorReason::SymlinkUnlink => write!(f, "failed to remove symlink"),
            ErrorReason::Serialize => write!(f, "failed to serialize state"),
            ErrorReason::Deserialize => write!(f, "failed to deserialize state"),
        }
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;

    use proptest::prelude::*;
    use std::io::Cursor;

    use crate::broker::tests::arb_broker_state;

    proptest! {
        #[test]
        fn bincode_roundtrip(state in arb_broker_state()) {
            let expected = state.clone();
            let format = BincodeFormat;
            let mut buffer = vec![0u8; 10 * 1024 * 1024];
            let writer = Cursor::new(&mut buffer);
            format.store(writer, state).unwrap();

            let reader = Cursor::new(buffer);
            let state = format.load(reader).unwrap();
            prop_assert_eq!(expected, state);
        }
    }
}
