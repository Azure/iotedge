use std::{
    cmp::min,
    collections::{HashSet, VecDeque},
    task::Waker,
};

use async_trait::async_trait;

use tracing::debug;

use mqtt3::proto::Publication;

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

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

#[async_trait]
impl StreamWakeableState for WakingMemoryStore {
    async fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        debug!("inserting publication with key {:?}", key);

        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    async fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        let count = min(count, self.queue.len());
        let output: VecDeque<_> = self.queue.drain(..count).collect();

        for (key, _) in &output {
            self.loaded.insert(*key);
        }

        Ok(output)
    }

    async fn remove(&mut self, key: Key) -> Result<(), PersistError> {
        debug!(
            "Removing publication with key {:?}. Current state of loaded messages: {:?}",
            key, self.loaded
        );

        if self.loaded.remove(&key) {
            Ok(())
        } else {
            Err(PersistError::RemovalForMissing)
        }
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
