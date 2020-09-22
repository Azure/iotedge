#![allow(dead_code)] // TODO remove when ready

use std::{
    cmp::min,
    collections::{HashMap, VecDeque},
    task::Waker,
};

use mqtt3::proto::Publication;

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

/// When elements are retrieved they are moved to the in flight collection.
pub struct WakingMemoryStore {
    queue: VecDeque<(Key, Publication)>,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingMemoryStore {
    pub fn new() -> Self {
        WakingMemoryStore {
            queue: VecDeque::new(),
            in_flight: HashMap::new(),
            waker: None,
        }
    }
}

impl StreamWakeableState for WakingMemoryStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        let count = min(count, self.queue.len());
        let output: VecDeque<_> = self.queue.drain(..count).collect();
        self.in_flight.extend(output.clone().into_iter());

        Ok(output)
    }

    fn remove_in_flight(&mut self, key: &Key) -> Result<Publication, PersistError> {
        self.in_flight
            .remove(key)
            .ok_or(PersistError::RemovalForMissing)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
