use std::sync::Arc;

use memmap::MmapMut;
use parking_lot::Mutex;

use super::{error::RingBufferError, RingBufferResult};

pub(crate) fn mmap_read(mmap: Arc<Mutex<MmapMut>>, start: usize, end: usize) -> Vec<u8> {
    let mmap = mmap.lock();
    let bytes = &mmap[start..end];
    Vec::from(bytes)
}

pub(crate) fn mmap_write(
    mmap: Arc<Mutex<MmapMut>>,
    start: usize,
    end: usize,
    bytes: &[u8],
    should_flush: bool,
) -> RingBufferResult<()> {
    let mut mmap = mmap.lock();
    mmap[start..end].clone_from_slice(&bytes);
    if should_flush {
        mmap.flush_async_range(start, end - start)
            .map_err(RingBufferError::Flush)?;
    }
    Ok(())
}
