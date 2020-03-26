#![allow(dead_code)]
#![allow(unused_imports)]

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
const STATE_DEFAULT_PREVIOUS_COUNT: usize = 2;
static STATE_DEFAULT_STEM: &str = "state";
static STATE_EXTENSION: &str = "dat";

#[async_trait]
pub trait Persist {
    type Error: Into<Error>;

    async fn load(&mut self) -> Result<Option<BrokerState>, Self::Error>;

    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error>;
}

/// A persistor that does nothing.
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

/// An abstraction over the broker state's file format.
pub trait FileFormat {
    type Error: Into<Error>;

    /// Load `BrokerState` from a reader.
    fn load<R: Read>(&self, reader: R) -> Result<BrokerState, Self::Error>;

    /// Store `BrokerState` to a writer.
    fn store<W: Write>(&self, writer: W, state: BrokerState) -> Result<(), Self::Error>;
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

/// A simple format based on Bincode and serde
#[derive(Clone, Debug, Default)]
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

use crate::session::SessionState;
use crate::subscription::Subscription;
use crate::ClientId;
use bytes::Bytes;
use mqtt3::proto::Publication;
use serde::ser::SerializeMap;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::collections::hash_map::DefaultHasher;
use std::collections::{HashMap, VecDeque};
use std::hash::{Hash, Hasher};
use std::iter::FromIterator;

pub struct ConsolidatedStateFormat;

impl ConsolidatedStateFormat {
    pub fn new() -> Self {
        Self
    }
}

impl FileFormat for ConsolidatedStateFormat {
    type Error = Error;

    fn load<R: Read>(&self, reader: R) -> Result<BrokerState, Self::Error> {
        let decoder = GzDecoder::new(reader);
        fail_point!("bincodeformat.load.deserialize_from", |_| {
            Err(Error::from(ErrorKind::Persist(ErrorReason::Deserialize)))
        });
        let state = bincode::deserialize_from(decoder)
            .context(ErrorKind::Persist(ErrorReason::Deserialize))?;

        let state = Resolver::resolve_state(state);
        Ok(state)
    }

    fn store<W: Write>(&self, writer: W, state: BrokerState) -> Result<(), Self::Error> {
        let state = Consolidator::consolidate_state(state);

        let encoder = GzEncoder::new(writer, Compression::default());
        fail_point!("bincodeformat.store.serialize_into", |_| {
            Err(Error::from(ErrorKind::Persist(ErrorReason::Deserialize)))
        });
        bincode::serialize_into(encoder, &state)
            .context(ErrorKind::Persist(ErrorReason::Serialize))?;
        Ok(())
    }
}

struct Consolidator {
    payloads: HashMap<Bytes, u64>,
    curr_id: u64,
}

impl Consolidator {
    fn consolidate_state(state: BrokerState) -> ConsolidatedState {
        let mut this = Self {
            payloads: HashMap::new(),
            curr_id: 0,
        };

        let retained = state
            .retained
            .into_iter()
            .map(|(k, v)| (k, this.consolidate_publication(v)));
        let retained = HashMap::from_iter(retained);

        let sessions = state
            .sessions
            .into_iter()
            .map(|s| this.consolidate_session(s))
            .collect();

        let payloads = this.payloads.into_iter().map(|(k, v)| (v, k));
        let payloads = HashMap::from_iter(payloads);

        ConsolidatedState {
            payloads,
            retained,
            sessions,
        }
    }

    fn consolidate_publication(&mut self, publication: Publication) -> SimplifiedPublication {
        SimplifiedPublication {
            topic_name: publication.topic_name,
            retain: publication.retain,
            qos: publication.qos,
            payload: self.get_id(publication.payload),
        }
    }

    fn consolidate_session(&mut self, session: SessionState) -> ConsolidatedSession {
        ConsolidatedSession {
            client_id: session.client_id,
            subscriptions: session.subscriptions,
            waiting_to_be_sent: session
                .waiting_to_be_sent
                .into_iter()
                .map(|p| self.consolidate_publication(p))
                .collect(),
        }
    }

    fn get_id(&mut self, bytes: Bytes) -> u64 {
        if let Some(id) = self.payloads.get(&bytes) {
            *id
        } else {
            self.curr_id += 1;
            let id = self.curr_id;
            self.payloads.insert(bytes, id);
            id
        }
    }
}

struct Resolver {
    payloads: HashMap<u64, Bytes>,
}

impl Resolver {
    fn resolve_state(state: ConsolidatedState) -> BrokerState {
        let mut this = Self {
            payloads: state.payloads,
        };

        let retained = state
            .retained
            .into_iter()
            .map(|(k, v)| (k, this.resolve_publication(v)));
        let retained = HashMap::from_iter(retained);

        let sessions = state
            .sessions
            .into_iter()
            .map(|s| this.resolve_session(s))
            .collect();

        BrokerState { retained, sessions }
    }

