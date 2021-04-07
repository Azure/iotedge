use std::{collections::VecDeque, num::NonZeroU64, task::Waker};

use mqtt3::proto::Publication;

use crate::persist::{
    waking_state::{
        ring_buffer::{flush::FlushOptions, RingBuffer},
        PersistResult,
    },
    Key, StreamWakeableState,
};

const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
const MAX_FILE_SIZE: NonZeroU64 = unsafe { NonZeroU64::new_unchecked(1024) };

pub(crate) struct TestRingBuffer(RingBuffer);

impl StreamWakeableState for TestRingBuffer {
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
        self.0.set_waker(waker)
    }
}

impl Default for TestRingBuffer {
    fn default() -> Self {
        let result = tempfile::NamedTempFile::new();
        assert!(result.is_ok());
        let file = result.unwrap();
        let file_path = file.path().to_path_buf();

        let result = RingBuffer::new(&file_path, MAX_FILE_SIZE, FLUSH_OPTIONS);
        assert!(result.is_ok());
        Self(result.unwrap())
    }
}
