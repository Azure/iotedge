mod block;
pub mod error;
pub mod flush;
mod serialize;

use std::{
    collections::VecDeque,
    fmt::Debug,
    fs::{File, OpenOptions},
    io::Result as IOResult,
    path::Path,
    task::Waker,
    time::Instant,
};

use bincode::Result as BincodeResult;
use error::BlockError;
use memmap::MmapMut;
use mqtt3::proto::Publication;
use serde::{Deserialize, Serialize};
use tracing::error;

use crate::persist::{
    waking_state::{
        ring_buffer::{
            block::{
                calculate_hash, serialized_block_size, validate, BlockHeaderWithHash, Data,
                BLOCK_HINT,
            },
            error::RingBufferError,
            flush::{FlushOptions, FlushState},
            serialize::{binary_deserialize, binary_serialize, binary_serialize_size},
        },
        StorageResult, StreamWakeableState,
    },
    Key, StorageError,
};

/// Convenience struct for tracking read and write pointers into the file.
#[derive(Debug, Default, Deserialize, Serialize)]
pub(crate) struct FilePointers {
    write: usize,
    read_begin: usize,
    read_end: usize,
}

/// Imagine there are three pointers:
/// W - A write pointer
/// R - A read pointer
/// D - A delete pointer
///
/// With a file buffer similar to below:
/// +---+---+---+---+---+---+---+---+---+
/// | A | B | C | D | E | F | G | H | I |
/// +---+---+---+---+---+---+---+---+---+
///
/// It is possible that W could wrap around and cause us to lose data due to
/// variable sized data being written. If W + data size > R and should write
/// precede read, the data could become corrupted.
///
/// Either this is prevented by scanning the file for the next block and
/// checking if the write will fit. Or, W + data must not pass both D and R.
/// - Not passing D helps with guaranteeing all data read was handled.
/// - Not passing R helps avoid corruption.
pub(crate) struct RingBuffer {
    flush_options: FlushOptions,
    flush_state: FlushState,
    mmap: MmapMut,
    max_file_size: usize,
    order: usize,
    pointers: FilePointers,
    waker: Option<Waker>,
    has_read: bool,
}

impl RingBuffer {
    pub(crate) fn new(
        file_path: &Path,
        max_file_size: usize,
        flush_options: &FlushOptions,
    ) -> Self {
        let file = create_file(file_path).expect("Failed to create file");
        file.set_len(max_file_size as u64)
            .expect("Failed to set file size");
        let mmap = unsafe { MmapMut::map_mut(&file).expect("Failed to create mmap") };
        let file_pointers =
            find_pointers_post_crash(&mmap, max_file_size).expect("Failed to init file pointers");
        Self {
            flush_options: flush_options.clone(),
            flush_state: FlushState::new(),
            mmap,
            max_file_size,
            order: usize::default(),
            pointers: file_pointers,
            waker: None,
            has_read: false,
        }
    }

