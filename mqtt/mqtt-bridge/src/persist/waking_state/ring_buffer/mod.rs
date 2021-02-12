#![allow(dead_code)]
mod block;
pub mod error;
pub mod flush;

use std::{
    collections::VecDeque,
    fmt::Debug,
    fs::{File, OpenOptions},
    io::Result as IOResult,
    mem::size_of,
    path::Path,
    task::Waker,
    time::{Duration, Instant},
};

use bincode::Result as BincodeResult;
use error::BlockError;
use memmap::MmapMut;
use mqtt3::proto::Publication;
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use tracing::error;

use crate::persist::{
    waking_state::ring_buffer::{
        block::{calculate_hash, validate, BlockHeaderWithHash, BLOCK_HINT, SERIALIZED_BLOCK_SIZE},
        error::RingBufferError,
        flush::{FlushOptions, FlushState},
    },
    Key, StorageError,
};

pub type StorageResult<T> = Result<T, StorageError>;

/// Convenience struct for tracking read and write pointers into the file.
#[derive(Debug, Default, Deserialize, Serialize)]
pub(crate) struct FilePointers {
    write: u32,
    read_begin: u32,
    read_end: u32,
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
    // If write reaches read but we have data to read, this is set.
    // It allows us handle the edge case of read == write, but data
    // has not been read yet.
    can_read_from_wrap_around: bool,
    // The optional flush threshold to uphold.
    flush_options: FlushOptions,
    // For determining if a flush should occur.
    flush_state: FlushState,
    // If data has been read this is set, if data is removed to
    // the point of reaching the read pointer this is unset.
    // This prevents deletion of data without reading.
    has_read: bool,
    // Max size for the file that is required for mmap.
    max_file_size: u32,
    // A representation of Mmap with operations built-in.
    mmap: MmapMut,
    // A tracker for suppling an ordering to blocks being written.
    order: u128,
    // Pointers into the file (read/write).
    pointers: FilePointers,
    // A waker for updating any pending batch after an insert.
    waker: Option<Waker>,
}

impl RingBuffer {
    pub(crate) fn new(
        file_path: &Path,
        max_file_size: u32,
        flush_options: &FlushOptions,
    ) -> StorageResult<Self> {
        let file = create_file(file_path).map_err(RingBufferError::MmapCreate)?;
        file.set_len(max_file_size.into())
            .map_err(RingBufferError::MmapCreate)?;
        let mmap = unsafe { MmapMut::map_mut(&file).map_err(RingBufferError::MmapCreate)? };
        let (file_pointers, order) = find_pointers_and_order_post_crash(&mmap, max_file_size)?;

        Ok(Self {
            can_read_from_wrap_around: false,
            flush_options: *flush_options,
            flush_state: FlushState::new(),
            has_read: false,
            max_file_size,
            mmap,
            order,
            pointers: file_pointers,
            waker: None,
        })
    }

    fn insert(&mut self, publication: &Publication) -> StorageResult<Key> {
        let timer = Instant::now();
        let data = bincode::serialize(publication)?;

        #[allow(clippy::cast_possible_truncation)]
        let data_size = data.len() as u32;
        let data_hash = calculate_hash(&data);

        let write_index = self.pointers.write;
        let read_index = self.pointers.read_begin;
        let order = self.order;
        let key = write_index;

        let block_header = BlockHeaderWithHash::new(data_hash, data_size, order, write_index);

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

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
        // 3. check if write would wrap around end and corrupt
        if write_index > read_index
            && write_index + total_size > self.max_file_size
            && (write_index + total_size) % self.max_file_size > read_index
        {
            return Err(StorageError::RingBuffer(RingBufferError::Full));
        }

        let mut start = write_index;

        // Check if an existing block header is present. If the block is there
        // and if the overwrite flag is not set then we shouldn't write.
        let result = load_block_header(
            &self.mmap,
            start as usize,
            block_size as usize,
            self.max_file_size as usize,
        );
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
            start as usize,
            self.max_file_size as usize,
            should_flush,
        )?;

        let mut end = start + block_size;
        start = end % self.max_file_size;

        save_data(
            &mut self.mmap,
            &data,
            start as usize,
            self.max_file_size as usize,
            should_flush,
        )?;

