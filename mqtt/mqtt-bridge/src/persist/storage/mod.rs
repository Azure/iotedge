pub mod ring_buffer;
pub mod serialize;

use super::StorageError;
use std::sync::atomic::{AtomicUsize, Ordering};

#[allow(dead_code)]
pub type StorageResult<T> = Result<T, StorageError>;

#[allow(dead_code)]
#[derive(Clone, Copy, Debug)]
pub enum FlushOptions {
    AfterEachWrite,
    AfterXWrites(usize),
    AfterXBytes(usize),
    AfterXMilliseconds(usize),
    Off,
}

#[allow(dead_code)]
pub struct FlushState {
    writes: AtomicUsize,
    bytes_written: AtomicUsize,
    millis_elapsed: AtomicUsize,
}

#[allow(dead_code)]
impl FlushState {
    fn new() -> Self {
        Self {
            writes: AtomicUsize::default(),
            bytes_written: AtomicUsize::default(),
            millis_elapsed: AtomicUsize::default(),
        }
    }

    fn reset(&self, flush_option: &FlushOptions) {
        match flush_option {
            FlushOptions::AfterEachWrite => {}
            FlushOptions::AfterXWrites(_) => {
                self.writes.store(0, Ordering::SeqCst);
            }
            FlushOptions::AfterXBytes(_) => {
                self.bytes_written.store(0, Ordering::SeqCst);
            }
            FlushOptions::AfterXMilliseconds(_) => {
                self.millis_elapsed.store(0, Ordering::SeqCst);
            }
            FlushOptions::Off => {}
        }
    }

    fn update(&self, writes: usize, bytes_written: usize, millis_elapsed: usize) {
        self.bytes_written
            .fetch_add(bytes_written, Ordering::SeqCst);
        self.millis_elapsed
            .fetch_add(millis_elapsed, Ordering::SeqCst);
        self.writes.fetch_add(writes, Ordering::SeqCst);
    }
}