    fn resolve_publication(&mut self, publication: SimplifiedPublication) -> Publication {
        Publication {
            topic_name: publication.topic_name,
            retain: publication.retain,
            qos: publication.qos,
            payload: self.get_payload(publication.payload),
        }
    }

    fn resolve_session(&mut self, session: ConsolidatedSession) -> SessionState {
        let mut result = SessionState::new(session.client_id);
        result.subscriptions = session.subscriptions;
        result.waiting_to_be_sent = session
            .waiting_to_be_sent
            .into_iter()
            .map(|p| self.resolve_publication(p))
            .collect();

        result
    }

    fn get_payload(&mut self, id: u64) -> Bytes {
        self.payloads
            .get(&id)
            .expect("All payloads should be stored.")
            .clone()
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

#[derive(Deserialize, Serialize)]
struct ConsolidatedSession {
    client_id: ClientId,
    subscriptions: HashMap<String, Subscription>,
    waiting_to_be_sent: Vec<SimplifiedPublication>,
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
                    Err(Error::from(ErrorKind::Persist(ErrorReason::FileOpen(
                        path.clone(),
                    ))))
                });
                let file = OpenOptions::new()
                    .read(true)
                    .open(&path)
                    .context(ErrorKind::Persist(ErrorReason::FileOpen(path)))?;

                fail_point!("filepersistor.load.format", |_| {
                    Err(Error::from(ErrorKind::Persist(ErrorReason::Serialize)))
                });
                let state = format.load(file).map_err(Into::into)?;
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

    #[allow(clippy::too_many_lines)]
    async fn store(&mut self, state: BrokerState) -> Result<(), Self::Error> {
        let dir = self.dir.clone();
        let format = self.format.clone();
        let previous_count = self.previous_count;

        self.seq = self.seq.wrapping_add(1);
        let seq = self.seq;

        let res = tokio::task::spawn_blocking(move || {
            let span = span!(Level::INFO, "persistor", dir = %dir.display());
            let _guard = span.enter();

            if !dir.exists() {
                fail_point!("filepersistor.store.createdir", |_| {
                    Err(Error::from(ErrorKind::Persist(ErrorReason::CreateDir(
                        dir.clone(),
                    ))))
                });
                fs::create_dir_all(&dir)
                    .context(ErrorKind::Persist(ErrorReason::CreateDir(dir.clone())))?;
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
                Err(Error::from(ErrorKind::Persist(ErrorReason::FileOpen(
                    path.clone(),
                ))))
            });
            let file = OpenOptions::new()
                .create(true)
                .write(true)
                .open(&path)
                .context(ErrorKind::Persist(ErrorReason::FileOpen(path.clone())))?;
            debug!("{} opened.", path.display());

            debug!("persisting state to {}...", path.display());
            match format.store(file, state).map_err(Into::into) {
                Ok(_) => {
                    debug!("state persisted to {}.", path.display());

                    // Swap the symlink
                    //   - remove the old link if it exists
                    //   - link the new file
                    if temp_link_path.exists() {
                        fail_point!("filepersistor.store.symlink_unlink", |_| {
                            Err(Error::from(ErrorKind::Persist(ErrorReason::SymlinkUnlink(
                                temp_link_path.clone(),
                            ))))
                        });
                        fs::remove_file(&temp_link_path).context(ErrorKind::Persist(
                            ErrorReason::SymlinkUnlink(temp_link_path.clone()),
                        ))?;
                    }

                    debug!("linking {} to {}", temp_link_path.display(), path.display());

                    fail_point!("filepersistor.store.symlink", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::Symlink(
                            temp_link_path.clone(),
                            path.clone(),
                        ))))
                    });

                    #[cfg(unix)]
                    symlink(&path, &temp_link_path).context(ErrorKind::Persist(
                        ErrorReason::Symlink(temp_link_path.clone(), path.clone()),
                    ))?;

                    #[cfg(windows)]
                    symlink_file(&path, &temp_link_path).context(ErrorKind::Persist(
                        ErrorReason::Symlink(temp_link_path.clone(), path.clone()),
                    ))?;

