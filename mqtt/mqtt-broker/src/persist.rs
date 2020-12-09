#[cfg(unix)]
use std::os::unix::fs::symlink;
#[cfg(windows)]
use std::os::windows::fs::symlink_file;
use std::{
    cmp,
    collections::HashMap,
    error::Error as StdError,
    fs::{self, OpenOptions},
    io::{Read, Write},
    iter::FromIterator,
    path::PathBuf,
};

use async_trait::async_trait;
use bytes::Bytes;
use chrono::{DateTime, Utc};
use fail::fail_point;
use flate2::{read::GzDecoder, write::GzEncoder, Compression};
use serde::{ser::SerializeMap, Deserialize, Deserializer, Serialize, Serializer};
use tracing::{debug, info, info_span};

use mqtt3::proto::Publication;

use crate::{subscription::Subscription, BrokerSnapshot, ClientInfo, SessionSnapshot};

/// sets the number of past states to save - 2 means we save the current and the pervious
const STATE_DEFAULT_PREVIOUS_COUNT: usize = 2;
static STATE_DEFAULT_STEM: &str = "state";
static STATE_EXTENSION: &str = "dat";

#[async_trait]
pub trait Persist {
    type Error: StdError;

    async fn load(&mut self) -> Result<Option<BrokerSnapshot>, Self::Error>;

    async fn store(&mut self, state: BrokerSnapshot) -> Result<(), Self::Error>;
}

/// A persistor that does nothing.
#[derive(Debug)]
pub struct NullPersistor;

#[async_trait]
impl Persist for NullPersistor {
    type Error = PersistError;

    async fn load(&mut self) -> Result<Option<BrokerSnapshot>, Self::Error> {
        Ok(None)
    }

    async fn store(&mut self, _: BrokerSnapshot) -> Result<(), Self::Error> {
        Ok(())
    }
}

/// An abstraction over the broker state's file format.
pub trait FileFormat {
    type Error: StdError;

    /// Load `BrokerState` from a reader.
    fn load<R: Read>(&self, reader: R) -> Result<BrokerSnapshot, Self::Error>;

    /// Store `BrokerState` to a writer.
    fn store<W: Write>(&self, writer: W, state: BrokerSnapshot) -> Result<(), Self::Error>;
}

/// Loads/stores the broker state to a file defined by `FileFormat`.
///
/// The `FilePersistor` works by storing to a new state file and then updating
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
    previous_count: usize,

    /// seq is bumped on every store to disambiguate stores in the same timestamp
    seq: u16,
}

impl<F> FilePersistor<F> {
    pub fn new<P: Into<PathBuf>>(dir: P, format: F) -> Self {
        FilePersistor {
            dir: dir.into(),
            format,
            previous_count: STATE_DEFAULT_PREVIOUS_COUNT,
            seq: 0,
        }
    }

    /// Sets the number of past states to save
    ///
    /// The intent is to allow rollback to a pervious state.
    /// The default is `2`, which saves the current and previous state.
    /// The minimum value is `1` (the current state).
    pub fn with_previous_count(mut self, previous_count: usize) -> Self {
        self.previous_count = cmp::max(1, previous_count);
        self
    }
}

/// Root structure that is being serialized/deserialized.
/// Used to support versioning of persisted state.
///
/// Every inner data structure must implement Into/From `BrokerSnapshot` in back-compat manner.
#[derive(Deserialize, Serialize)]
enum VersionedState {
    V1(ConsolidatedState),
}

impl From<BrokerSnapshot> for VersionedState {
    fn from(state: BrokerSnapshot) -> Self {
        VersionedState::V1(state.into())
    }
}

