use std::{collections::VecDeque, num::NonZeroUsize, task::Waker};

use mqtt3::proto::Publication;
use tracing::debug;

use crate::persist::{Key, MemoryError, PersistResult, StreamWakeableState};

pub mod error;
#[cfg(test)]
pub mod test;

/// When elements are retrieved they are moved to the loaded collection.
/// This loaded collection is necessary so it behaves the same as other `StreamWakeableState` implementations
pub struct WakingMemoryStore {
    queue: VecDeque<Item>,
    waker: Option<Waker>,
    max_size: usize,
    offset: u64,
}

impl WakingMemoryStore {
    pub fn new(max_size: NonZeroUsize) -> Self {
        Self {
            queue: VecDeque::new(),
            waker: None,
            max_size: max_size.get(),
            offset: 0,
        }
    }
}

impl StreamWakeableState for WakingMemoryStore {
    fn insert(&mut self, value: &Publication) -> PersistResult<Key> {
        let key = Key {
            offset: self.offset,
        };
        debug!("inserting publication with key {}", key);

        if self.max_size <= self.queue.len() {
            return Err(MemoryError::Full.into());
        }

        let item = Item {
            key,
            publication: value.clone(),
            has_read: false,
        };

        self.queue.push_back(item);

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        self.offset += 1;

        Ok(key)
    }

    fn batch(&mut self, size: usize) -> PersistResult<VecDeque<(Key, Publication)>> {
        let mut batch = VecDeque::with_capacity(size);
        for item in self.queue.iter_mut().take(size) {
            batch.push_back((item.key, item.publication.clone()));
            item.has_read = true;
        }

        Ok(batch)
    }

    fn pop(&mut self) -> PersistResult<Key> {
        match self.queue.pop_front() {
            Some(item) if !item.has_read => {
                let key = item.key;
                self.queue.push_front(item);
                Err(MemoryError::RemoveBeforeRead(key).into())
            }
            Some(item) => {
                debug!("removing publication with key {} ", item.key);

                if let Some(waker) = self.waker.take() {
                    waker.wake();
                }
                Ok(item.key)
            }
            None => Err(MemoryError::RemoveOnEmpty.into()),
        }
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

struct Item {
    key: Key,
    publication: Publication,
    has_read: bool,
}