                    // Commit the updated link by renaming the temp link.
                    // This is the so-called "capistrano" trick for atomically updating links
                    // https://github.com/capistrano/capistrano/blob/d04c1e3ea33e84b183d056b71c7cacf7744ce7ad/lib/capistrano/tasks/deploy.rake
                    fail_point!("filepersistor.store.filerename", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::FileRename(
                            temp_link_path.clone(),
                            link_path.clone(),
                        ))))
                    });
                    fs::rename(&temp_link_path, &link_path).context(ErrorKind::Persist(
                        ErrorReason::FileRename(temp_link_path, link_path),
                    ))?;

                    // Prune old states
                    fail_point!("filepersistor.store.readdir", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::ReadDir(
                            dir.clone(),
                        ))))
                    });

                    let mut entries = fs::read_dir(&dir)
                        .context(ErrorKind::Persist(ErrorReason::ReadDir(dir.clone())))?
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
                            Err(Error::from(ErrorKind::Persist(ErrorReason::FileUnlink(
                                entry.path(),
                            ))))
                        });
                        fs::remove_file(&entry.path()).context(ErrorKind::Persist(
                            ErrorReason::FileUnlink(entry.path().clone()),
                        ))?;
                        debug!("{} pruned.", entry.file_name().to_string_lossy());
                    }
                }
                Err(e) => {
                    fail_point!("filepersistor.store.new_file_unlink", |_| {
                        Err(Error::from(ErrorKind::Persist(ErrorReason::FileUnlink(
                            path.clone(),
                        ))))
                    });
                    fs::remove_file(&path)
                        .context(ErrorKind::Persist(ErrorReason::FileUnlink(path)))?;
                    return Err(e);
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
    FileOpen(PathBuf),
    FileRename(PathBuf, PathBuf),
    FileUnlink(PathBuf),
    CreateDir(PathBuf),
    ReadDir(PathBuf),
    Symlink(PathBuf, PathBuf),
    SymlinkUnlink(PathBuf),
    Serialize,
    Deserialize,
}

impl fmt::Display for ErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ErrorReason::FileOpen(pb) => write!(f, "failed to open file {}", pb.display()),
            ErrorReason::FileRename(from, to) => write!(
                f,
                "failed to rename file {} to {}",
                from.display(),
                to.display()
            ),
            ErrorReason::FileUnlink(pb) => write!(f, "failed to remove file {}", pb.display()),
            ErrorReason::CreateDir(pb) => {
                write!(f, "failed to create state directory {}", pb.display())
            }
            ErrorReason::ReadDir(pb) => {
                write!(f, "failed to read contents of directory {}", pb.display())
            }
            ErrorReason::Symlink(from, to) => write!(
                f,
                "failed to create symlink from {} to {}",
                from.display(),
                to.display()
            ),
            ErrorReason::SymlinkUnlink(pb) => {
                write!(f, "failed to remove symlink {}", pb.display())
            }
            ErrorReason::Serialize => write!(f, "failed to serialize state"),
            ErrorReason::Deserialize => write!(f, "failed to deserialize state"),
        }
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;

    use std::io::Cursor;

    use proptest::prelude::*;
    use tempfile::TempDir;

    use crate::broker::tests::arb_broker_state;

    proptest! {
        #[test]
        fn bincode_roundtrip(state in arb_broker_state()) {
            let expected = state.clone();
            let format = BincodeFormat;
            let mut buffer = vec![0_u8; 10 * 1024 * 1024];
            let writer = Cursor::new(&mut buffer);
            format.store(writer, state).unwrap();

            let reader = Cursor::new(buffer);
            let state = format.load(reader).unwrap();
            prop_assert_eq!(expected, state);
        }

        #[test]
        fn consolidate_simple(state in arb_broker_state()) {
            let expected = state.clone();

            let consolidated = Consolidator::consolidate_state(state);
            prop_assert_eq!(expected.retained.len(), consolidated.retained.len());
            prop_assert_eq!(expected.sessions.len(), consolidated.sessions.len());

            let state = Resolver::resolve_state(consolidated);

            prop_assert_eq!(expected.retained, state.retained);
            prop_assert_eq!(expected.sessions.len(), state.sessions.len());
            for i in 0..expected.sessions.len(){
                prop_assert_eq!(&expected.sessions[i].client_id, &state.sessions[i].client_id);
                prop_assert_eq!(&expected.sessions[i].subscriptions, &state.sessions[i].subscriptions);
                prop_assert_eq!(&expected.sessions[i].waiting_to_be_sent, &state.sessions[i].waiting_to_be_sent);
            }
        }

        #[test]
        fn consolidate_roundtrip(state in arb_broker_state()) {
            let expected = state.clone();
            let format = ConsolidatedStateFormat;
            let mut buffer = vec![0_u8; 10 * 1024 * 1024];
            let writer = Cursor::new(&mut buffer);
            format.store(writer, state).unwrap();

            let reader = Cursor::new(buffer);
            let state = format.load(reader).unwrap();

            prop_assert_eq!(expected.retained, state.retained);
            prop_assert_eq!(expected.sessions.len(), state.sessions.len());
            for i in 0..expected.sessions.len(){
                prop_assert_eq!(&expected.sessions[i].client_id, &state.sessions[i].client_id);
                prop_assert_eq!(&expected.sessions[i].subscriptions, &state.sessions[i].subscriptions);
                prop_assert_eq!(&expected.sessions[i].waiting_to_be_sent, &state.sessions[i].waiting_to_be_sent);
            }
        }
    }

    #[tokio::test]
    async fn filepersistor_smoketest() {
        let tmp_dir = TempDir::new().unwrap();
        let path = tmp_dir.path().to_owned();
        let mut persistor = FilePersistor::new(path, BincodeFormat::new());

        persistor.store(BrokerState::default()).await.unwrap();
        let state = persistor.load().await.unwrap().unwrap();
        assert_eq!(BrokerState::default(), state);
    }
}
