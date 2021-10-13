use std::{collections::VecDeque, num::NonZeroUsize, task::Waker};

use mqtt3::proto::Publication;

use crate::persist::{
    waking_state::memory::WakingMemoryStore, Key, PersistResult, StreamWakeableState,
};

pub struct TestWakingMemoryStore(WakingMemoryStore);

impl Default for TestWakingMemoryStore {
    fn default() -> Self {
        Self(WakingMemoryStore::new(unsafe {
            NonZeroUsize::new_unchecked(1024 * 1024 * 1024)
        }))
    }
}

impl StreamWakeableState for TestWakingMemoryStore {
    fn insert(&mut self, value: &Publication) -> PersistResult<Key> {
        self.0.insert(value)
    }

    fn batch(&mut self, size: usize) -> PersistResult<VecDeque<(Key, Publication)>> {
        self.0.batch(size)
    }

    fn pop(&mut self) -> PersistResult<Key> {
        self.0.pop()
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.0.set_waker(waker);
    }
}
