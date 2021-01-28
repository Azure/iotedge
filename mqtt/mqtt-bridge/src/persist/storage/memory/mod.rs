pub mod error;

use std::{
    cmp::min,
    collections::{HashSet, VecDeque},
    task::Waker,
};
use crate::persist::{waking_state::StreamWakeableState, Key};
use mqtt3::proto::Publication;
use tracing::debug;
use error::MemoryError;
use super::StorageResult;

/// When elements are retrieved they are moved to the loaded collection.
/// This loaded collection is necessary so it behaves the same as other `StreamWakeableState` implementations
pub struct WakingMemoryStore {
    queue: VecDeque<(Key, Publication)>,
    loaded: HashSet<Key>,
    waker: Option<Waker>,
}

impl Default for WakingMemoryStore {
    fn default() -> Self {
        Self {
            queue: VecDeque::new(),
            loaded: HashSet::new(),
            waker: None,
        }
    }
}

impl StreamWakeableState for WakingMemoryStore {
    fn insert(&mut self, value: Publication) -> StorageResult<Key> {
        let key = Key {
            offset: self.queue.len() as u64,
        };

        debug!("inserting publication with key {:?}", key);

        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(key)
    }

    fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        let count = min(count, self.queue.len());
        let output: VecDeque<_> = self.queue.drain(..count).collect();

        for (key, _) in &output {
            self.loaded.insert(*key);
        }

        Ok(output)
    }

    fn remove(&mut self, key: Key) -> StorageResult<()> {
        debug!(
            "Removing publication with key {:?}. Current state of loaded messages: {:?}",
            key, self.loaded
        );

        if self.loaded.remove(&key) {
            Ok(())
        } else {
            Err(MemoryError::NonExistantKey.into())
        }
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