    fn insert(&mut self, publication: &Publication) -> StorageResult<Key> {
        let timer = Instant::now();
        let data = binary_serialize(publication)?;
        let data = Data::new(&data);
        let data_size = binary_serialize_size(&data)?;
        let data_hash = calculate_hash(&data);

        let write_index = self.pointers.write;
        let read_index = self.pointers.read_begin;
        let order = self.order;
        let key = write_index;

        let block_header = BlockHeaderWithHash::new(data_hash, data_size, order, write_index);
        let block_size = serialized_block_size()?;

        let total_size = block_size + data_size;
        // Check to see if we might corrupt data if we write, if so, return err of full.
        // There are two cases for this:
        // 1. write has wrapped around and is now behind read
        if write_index < read_index && write_index + total_size > read_index {
            return Err(StorageError::RingBuffer(RingBufferError::Full));
        }
        // 2. write has reached the end (or close enough) but read has not moved
        if read_index == 0 && write_index + total_size > self.max_file_size {
            return Err(StorageError::RingBuffer(RingBufferError::Full));
        }

        let mut start = write_index;

        // Check if an existing block header is present. If the block is there
        // and if the overwrite flag is not set then we shouldn't write.
        let result = load_block_header(&self.mmap, start, block_size, self.max_file_size);
        if let Ok(block_header) = result {
            let should_not_overwrite = block_header.inner().should_not_overwrite();
            if should_not_overwrite {
                return Err(StorageError::RingBuffer(RingBufferError::Full));
            }
        }

        let should_flush = self.should_flush();

        save_block_header(
            &mut self.mmap,
            &block_header,
            start,
            self.max_file_size,
            should_flush,
        )?;

        let mut end = start + block_size;
        start = end % self.max_file_size;

        save_data(
            &mut self.mmap,
            &data,
            start,
            self.max_file_size,
            should_flush,
        )?;

        end = start + data_size;
        self.pointers.write = end % self.max_file_size;

        self.wake_up_task();

        self.order += 1;

        self.flush_state
            .update(1, total_size, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(Key { offset: key as u64 })
    }

    fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        let write_index = self.pointers.write;
        let read_index = self.pointers.read_begin;
        let block_size = serialized_block_size()?;

        // If read would go into where writes are happening then we don't have data to read.
        if self.pointers.read_end == write_index {
            return Ok(VecDeque::new());
        }

        let mut start = read_index;
        let mut vdata = VecDeque::new();
        for _ in 0..count {
            // This case shouldn't be pending, as we must have gotten something we can process.
            if start >= write_index {
                break;
            }

            let end = start + block_size;
            let bytes = &self.mmap[start..end];
            // this is unused memory
            if bytes.iter().map(|&x| x as usize).sum::<usize>() == 0 {
                break;
            }

            let block = binary_deserialize::<BlockHeaderWithHash>(bytes)?;
            // this means we read bytes that don't make a block, this is
            // a really bad state to be in as somehow the pointers don't
            // match to where data really is at.
            if block.inner().hint() != BLOCK_HINT {
                return Err(RingBufferError::Validate(BlockError::Hint).into());
            }

            let inner_block = block.inner();
            let data_size = inner_block.data_size();
            let index = inner_block.write_index();
            start += block_size;
            let end = start + data_size;
            let data = load_data(&self.mmap, start, data_size, self.max_file_size)?;
            start = end % self.max_file_size;
            self.pointers.read_end = start;

            validate(&block, &data)?;
            let publication = binary_deserialize::<Publication>(data.inner())?;
            let key = Key {
                offset: index as u64,
            };
            vdata.push_back((key, publication));

            self.has_read = true;
        }

        Ok(vdata)
    }

    fn remove(&mut self, key: usize) -> StorageResult<()> {
        if !self.has_read {
            return Err(StorageError::RingBuffer(RingBufferError::RemoveBeforeRead));
        }
        let timer = Instant::now();
        let read_index = self.pointers.read_begin;
        if key != read_index {
            return Err(StorageError::RingBuffer(RingBufferError::RemovalIndex));
        }
        let block_size = serialized_block_size()?;

        let start = key;
        let maybe_block = load_block_header(&self.mmap, start, block_size, self.max_file_size);
        if maybe_block.is_err() {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
        }

        let mut block = maybe_block.unwrap();
        if block.inner().hint() != BLOCK_HINT {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
        }

        let data_size;
        {
            let inner_block = block.inner_mut();
            inner_block.set_should_not_overwrite(false);
            data_size = inner_block.data_size();
        }

        let should_flush = self.should_flush();
        save_block_header(
            &mut self.mmap,
            &block,
            start,
            self.max_file_size,
            should_flush,
        )?;

        let end = start + block_size + data_size;
        self.pointers.read_begin = end % self.max_file_size;

        self.flush_state
            .update(0, 0, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        if self.pointers.read_end == self.pointers.read_begin {
            self.has_read = false;
        }

        Ok(())
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }

    fn should_flush(&self) -> bool {
        match self.flush_options {
            FlushOptions::AfterEachWrite => true,
            FlushOptions::AfterXWrites(xwrites) => self.flush_state.writes >= xwrites,
            FlushOptions::AfterXBytes(xbytes) => self.flush_state.bytes_written >= xbytes,
            FlushOptions::AfterXMilliseconds(xmillis_elapsed) => {
                self.flush_state.millis_elapsed >= xmillis_elapsed
            }
            FlushOptions::Off => false,
        }
    }

    fn wake_up_task(&mut self) {
        if let Some(waker) = self.waker.take() {
            waker.wake();
        }
    }
}

impl Drop for RingBuffer {
    fn drop(&mut self) {
        let result = self.mmap.flush();
        if let Some(err) = result.err() {
            error!("Failed to flush {:?}", err);
        }
    }
}

fn create_file(file_path: &Path) -> IOResult<File> {
    OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(file_path)
}

fn find_pointers_post_crash(mmap: &MmapMut, max_file_size: usize) -> StorageResult<FilePointers> {
    let mut block: BlockHeaderWithHash;
    let block_size = serialized_block_size()?;

    let mut start = 0;
    let mut end = start + block_size;
    let mut read = 0;
    let mut write = 0;
    // First we need to find *a* block header to work with.
    // Once we find one, if any, then we can skip more efficiently
    // to others.
    loop {
        if end > max_file_size {
            return Ok(FilePointers {
                write,
                read_begin: read,
                read_end: read,
            });
        }

        if let Ok(next_block) = load_block_header(mmap, start, block_size, max_file_size) {
            if next_block.inner().hint() == BLOCK_HINT {
                block = next_block
            } else {
                start += 1;
                end += 1;
                continue;
            }
        } else {
            start += 1;
            end += 1;
            continue;
        }

        // We have found a block and can break the loop.
        break;
    }
    let mut order = 0;
    // Now that a block has been found, we can find the last write.
    // Note read pointer is found a different way.
    // Read is found by scanning blocks marked for overwrite
    // and taking the value of the first non-overwrite block.
    loop {
        let inner = block.inner();
        let found_overwrite_block = !inner.should_not_overwrite();
        let data_size = inner.data_size();

        // Found a block that was removed, so update the read pointer.
        if found_overwrite_block && inner.hint() == BLOCK_HINT {
            read = end + data_size;
        }
        // Found the last write, take whatever we got for read and write
        // and return the pointers.
        if inner.order() < order {
            write = start;
            return Ok(FilePointers {
                write,
                read_begin: read,
                read_end: read,
            });
        }

        // Check next block
        order = inner.order();
        start = end + data_size;
        end = start + block_size;
        block = match load_block_header(mmap, start, block_size, max_file_size) {
            Ok(block) => block,
            Err(_) => {
                return Ok(FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                })
            }
        };
    }
}