impl From<VersionedState> for BrokerSnapshot {
    fn from(state: VersionedState) -> Self {
        match state {
            VersionedState::V1(state) => state.into(),
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct VersionedFileFormat;

impl FileFormat for VersionedFileFormat {
    type Error = PersistError;

    fn load<R: Read>(&self, reader: R) -> Result<BrokerSnapshot, Self::Error> {
        let decoder = GzDecoder::new(reader);
        fail_point!("bincodeformat.load.deserialize_from", |_| {
            Err(PersistError::Deserialize(None))
        });

        let state: VersionedState =
            bincode::deserialize_from(decoder).map_err(|e| PersistError::Deserialize(Some(e)))?;

        Ok(state.into())
    }

    fn store<W: Write>(&self, writer: W, state: BrokerSnapshot) -> Result<(), Self::Error> {
        let state: VersionedState = state.into();

        let encoder = GzEncoder::new(writer, Compression::default());
        fail_point!("bincodeformat.store.serialize_into", |_| {
            Err(PersistError::Serialize(None))
        });
        bincode::serialize_into(encoder, &state).map_err(|e| PersistError::Serialize(Some(e)))
    }
}

#[derive(Deserialize, Serialize)]
struct ConsolidatedState {
    #[serde(serialize_with = "serialize_payloads")]
    #[serde(deserialize_with = "deserialize_payloads")]
    payloads: HashMap<u64, Bytes>,
    retained: HashMap<String, SimplifiedPublication>,
    sessions: Vec<ConsolidatedSession>,
}

impl From<BrokerSnapshot> for ConsolidatedState {
    fn from(state: BrokerSnapshot) -> Self {
        let (retained, sessions) = state.into_parts();

        #[allow(clippy::mutable_key_type)]
        let mut payloads = HashMap::new();

        let mut shrink_payload = |publication: Publication| {
            let next_id = payloads.len() as u64;

            let id = *payloads.entry(publication.payload).or_insert(next_id);

            SimplifiedPublication {
                topic_name: publication.topic_name,
                qos: publication.qos,
                retain: publication.retain,
                payload: id,
            }
        };

        let retained = retained
            .into_iter()
            .map(|(topic, publication)| (topic, shrink_payload(publication)))
            .collect();

        let sessions = sessions
            .into_iter()
            .map(|session| {
                let (client_info, subscriptions, waiting_to_be_sent, last_active) =
                    session.into_parts();

                #[allow(clippy::redundant_closure)] // removing closure leads to borrow error
                let waiting_to_be_sent = waiting_to_be_sent
                    .into_iter()
                    .map(|publication| shrink_payload(publication))
                    .collect();

                ConsolidatedSession {
                    client_info,
                    subscriptions,
                    waiting_to_be_sent,
                    last_active,
                }
            })
            .collect();

        // Note that while payloads are consolidated using a Hashmap<Byte, u64>, they are stored as a Hashmap<u64, Byte>.
        // This makes consolidation much faster
        let payloads = payloads
            .drain()
            .map(|(payload, id)| (id, payload))
            .collect();

        ConsolidatedState {
            payloads,
            retained,
            sessions,
        }
    }
}

impl From<ConsolidatedState> for BrokerSnapshot {
    fn from(state: ConsolidatedState) -> Self {
        let ConsolidatedState {
            payloads,
            retained,
            sessions,
        } = state;

        let expand_payload = |publication: SimplifiedPublication| Publication {
            topic_name: publication.topic_name,
            qos: publication.qos,
            retain: publication.retain,
            payload: payloads
                .get(&publication.payload)
                .expect("corrupted data")
                .clone(),
        };

        let retained = retained
            .into_iter()
            .map(|(topic, publication)| (topic, expand_payload(publication)))
            .collect();

        let sessions = sessions
            .into_iter()
            .map(|session| {
                let waiting_to_be_sent = session
                    .waiting_to_be_sent
                    .into_iter()
                    .map(expand_payload)
                    .collect();
                SessionSnapshot::from_parts(
                    session.client_info,
                    session.subscriptions,
                    waiting_to_be_sent,
                    session.last_active,
                )
            })
            .collect();

        BrokerSnapshot::new(retained, sessions)
    }
}

#[derive(Deserialize, Serialize)]
struct ConsolidatedSession {
    client_info: ClientInfo,
    subscriptions: HashMap<String, Subscription>,
    waiting_to_be_sent: Vec<SimplifiedPublication>,
    last_active: DateTime<Utc>,
}

#[derive(Deserialize, Serialize)]
struct SimplifiedPublication {
    topic_name: String,
    qos: crate::proto::QoS,
    retain: bool,
    payload: u64,
}

fn serialize_payloads<S>(payloads: &HashMap<u64, Bytes>, serializer: S) -> Result<S::Ok, S::Error>
where
    S: Serializer,
{
    let mut map = serializer.serialize_map(Some(payloads.len()))?;
    for (k, v) in payloads {
        map.serialize_entry(k, &v[..])?;
    }

    map.end()
}

fn deserialize_payloads<'de, D>(deserializer: D) -> Result<HashMap<u64, Bytes>, D::Error>
where
    D: Deserializer<'de>,
{
    let payloads = HashMap::<u64, Vec<u8>>::deserialize(deserializer)?
        .into_iter()
        .map(|(k, v)| (k, Bytes::from(v)));
    let payloads = HashMap::from_iter(payloads);
    Ok(payloads)
}

#[async_trait]
impl<F> Persist for FilePersistor<F>
where
    F: FileFormat<Error = PersistError> + Clone + Send + 'static,
{
    type Error = PersistError;

    async fn load(&mut self) -> Result<Option<BrokerSnapshot>, Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();

        let res = tokio::task::spawn_blocking(move || {
            let path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            if path.exists() {
                info!("loading state from file {}.", path.display());

                fail_point!("filepersistor.load.fileopen", |_| {
                    Err(PersistError::FileOpen(path.clone(), None))
                });
                let file = OpenOptions::new()
                    .read(true)
                    .open(&path)
                    .map_err(|e| PersistError::FileOpen(path.clone(), Some(e)))?;

                fail_point!("filepersistor.load.format", |_| {
                    Err(PersistError::Deserialize(None))
                });
                let state = format.load(file)?;
                Ok(Some(state))
            } else {
                info!("no state file found at {}.", path.display());
                Ok(None)
            }
        })
        .await;

        fail_point!("filepersistor.load.spawn_blocking", |_| {
            Err(PersistError::TaskJoin(None))
        });
        res.map_err(|e| PersistError::TaskJoin(Some(e)))?
    }

    #[allow(clippy::too_many_lines)]
    async fn store(&mut self, state: BrokerSnapshot) -> Result<(), Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();
        let previous_count = self.previous_count;

        self.seq = self.seq.wrapping_add(1);
        let seq = self.seq;

        let res = tokio::task::spawn_blocking(move || {
            let span = info_span!("persistor", dir = %dir.display());
            let _guard = span.enter();

            if !dir.exists() {
                fail_point!("filepersistor.store.createdir", |_| {
                    Err(PersistError::CreateDir(dir.clone(), None))
                });
                fs::create_dir_all(&dir)
                    .map_err(|e| PersistError::CreateDir(dir.clone(), Some(e)))?;
            }

            let link_path = dir.join(format!("{}.{}", STATE_DEFAULT_STEM, STATE_EXTENSION));
            let temp_link_path =
                dir.join(format!("{}.{}.tmp", STATE_DEFAULT_STEM, STATE_EXTENSION));

            let path = dir.join(format!(
                "{}.{}-{:05}.{}",
                STATE_DEFAULT_STEM,
                chrono::Utc::now().format("%Y%m%d%H%M%S%3f"),
                seq,
                STATE_EXTENSION
            ));

            info!(message="persisting state...", file=%path.display());
            debug!("opening {} for writing state...", path.display());
            fail_point!("filepersistor.store.fileopen", |_| {
                Err(PersistError::FileOpen(path.clone(), None))
            });
            let file = OpenOptions::new()
                .create(true)
                .write(true)
                .open(&path)
                .map_err(|e| PersistError::FileOpen(path.clone(), Some(e)))?;
            debug!("{} opened.", path.display());

            debug!("persisting state to {}...", path.display());
            match format.store(file, state) {
                Ok(_) => {
                    debug!("state persisted to {}.", path.display());

                    // Swap the symlink
                    //   - remove the old link if it exists
                    //   - link the new file
                    if temp_link_path.exists() {
                        fail_point!("filepersistor.store.symlink_unlink", |_| {
                            Err(PersistError::SymlinkUnlink(temp_link_path.clone(), None))
                        });
                        fs::remove_file(&temp_link_path).map_err(|e| {
                            PersistError::SymlinkUnlink(temp_link_path.clone(), Some(e))
                        })?;
                    }

                    debug!("linking {} to {}", temp_link_path.display(), path.display());

                    fail_point!("filepersistor.store.symlink", |_| {
                        Err(PersistError::Symlink(
                            temp_link_path.clone(),
                            path.clone(),
                            None,
                        ))
                    });

                    #[cfg(unix)]
                    symlink(&path, &temp_link_path).map_err(|e| {
                        PersistError::Symlink(temp_link_path.clone(), path.clone(), Some(e))
                    })?;

                    #[cfg(windows)]
                    symlink_file(&path, &temp_link_path).map_err(|e| {
                        PersistError::Symlink(temp_link_path.clone(), path.clone(), Some(e))
                    })?;

                    // Commit the updated link by renaming the temp link.
                    // This is the so-called "capistrano" trick for atomically updating links
                    // https://github.com/capistrano/capistrano/blob/d04c1e3ea33e84b183d056b71c7cacf7744ce7ad/lib/capistrano/tasks/deploy.rake
                    fail_point!("filepersistor.store.filerename", |_| {
                        Err(PersistError::FileRename(
                            temp_link_path.clone(),
                            path.clone(),
                            None,
                        ))
                    });
                    fs::rename(&temp_link_path, &link_path).map_err(|e| {
                        PersistError::Symlink(temp_link_path.clone(), path.clone(), Some(e))
                    })?;

                    // Prune old states
                    fail_point!("filepersistor.store.readdir", |_| {
                        Err(PersistError::ReadDir(dir.clone(), None))
                    });

                    let mut entries = fs::read_dir(&dir)
                        .map_err(|e| (PersistError::ReadDir(dir.clone(), Some(e))))?
                        .filter_map(Result::ok)
                        .filter(|entry| entry.file_type().ok().map_or(false, |f| f.is_file()))
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

                    for entry in entries.iter().skip(previous_count) {
                        debug!(
                            "pruning old state file {}...",
                            entry.file_name().to_string_lossy()
                        );

                        fail_point!("filepersistor.store.entry_unlink", |_| {
                            Err(PersistError::FileUnlink(entry.path(), None))
                        });
                        fs::remove_file(&entry.path())
                            .map_err(|e| PersistError::FileUnlink(entry.path(), Some(e)))?;
                        debug!("{} pruned.", entry.file_name().to_string_lossy());
                    }
                }
                Err(e) => {
                    fail_point!("filepersistor.store.new_file_unlink", |_| {
                        Err(PersistError::FileUnlink(path.clone(), None))
                    });
                    fs::remove_file(&path)
                        .map_err(|e| (PersistError::FileUnlink(path, Some(e))))?;
                    return Err(e);
                }
            }
            info!(message="persisted state.", file=%path.display());
            Ok(())
        })
        .await;

        fail_point!("filepersistor.load.spawn_blocking", |_| {
            Err(PersistError::TaskJoin(None))
        });
        res.map_err(|e| PersistError::TaskJoin(Some(e)))?
    }
}

#[derive(Debug, thiserror::Error)]
pub enum PersistError {
    #[error("failed to open file {0}")]
    FileOpen(PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to rename file {0} to {}")]
    FileRename(PathBuf, PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to remove file {0}")]
    FileUnlink(PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to create state directory {0}")]
    CreateDir(PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to read contents of directory {0}")]
    ReadDir(PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to create symlink from {0} to {1}")]
    Symlink(PathBuf, PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to remove symlink {0}")]
    SymlinkUnlink(PathBuf, #[source] Option<std::io::Error>),

    #[error("failed to serialize state")]
    Serialize(#[source] Option<bincode::Error>),

    #[error("failed to deserialize state")]
    Deserialize(#[source] Option<bincode::Error>),

    #[error("An error occurred joining a task.")]
    TaskJoin(#[source] Option<tokio::task::JoinError>),
}

#[cfg(all(test, target_arch = "x86_64"))]
mod tests {
    use std::io::Cursor;

    use proptest::prelude::*;
    use tempfile::TempDir;

    use crate::{
        persist::{FileFormat, FilePersistor, Persist, VersionedFileFormat},
        proptest::arb_broker_snapshot,
        BrokerSnapshot,
    };

    proptest! {
        #[test]
        fn broker_state_versioned_file_format_persistance_test(state in arb_broker_snapshot()) {
            let (expected_retained, expected_sessions) = state.clone().into_parts();
            let format = VersionedFileFormat;
            let mut buffer = vec![0_u8; 10 * 1024 * 1024];
            let writer = Cursor::new(&mut buffer);
            format.store(writer, state).unwrap();

            let reader = Cursor::new(buffer);
            let state = format.load(reader).unwrap();
            let (result_retained, result_sessions) = state.into_parts();

            prop_assert_eq!(expected_retained, result_retained);
            prop_assert_eq!(expected_sessions, result_sessions);
        }
    }

    #[tokio::test]
    async fn filepersistor_smoketest() {
        let tmp_dir = TempDir::new().unwrap();
        let path = tmp_dir.path().to_owned();
        let mut persistor = FilePersistor::new(path, VersionedFileFormat::default());

        persistor.store(BrokerSnapshot::default()).await.unwrap();
        let state = persistor.load().await.unwrap().unwrap();
        assert_eq!(BrokerSnapshot::default(), state);
    }
}