        end = start + data_size;
        self.pointers.write = end % self.max_file_size;

        // should only happen if we wrap around
        if self.pointers.write == self.pointers.read_begin {
            self.can_read_from_wrap_around = true;
        }

        self.wake_up_task();

        self.order += 1;

        self.flush_state_update(should_flush, 1, total_size, timer.elapsed());

        Ok(Key { offset: key })
    }

    fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        let write_index = self.pointers.write;
        let read_index = self.pointers.read_begin;

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

        // If read would go into where writes are happening then we don't have data to read.
        if self.pointers.read_end == write_index && !self.can_read_from_wrap_around {
            return Ok(VecDeque::new());
        }

        // If we are reading then we can reset wrap around flag
        self.can_read_from_wrap_around = false;

        let mut start = read_index;
        let mut vdata = VecDeque::new();
        for _ in 0..count {
            let end = start + block_size;
            let bytes = &self.mmap[start as usize..end as usize];
            // this is unused memory
            if bytes.iter().map(|&x| x as usize).sum::<usize>() == 0 {
                break;
            }

            let block = bincode::deserialize::<BlockHeaderWithHash>(bytes)?;
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
            let data = load_data(
                &self.mmap,
                start as usize,
                data_size as usize,
                self.max_file_size as usize,
            )?;
            start = end % self.max_file_size;
            self.pointers.read_end = start;

            validate(&block, &bincode::serialize(&data)?)?;
            let key = Key { offset: index };

            vdata.push_back((key, data));

            self.has_read = true;

            // This case shouldn't be pending, as we must have gotten something we can process.
            if start as u32 == write_index {
                break;
            }
        }

        Ok(vdata)
    }

    fn remove(&mut self, key: u32) -> StorageResult<()> {
        if !self.has_read {
            return Err(StorageError::RingBuffer(RingBufferError::RemoveBeforeRead));
        }
        let timer = Instant::now();
        let read_index = self.pointers.read_begin;
        if key != read_index {
            return Err(StorageError::RingBuffer(RingBufferError::RemovalIndex));
        }

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

        let start = key;
        let mut block = load_block_header(
            &self.mmap,
            start as usize,
            block_size as usize,
            self.max_file_size as usize,
        )
        .map_err(StorageError::Serialization)?;

        if block.inner().hint() != BLOCK_HINT {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
        }

        let data_size = {
            let inner_block = block.inner_mut();
            inner_block.set_should_not_overwrite(false);
            inner_block.data_size()
        };

        let should_flush = self.should_flush();
        save_block_header(
            &mut self.mmap,
            &block,
            start as usize,
            self.max_file_size as usize,
            should_flush,
        )?;

        let end = start + block_size + data_size;
        self.pointers.read_begin = end % self.max_file_size;

        self.flush_state_update(should_flush, 0, 0, timer.elapsed());

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
            FlushOptions::AfterXTime(xelapsed) => self.flush_state.elapsed >= xelapsed,
            FlushOptions::Off => false,
        }
    }

    fn wake_up_task(&mut self) {
        if let Some(waker) = self.waker.take() {
            waker.wake();
        }
    }

    fn flush_state_update(
        &mut self,
        should_flush: bool,
        writes: u32,
        bytes_written: u32,
        millis: Duration,
    ) {
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        } else {
            self.flush_state.update(writes, bytes_written, millis);
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

fn find_pointers_and_order_post_crash(
    mmap: &MmapMut,
    max_file_size: u32,
) -> StorageResult<(FilePointers, u128)> {
    let mut block: BlockHeaderWithHash;

    #[allow(clippy::cast_possible_truncation)]
    let block_size = *SERIALIZED_BLOCK_SIZE as u32;

    let mut start = 0_u32;
    let mut end = start + block_size;
    let mut read = 0;
    let mut write = 0;
    let mut order = 0;

    // First we need to find *a* block header to work with.
    // Once we find one, if any, then we can skip more efficiently
    // to others.
    loop {
        if end > max_file_size {
            return Ok((
                FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
            ));
        }

        let hint_result =
            bincode::deserialize::<u32>(&mmap[start as usize..(start as usize + size_of::<u32>())]);

        if let Ok(hint) = hint_result {
            if hint == BLOCK_HINT {
                block = load_block_header(
                    mmap,
                    start as usize,
                    block_size as usize,
                    max_file_size as usize,
                )?;
                order = block.inner().order();
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
            read = end as u32 + data_size;
        }
        // Found the last write, take whatever we got for read and write
        // and return the pointers.
        if inner.order() < order {
            write = start as u32;

            return Ok((
                FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
            ));
        }

        // Check next block
        order = inner.order();
        start = (end + data_size) % max_file_size;
        end = (start + block_size) % max_file_size;

        let bytes = &mmap[start as usize..end as usize];
        // this is unused memory
        if bytes.iter().map(|&x| x as usize).sum::<usize>() == 0 {
            write = start as u32;

            return Ok((
                FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
            ));
        }

        block = match load_block_header(
            mmap,
            start as usize,
            block_size as usize,
            max_file_size as usize,
        ) {
            Ok(block) => block,
            Err(_) => {
                return Ok((
                    FilePointers {
                        write,
                        read_begin: read,
                        read_end: read,
                    },
                    order,
                ));
            }
        };
    }
}

fn load_block_header(
    mmap: &MmapMut,
    mut start: usize,
    size: usize,
    file_size: usize,
) -> BincodeResult<BlockHeaderWithHash> {
    if start > file_size {
        start %= file_size;
    }
    let end = start + size;

    if end > file_size {
        mmap_read_wrap_around(mmap, start, size, file_size)
    } else {
        mmap_read(mmap, start, size)
    }
}

fn save_block_header(
    mmap: &mut MmapMut,
    block: &BlockHeaderWithHash,
    start: usize,
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    let bytes = bincode::serialize(block)?;
    mmap_write(mmap, start, &bytes, file_size, should_flush)
}

fn save_data(
    mmap: &mut MmapMut,
    serialized_data: &[u8],
    start: usize,
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    mmap_write(mmap, start, &serialized_data, file_size, should_flush)
}

fn load_data(
    mmap: &MmapMut,
    mut start: usize,
    size: usize,
    file_size: usize,
) -> BincodeResult<Publication> {
    if start > file_size {
        start %= file_size;
    }
    let end = start + size;

    if end > file_size {
        mmap_read_wrap_around(mmap, start, size, file_size)
    } else {
        mmap_read(mmap, start, size)
    }
}

fn mmap_read<'a, T>(mmap: &'a MmapMut, start: usize, size: usize) -> BincodeResult<T>
where
    T: Deserialize<'a>,
{
    let end = start + size;
    bincode::deserialize::<T>(&mmap[start as usize..end as usize])
}

fn mmap_read_wrap_around<T>(
    mmap: &MmapMut,
    start: usize,
    size: usize,
    file_size: usize,
) -> BincodeResult<T>
where
    T: DeserializeOwned,
{
    let end = start + size;
    let file_split = end - file_size;
    let mut wrap = vec![];
    wrap.extend_from_slice(&mmap[start..file_size]);
    wrap.extend_from_slice(&mmap[0..file_split]);
    bincode::deserialize::<T>(&wrap)
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
    use std::{fs::write, panic};

    use bytes::Bytes;
    use matches::assert_matches;
    use mqtt3::proto::QoS;

    use super::*;

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const MAX_FILE_SIZE: u32 = 1024;

    struct TestRingBuffer(RingBuffer);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let result = tempfile::NamedTempFile::new();
            assert_matches!(result, Ok(_));
            let file = result.unwrap();
            let file_path = file.path();
            let result = RingBuffer::new(file_path, MAX_FILE_SIZE, &FLUSH_OPTIONS);
            assert!(result.is_ok());
            Self(result.unwrap())
        }
    }

    #[test]
    fn it_inits_ok_with_no_previous_data() {
        let result = panic::catch_unwind(|| {
            TestRingBuffer::default();
        });
        assert_matches!(result, Ok(_));
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
            assert_matches!(result, Ok(_));
            let file = result.unwrap();
            let mut keys = vec![];
            // Create ring buffer and perform some operations and then destruct.
            {
                let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                for _ in 0..10 {
                    let result = rb.insert(&publication);
                    assert_matches!(result, Ok(_));
                    keys.push(result.unwrap());
                }

                let result = rb.batch(4);
                assert_matches!(result, Ok(_));
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 4);
                for key in &keys[..4] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[0].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[1].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                {
                    #[allow(clippy::cast_possible_truncation)]
                    let key = keys[2].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                read = rb.pointers.read_begin;
                write = rb.pointers.write;
            }
            // Create ring buffer again and validate pointers match where they left off.
            {
                let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let loaded_read = rb.pointers.read_begin;
                let loaded_write = rb.pointers.write;
                assert_eq!(read, loaded_read);
                assert_eq!(write, loaded_write);
                let result = rb.batch(2);
                assert_matches!(result, Ok(_));
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 2);
                for key in &keys[3..5] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }
            }
        });
        assert_matches!(result, Ok(_));
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
        assert_matches!(result, Ok(_));
    }

    #[test]
    fn it_inserts_ok_after_leftover() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let mut keys = vec![];
        {
            let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
            assert!(result.is_ok());
            let mut rb = result.unwrap();
            // write some
            for _ in 0..5 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                let key = result.unwrap();
                keys.push(key);
            }

            let result = rb.batch(5);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for key in &keys[..5] {
                let maybe_entry = batch.remove(0);
                assert!(maybe_entry.is_some());
                let entry = maybe_entry.unwrap();
                assert_eq!(*key, entry.0);
                assert_eq!(publication, entry.1);
                #[allow(clippy::cast_possible_truncation)]
                let result = rb.remove(key.offset);
                assert_matches!(result, Ok(_));
            }
        }
        {
            let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            // write some more
            for _ in 0..5 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                let key = result.unwrap();
                keys.push(key);
            }

            let result = rb.batch(5);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for key in &keys[5..] {
                let maybe_entry = batch.remove(0);
                assert!(maybe_entry.is_some());
                let entry = maybe_entry.unwrap();
                assert_eq!(*key, entry.0);
                assert_eq!(publication, entry.1);
            }
        }
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

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));
        let data = result.unwrap();

        #[allow(clippy::cast_possible_truncation)]
        let data_size = data.len() as u32;

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            let result = rb.0.insert(&publication);
            assert_matches!(result, Ok(_));
        }
        let result = rb.0.insert(&publication);
        assert_matches!(result, Err(StorageError::RingBuffer(RingBufferError::Full)));
    }

    #[test]
    fn it_errs_on_insert_when_full_and_had_batched_and_removed() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));
        let data = result.unwrap();

        #[allow(clippy::cast_possible_truncation)]
        let data_size = data.len() as u32;

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            let result = rb.0.insert(&publication);
            assert_matches!(result, Ok(_));
        }

        let result = rb.0.batch(1);
        assert_matches!(result, Ok(_));
        let batch = result.unwrap();

        #[allow(clippy::cast_possible_truncation)]
        let result = rb.0.remove(batch[0].0.offset);
        assert_matches!(result, Ok(_));

        // need bigger pub
        let big_publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::from(
                "It was the best of times, it was the worst of times, 
                it was the age of wisdom, it was the age of foolishness, 
                it was the epoch of belief, it was the epoch of incredulity, 
                it was the season of light, it was the season of darkness, 
                it was the spring of hope, it was the winter of despair.",
            ),
        };

        let result = rb.0.insert(&big_publication);
        assert_matches!(result, Err(StorageError::RingBuffer(RingBufferError::Full)));
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
        assert_matches!(result, Ok(_));
        let key = result.unwrap();
        let result = rb.0.batch(1);
        assert_matches!(result, Ok(_));
        let batch = result.unwrap();
        assert!(!batch.is_empty());
        assert_eq!(key, batch[0].0);

        // now with 9 more
        let mut keys = vec![];
        keys.push(key);
        for _ in 0..9 {
            let result = rb.0.insert(&publication);
            assert_matches!(result, Ok(_));
            let key = result.unwrap();
            keys.push(key)
        }

        let result = rb.0.batch(10);
        assert_matches!(result, Ok(_));
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
    fn it_batches_ok_when_leftover_from_file() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let mut keys = vec![];
        {
            let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            // write some
            for _ in 0..10 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                let key = result.unwrap();
                keys.push(key);
            }

            let result = rb.batch(5);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for key in &keys[..5] {
                let maybe_entry = batch.remove(0);
                assert!(maybe_entry.is_some());
                let entry = maybe_entry.unwrap();
                assert_eq!(*key, entry.0);
                assert_eq!(publication, entry.1);
                #[allow(clippy::cast_possible_truncation)]
                let result = rb.remove(key.offset);
                assert_matches!(result, Ok(_));
            }
        }
        {
            let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            let result = rb.batch(5);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for key in &keys[5..] {
                let maybe_entry = batch.remove(0);
                assert!(maybe_entry.is_some());
                let entry = maybe_entry.unwrap();
                assert_eq!(*key, entry.0);
                assert_eq!(publication, entry.1);
            }
        }
    }

    #[test]
    fn it_batches_err_when_bad_block_from_file() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
        assert!(result.is_ok());
        let mut rb = result.unwrap();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let result = rb.insert(&publication);
        assert_matches!(result, Ok(_));

        let result = write(file.path(), "garbage");
        assert_matches!(result, Ok(_));

        let result = rb.batch(1);
        assert_matches!(
            result,
            Err(StorageError::RingBuffer(RingBufferError::Validate(
                BlockError::Hint
            )))
        );
    }

    #[test]
    fn it_batches_ok_when_write_reaches_begin_again() {
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));

        let data = result.unwrap();

        #[allow(clippy::cast_possible_truncation)]
        let block_size = *SERIALIZED_BLOCK_SIZE as u32;

        let result = bincode::serialized_size(&data);
        assert_matches!(result, Ok(_));

        #[allow(clippy::cast_possible_truncation)]
        let data_size = result.unwrap() as u32;

        let file_size = 10 * (block_size + data_size);

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(file.path(), file_size, &FLUSH_OPTIONS);
        assert!(result.is_ok());
        let mut rb = result.unwrap();

        let mut keys = vec![];
        for _ in 0..10 {
            let result = rb.insert(&publication);
            assert_matches!(result, Ok(_));
            keys.push(result.unwrap());
        }

        let result = rb.batch(10);
        assert_matches!(result, Ok(_));
        let batch = result.unwrap();
        assert!(!batch.is_empty());

        assert_eq!(
            batch.iter().map(|(k, _)| k.offset).collect::<Vec<_>>(),
            keys.iter().map(|k| k.offset).collect::<Vec<_>>()
        );
    }

    #[test]
    fn it_errs_on_remove_when_no_read() {
        let mut rb = TestRingBuffer::default();
        let result = rb.0.remove(1);
        assert_matches!(result, Err(_));
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::RemoveBeforeRead)
        );
    }

    #[test]
    fn it_errs_on_remove_with_key_not_equal_to_read() {
        let mut rb = TestRingBuffer::default();

        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let result = rb.0.insert(&publication);
        assert_matches!(result, Ok(_));

        let result = rb.0.batch(1);
        assert_matches!(result, Ok(_));

        let result = rb.0.remove(1);
        assert_matches!(result, Err(_));
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::RemovalIndex)
        );
    }

    #[test]
    fn it_errs_on_remove_with_key_that_does_not_exist() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(file.path(), MAX_FILE_SIZE, &FLUSH_OPTIONS);
        assert!(result.is_ok());
        let mut rb = result.unwrap();

        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let result = rb.insert(&publication);
        assert_matches!(result, Ok(_));

        let result = rb.batch(1);
        assert_matches!(result, Ok(_));

        let result = write(file.path(), "garbage");
        assert_matches!(result, Ok(_));

        let result = rb.remove(0);
        assert_matches!(result, Err(_));
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::NonExistantKey)
        );
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
            assert_matches!(result, Ok(_));
            let key = result.unwrap();
            keys.push(key)
        }

        let result = rb.0.batch(10);
        assert_matches!(result, Ok(_));
        let mut batch = result.unwrap();

        for key in keys {
            let entry = batch.pop_front().unwrap();
            assert_eq!(key, entry.0);

            #[allow(clippy::cast_possible_truncation)]
            let result = rb.0.remove(key.offset);
            assert_matches!(result, Ok(_));
        }
    }
}