impl StreamWakeableState for RingBuffer {
    fn insert(&mut self, value: Publication) -> StorageResult<Key> {
        RingBuffer::insert(self, &value)
    }

    fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        RingBuffer::batch(self, count)
    }

    fn remove(&mut self, key: Key) -> StorageResult<()> {
        RingBuffer::remove(self, key.offset as usize)
    }

    fn set_waker(&mut self, waker: &Waker) {
        RingBuffer::set_waker(self, waker)
    }
}

fn load_block_header(
    mmap: &MmapMut,
    start: usize,
    size: usize,
    file_size: usize,
) -> BincodeResult<BlockHeaderWithHash> {
    let bytes = mmap_read(mmap, start, size, file_size);
    binary_deserialize(&bytes)
}

fn save_block_header(
    mmap: &mut MmapMut,
    block: &BlockHeaderWithHash,
    start: usize,
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    let bytes = binary_serialize(block)?;
    mmap_write(mmap, start, &bytes, file_size, should_flush)
}

fn save_data(
    mmap: &mut MmapMut,
    data: &Data,
    start: usize,
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    let bytes = binary_serialize(data)?;
    mmap_write(mmap, start, &bytes, file_size, should_flush)
}

fn load_data(mmap: &MmapMut, start: usize, size: usize, file_size: usize) -> BincodeResult<Data> {
    let bytes = mmap_read(mmap, start, size, file_size);
    binary_deserialize::<Data>(&bytes)
}

fn mmap_read(mmap: &MmapMut, mut start: usize, size: usize, file_size: usize) -> Vec<u8> {
    // TODO: look into avoiding this copy
    // Options:
    // 1. split this in two and only do the copy on the wrap around
    // 2. see if there is a way to fudge two slices into one?
    // Probably will talk to dmolokanov about how to best do this...
    if start > file_size {
        start = start % file_size;
    }
    let end = start + size;
    if end > file_size {
        let file_split = end - file_size;
        let mut wrap = vec![];
        wrap.extend_from_slice(&mmap[start..file_size]);
        wrap.extend_from_slice(&mmap[0..file_split]);
        wrap
    } else {
        mmap[start..end].to_vec()
    }
}

