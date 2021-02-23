use std::{cmp::min, collections::VecDeque, num::NonZeroUsize, task::Waker};

use mqtt3::proto::Publication;
use tracing::debug;

use crate::persist::{Key, MemoryError, PersistResult, StreamWakeableState};

pub mod error;
#[cfg(test)]
pub mod test;

/// When elements are retrieved they are moved to the loaded collection.
/// This loaded collection is necessary so it behaves the same as other `StreamWakeableState` implementations
pub struct WakingMemoryStore {
    queue: VecDeque<(Key, Publication)>,
    loaded: VecDeque<Key>,
    waker: Option<Waker>,
    max_size: usize,
}

impl WakingMemoryStore {
    pub fn new(max_size: NonZeroUsize) -> Self {
        Self {
            queue: VecDeque::new(),
            loaded: VecDeque::new(),
            waker: None,
            max_size: max_size.get(),
        }
    }
}

impl StreamWakeableState for WakingMemoryStore {
    fn insert(&mut self, value: &Publication) -> PersistResult<Key> {
        let key = Key {
            offset: self.queue.len() as u64,
        };

        debug!("inserting publication with key {:?}", key);

        if self.max_size <= self.queue.len() {
            return Err(MemoryError::Full.into());
        }

        self.queue.push_back((key, value.clone()));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(key)
    }

    fn batch(&mut self, count: usize) -> PersistResult<VecDeque<(Key, Publication)>> {
        let count = min(count, self.queue.len());
        let output: VecDeque<_> = self.queue.drain(..count).collect();

        for (key, _) in &output {
            self.loaded.push_back(*key);
        }

        Ok(output)
    }

    fn remove(&mut self, key: Key) -> PersistResult<()> {
        debug!(
            "Removing publication with key {:?}. Current state of loaded messages: {:?}",
            key, self.loaded
        );

        if self.loaded.is_empty() {
            Err(MemoryError::RemoveOnEmpty.into())
        } else if self.loaded[0] == key {
            self.loaded.pop_front();
            Ok(())
        } else {
            Err(MemoryError::BadKeyOrdering.into())
        }
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