fn mmap_write(
    mmap: &mut MmapMut,
    start: usize,
    bytes: &[u8],
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    let end = start + bytes.len();
    if end > file_size {
        let file_split = end - file_size;
        let bytes_split = bytes.len() - file_split;
        mmap[start..file_size].copy_from_slice(&bytes[..bytes_split]);
        mmap[0..file_split].copy_from_slice(&bytes[bytes_split..]);
    } else {
        mmap[start..end].copy_from_slice(&bytes);
    }
    if should_flush {
        mmap.flush_async_range(start, end - start)
            .map_err(RingBufferError::Flush)?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use std::panic;

    use bytes::Bytes;
    use matches::assert_matches;
    use mqtt3::proto::QoS;

    use super::*;

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const MAX_FILE_SIZE: usize = 1024;

    struct TestRingBuffer(RingBuffer);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let result = tempfile::NamedTempFile::new();
            assert!(result.is_ok());
            let file = result.unwrap();
            let file_path = file.path();
            Self(RingBuffer::new(file_path, MAX_FILE_SIZE, &FLUSH_OPTIONS))
        }
    }

    #[test]
    fn it_inits_ok_with_no_previous_data() {
        let result = panic::catch_unwind(|| {
            TestRingBuffer::default();
        });
        assert!(result.is_ok());
    }

    #[test]
    fn it_inits_ok_with_previous_data() {
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let result = panic::catch_unwind(|| {
            let read;
            let write;
            let result = tempfile::NamedTempFile::new();
            assert!(result.is_ok());
            let file = result.unwrap();
            let mut keys = vec![];
            // Create ring buffer and perform some operations and then destruct.
            {
                let mut rb = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
                for _ in 0..10 {
                    let result = rb.insert(&publication);
                    assert!(result.is_ok());
                    keys.push(result.unwrap());
                }

                let result = rb.batch(4);
                assert!(result.is_ok());
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 4);
                for key in &keys[..4] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[0].offset as usize;
                    assert!(rb.remove(key).is_ok());
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[1].offset as usize;
                    assert!(rb.remove(key).is_ok());
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[2].offset as usize;
                    assert!(rb.remove(key).is_ok());
                }

                read = rb.pointers.read_begin;
                write = rb.pointers.write;
            }
            // Create ring buffer again and validate pointers match where they left off.
            {
                let mut rb = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
                let loaded_read = rb.pointers.read_begin;
                let loaded_write = rb.pointers.write;
                assert_eq!(read, loaded_read);
                assert_eq!(write, loaded_write);
                let result = rb.batch(2);
                assert!(result.is_ok());
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 2);
                for key in &keys[3..5] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }
            }
        });
        assert!(result.is_ok());
    }

    #[test]
    fn it_inserts_ok_when_not_full() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let result = rb.0.insert(&publication);
        assert!(result.is_ok());
    }

    #[test]
    fn it_errs_on_insert_when_full() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let result = serialized_block_size();
        assert!(result.is_ok());
        let block_size = result.unwrap();

        let result = binary_serialize(&publication);
        assert!(result.is_ok());
        let data = result.unwrap();
        let data = Data::new(&data);

        let result = binary_serialize_size(&data);
        assert!(result.is_ok());
        let data_size = result.unwrap();

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            let result = rb.0.insert(&publication);
            assert!(result.is_ok());
        }
        let result = rb.0.insert(&publication);
        assert!(result.is_err());
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::Full)
        );
    }

    #[test]
    fn it_batches_correctly_after_insert() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // first with 1
        let result = rb.0.insert(&publication);
        assert!(result.is_ok());
        let key = result.unwrap();
        let result = rb.0.batch(1);
        assert!(result.is_ok());
        let batch = result.unwrap();
        assert!(!batch.is_empty());
        assert_eq!(key, batch[0].0);

        // now with 9 more
        let mut keys = vec![];
        keys.push(key);
        for _ in 0..9 {
            let result = rb.0.insert(&publication);
            assert!(result.is_ok());
            let key = result.unwrap();
            keys.push(key)
        }

        let result = rb.0.batch(10);
        assert!(result.is_ok());
        let batch = result.unwrap();
        assert_eq!(10, batch.len());
        for i in 0..10 {
            assert_eq!(keys[i], batch[i].0);
        }
    }

    #[test]
    fn it_batches_ok_when_no_insert() {
        let mut rb = TestRingBuffer::default();
        let result = rb.0.batch(1);
        assert_matches!(result, Ok(_));
    }

    #[test]
    fn it_errs_on_remove_with_key_not_equal_to_read() {
        let mut rb = TestRingBuffer::default();
        let result = rb.0.remove(1);
        assert!(result.is_err());
    }

    #[test]
    fn it_errs_on_remove_with_key_that_does_not_exist() {
        let mut rb = TestRingBuffer::default();
        let result = rb.0.remove(0);
        assert!(result.is_err());
    }

    #[test]
    fn it_removes_in_order_of_batch() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let mut keys = vec![];
        for _ in 0..10_usize {
            let result = rb.0.insert(&publication);
            assert!(result.is_ok());
            let key = result.unwrap();
            keys.push(key)
        }

        let result = rb.0.batch(10);
        assert!(result.is_ok());
        let mut batch = result.unwrap();

        for key in keys {
            let entry = batch.pop_front().unwrap();
            assert_eq!(key, entry.0);

            #[allow(clippy::cast_possible_truncation)]
            let result = rb.0.remove(key.offset as usize);
            assert!(result.is_ok());
        }
    }
}
