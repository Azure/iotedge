mod block;
pub mod error;
pub mod flush;
#[cfg(test)]
pub mod test;

use std::{
    collections::VecDeque,
    fmt::Debug,
    fs::{create_dir_all, File, OpenOptions},
    io::{BufReader, Read, Result as IOResult, Seek, SeekFrom, Write},
    num::NonZeroU64,
    path::PathBuf,
    task::Waker,
    time::{Duration, Instant},
};

use mqtt3::proto::Publication;

use bincode::Result as BincodeResult;
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use tracing::error;

use crate::persist::{
    waking_state::{
        ring_buffer::{
            block::{
                calculate_crc_over_bytes, validate, BlockHeaderV1, BlockHeaderWithCrc,
                BlockVersion, BLOCK_HINT, SERIALIZED_BLOCK_SIZE,
            },
            error::{BlockError, RingBufferError},
            flush::{FlushOptions, FlushState},
        },
        PersistResult, StreamWakeableState,
    },
    Key, PersistError,
};

/// Convenience struct for tracking read and write pointers into the file.
#[derive(Debug, Default, Deserialize, Serialize)]
pub(crate) struct FilePointers {
    write: u64,
    read_begin: u64,
    read_end: u64,
}

// Metadata that is saved to state file for rapidly picking up where
// previous `RingBuffer` state was at.
#[derive(Debug, Default, Deserialize, Serialize)]
pub(crate) struct RingBufferMetadata {
    // If write reaches read but we have data to read, this is set.
    // It allows us handle the edge case of read == write, but data
    // has not been read yet.
    can_read_from_wrap_around_when_write_full: bool,

    // Pointers into the file (read/write).
    file_pointers: FilePointers,

    // A tracker for suppling an ordering to blocks being written.
    order: u64,
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
#[derive(Debug)]
pub struct RingBuffer {
    // The optional flush threshold to uphold.
    flush_options: FlushOptions,

    // For determining if a flush should occur.
    flush_state: FlushState,

    // If data has been read this is set, if data is removed to
    // the point of reaching the read pointer this is unset.
    // This prevents deletion of data without reading.
    has_read: bool,

    // Max size for the file.
    max_file_size: u64,

    // A representation of Mmap with operations built-in.
    file: File,

    // A collection of data that used for tracking state of the
    // `RingBuffer`.
    metadata: RingBufferMetadata,

    // A waker for updating any pending batch after an insert.
    waker: Option<Waker>,
}

impl RingBuffer {
    pub(crate) fn new(
        file_path: &PathBuf,
        max_file_size: NonZeroU64,
        flush_options: FlushOptions,
    ) -> PersistResult<Self> {
        let mut file = create_file(file_path).map_err(RingBufferError::FileCreate)?;

        // We cannot allow for file truncation if an existing file exists. That will surely
        // lead to data loss.
        let current_file_size = file
            .metadata()
            .map_err(RingBufferError::FileMetadata)?
            .len();
        let max_file_size = max_file_size.get();

        if current_file_size > max_file_size {
            return Err(RingBufferError::FileTruncation {
                current: current_file_size,
                new: max_file_size,
            }
            .into());
        }
        file.set_len(max_file_size)
            .map_err(RingBufferError::FileCreate)?;

        // For correctness, need to scan after best guess and see if can get more accurate.
        let metadata = find_pointers_and_order_post_crash(&mut file, max_file_size);

        Ok(Self {
            flush_options,
            flush_state: FlushState::default(),
            has_read: false,
            max_file_size,
            file,
            metadata,
            waker: None,
        })
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
        writes: u64,
        bytes_written: u64,
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
        let result = self.file.flush();
        if let Some(err) = result.err() {
            error!("Failed to flush {:?}", err);
        }
    }
}

impl StreamWakeableState for RingBuffer {
    fn insert(&mut self, publication: &Publication) -> PersistResult<Key> {
        let timer = Instant::now();
        let data = bincode::serialize(publication)?;

        let data_size = data.len() as u64;
        let data_crc = calculate_crc_over_bytes(&data);

        let write_index = self.metadata.file_pointers.write;
        let read_index = self.metadata.file_pointers.read_begin;
        let order = self.metadata.order;
        let key = write_index;

        let block_size = *SERIALIZED_BLOCK_SIZE;
        let total_size = block_size + data_size;

        // Check that we have enough space to insert data.
        // If we have set can_read_from_wrap_around_when_write_full
        // then we must also be full.
        let free_space = (read_index + self.max_file_size - write_index) % self.max_file_size;
        if self.metadata.can_read_from_wrap_around_when_write_full {
            return Err(PersistError::RingBuffer(RingBufferError::Full));
        }
        if read_index != write_index && free_space < total_size {
            return Err(PersistError::RingBuffer(RingBufferError::Full));
        }

        let start = write_index;

        let v1 = BlockHeaderV1::new(BLOCK_HINT, data_crc, data_size, order, write_index);
        let versioned_block = BlockVersion::Version1(v1);

        let block_header =
            BlockHeaderWithCrc::new(versioned_block).map_err(RingBufferError::Block)?;

        let should_flush = self.should_flush();
        save_block_header_and_data(
            &mut self.file,
            &block_header,
            &data,
            start,
            self.max_file_size,
            should_flush,
        )?;

        let end = start + data_size + block_size;
        self.metadata.file_pointers.write = end % self.max_file_size;

        // should only happen if we wrap around, this is a special case
        // where write has filled whole buffer and is **exactly** on read ptr
        // again as if we just started.
        if self.metadata.file_pointers.write == self.metadata.file_pointers.read_begin {
            self.metadata.can_read_from_wrap_around_when_write_full = true;
        }

        self.wake_up_task();

        self.metadata.order += 1;

        self.flush_state_update(should_flush, 1, total_size, timer.elapsed());

        Ok(Key { offset: key })
    }

    fn batch(&mut self, count: usize) -> PersistResult<VecDeque<(Key, Publication)>> {
        let write_index = self.metadata.file_pointers.write;
        let read_index = self.metadata.file_pointers.read_begin;

        let block_size = *SERIALIZED_BLOCK_SIZE;

        // If read would go into where writes are happening then we don't have data to read.
        if self.metadata.file_pointers.read_end == write_index
            && !self.metadata.can_read_from_wrap_around_when_write_full
        {
            return Ok(VecDeque::new());
        }

        // If we are reading then we can reset wrap around flag
        self.metadata.can_read_from_wrap_around_when_write_full = false;

        let mut start = read_index;
        let mut vdata = VecDeque::new();
        let mut reader = BufReader::with_capacity(page_size::get(), &mut self.file);
        for _ in 0..count {
            let block = load_block_header(&mut reader, start, block_size, self.max_file_size)?;

            // this means we read bytes that don't make a block, this is
            // a really bad state to be in as somehow the pointers don't
            // match to where data really is at.
            let BlockVersion::Version1(inner_block) = block.inner();
            if inner_block.hint() != BLOCK_HINT {
                return Err(RingBufferError::Validate(BlockError::Hint).into());
            }

            let data_size = inner_block.data_size();
            let index = inner_block.write_index();
            start += block_size;
            let end = start + data_size;
            let publication = load_data(&mut reader, start, data_size, self.max_file_size)?;

            // Update start for the next block.
            start = end % self.max_file_size;
            self.metadata.file_pointers.read_end = start;

            // Validate the block and data. This should be data section in the file
            // after a `BlockHeaderWithCrc`.
            validate(&block, &bincode::serialize(&publication)?)?;

            let key = Key { offset: index };

            vdata.push_back((key, publication));

            self.has_read = true;

            // This case shouldn't be pending, as we must have gotten something we can process.
            if start == write_index {
                break;
            }
        }

        Ok(vdata)
    }

    fn remove(&mut self, key: Key) -> PersistResult<()> {
        if !self.has_read {
            return Err(PersistError::RingBuffer(RingBufferError::RemoveBeforeRead));
        }
        let timer = Instant::now();
        let read_index = self.metadata.file_pointers.read_begin;
        let key = key.offset;
        if key != read_index {
            return Err(PersistError::RingBuffer(RingBufferError::RemovalIndex));
        }

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let start = key;
        let mut block = load_block_header(&mut self.file, start, block_size, self.max_file_size)
            .map_err(PersistError::Serialization)?;

        let BlockVersion::Version1(inner_block) = block.inner_mut();
        if inner_block.hint() != BLOCK_HINT {
            return Err(PersistError::RingBuffer(RingBufferError::NonExistantKey));
        }

        inner_block.set_should_not_overwrite(false);

        let data_size = inner_block.data_size();

        let should_flush = self.should_flush();
        save_block_header(
            &mut self.file,
            &block,
            start,
            self.max_file_size,
            should_flush,
        )?;

        let end = start + block_size + data_size;
        self.metadata.file_pointers.read_begin = end % self.max_file_size;

        self.flush_state_update(should_flush, 0, 0, timer.elapsed());

        if self.metadata.file_pointers.read_end == self.metadata.file_pointers.read_begin {
            self.has_read = false;
        }

        Ok(())
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

fn create_file(file_path: &PathBuf) -> IOResult<File> {
    if let Some(parent) = file_path.parent() {
        create_dir_all(parent)?;
    }
    OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(file_path)
}

fn find_pointers_and_order_post_crash(file: &mut File, max_file_size: u64) -> RingBufferMetadata {
    let mut reader = BufReader::with_capacity(page_size::get(), file);
    let mut block = match find_first_block(&mut reader) {
        Ok(block) => block,
        // If we didn't find a block at all, start as if everything is fresh.
        Err(_) => {
            return RingBufferMetadata::default();
        }
    };

    // Now that a block has been found, we can find the last write.
    // We can scan each block we find and check the order on it, if
    // we find a block that has any of the following we can stop searching:
    // 1. An order that is less than current block (wrap-around)
    // 2. All zero memory, which is nothing was written
    // 3. Failure to load block, which could be data corruption, but we can
    // at least serve new data.
    // Note read pointer is found a different way.
    // Read is found by scanning blocks marked for overwrite
    // and taking the value of the first non-overwrite block.

    let BlockVersion::Version1(inner) = block.inner();

    let block_size = *SERIALIZED_BLOCK_SIZE;
    let mut start = inner.write_index();
    let mut end = start + block_size;
    let mut read = 0;
    let mut write = 0;
    let mut order = inner.order();
    let mut should_update_write = true;

    // if we have more write_updates than read_updates and write == read
    // then we must be in wrap_around case.
    let mut write_updates = 0;
    let mut read_updates = 0;
    loop {
        let BlockVersion::Version1(inner) = block.inner();

        // We encountered memory without a block so should just return.
        if inner.hint() != BLOCK_HINT {
            return RingBufferMetadata {
                file_pointers: FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
                // Check to see if wrap around case.
                can_read_from_wrap_around_when_write_full: (write == read
                    && write_updates > read_updates),
            };
        }

        let found_overwrite_block = !inner.should_not_overwrite();
        let data_size = inner.data_size();

        // Found a block that was removed, so update the read pointer.
        if found_overwrite_block && inner.hint() == BLOCK_HINT {
            read_updates += 1;
            read = end + data_size;
        }

        // Check next block
        write_updates += 1;
        start = end + data_size;
        end = start + block_size;

        // Found the last write, can stop updating write.
        if should_update_write && inner.order() < order {
            should_update_write = false;

            // If we haven't found any reads but we have found an order
            // less than the last one, then we must have read and removed
            // previous data. So, read should start at write and we
            // are full.
            if read_updates == 0 {
                read = write;
            }
        }
        if should_update_write {
            write = start % max_file_size;
            order += 1; // We want to start with the next order number.
        }

        if start >= max_file_size {
            return RingBufferMetadata {
                file_pointers: FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
                // Check to see if wrap around case.
                can_read_from_wrap_around_when_write_full: (write == read
                    && write_updates > read_updates),
            };
        }

        block = match load_block_header(&mut reader, start, block_size, max_file_size) {
            Ok(block) => block,
            Err(_) => {
                return RingBufferMetadata {
                    file_pointers: FilePointers {
                        write,
                        read_begin: read,
                        read_end: read,
                    },
                    order,
                    // Check to see if wrap around case.
                    can_read_from_wrap_around_when_write_full: (write == read
                        && write_updates > read_updates),
                };
            }
        };
    }
}

fn find_first_block<T>(readable: &mut T) -> BincodeResult<BlockHeaderWithCrc>
where
    T: Read + Seek,
{
    // We don't need any explicit returns elsewhere as EOF should be an ERR.
    #[allow(clippy::cast_possible_truncation)]
    let all_zeroes: Vec<u8> = vec![0; *SERIALIZED_BLOCK_SIZE as usize];
    loop {
        #[allow(clippy::cast_possible_truncation)]
        let mut buf = vec![0; *SERIALIZED_BLOCK_SIZE as usize];
        readable.read_exact(&mut buf)?;

        // If all zeroes we can skip faster.
        if buf == all_zeroes {
            // This is okay since the block size is too small to wrap.
            #[allow(clippy::cast_possible_wrap)]
            readable.seek(SeekFrom::Current(*SERIALIZED_BLOCK_SIZE as i64))?;
            continue;
        }

        if let Ok(block) = bincode::deserialize::<BlockHeaderWithCrc>(&buf) {
            let BlockVersion::Version1(inner) = block.inner();
            // We maybe found a block.
            if inner.hint() == BLOCK_HINT {
                return Ok(block);
            }
        }

        readable.seek(SeekFrom::Current(1))?;
    }
}

fn load_block_header<T>(
    readable: &mut T,
    mut start: u64,
    size: u64,
    max_size: u64,
) -> BincodeResult<BlockHeaderWithCrc>
where
    T: Read + Seek,
{
    if start > max_size {
        start %= max_size;
    }
    let end = start + size;

    if end > max_size {
        read_wrap_around(readable, start, size, max_size)
    } else {
        read(readable, start, size)
    }
}

fn save_block_header<T>(
    writable: &mut T,
    block: &BlockHeaderWithCrc,
    start: u64,
    max_size: u64,
    should_flush: bool,
) -> PersistResult<()>
where
    T: Write + Seek,
{
    let bytes = bincode::serialize(block)?;
    write(writable, start, &bytes, max_size, should_flush)
}

fn save_block_header_and_data<T>(
    writable: &mut T,
    block: &BlockHeaderWithCrc,
    serialized_data: &[u8],
    start: u64,
    max_size: u64,
    should_flush: bool,
) -> PersistResult<()>
where
    T: Write + Seek,
{
    let mut bytes = bincode::serialize(block)?;
    bytes.extend_from_slice(serialized_data);
    write(writable, start, &bytes, max_size, should_flush)
}

fn load_data<T>(
    readable: &mut T,
    mut start: u64,
    size: u64,
    max_size: u64,
) -> BincodeResult<Publication>
where
    T: Read + Seek,
{
    if start > max_size {
        start %= max_size;
    }
    let end = start + size;

    if end > max_size {
        read_wrap_around(readable, start, size, max_size)
    } else {
        read(readable, start, size)
    }
}

fn read<R, T>(readable: &mut R, start: u64, size: u64) -> BincodeResult<T>
where
    R: Read + Seek,
    T: DeserializeOwned,
{
    readable.seek(SeekFrom::Start(start))?;
    #[allow(clippy::cast_possible_truncation)]
    let mut buf = vec![0; size as usize];
    readable.read_exact(&mut buf)?;
    bincode::deserialize::<T>(&buf)
}

fn read_wrap_around<R, T>(
    readable: &mut R,
    start: u64,
    size: u64,
    max_size: u64,
) -> BincodeResult<T>
where
    R: Read + Seek,
    T: DeserializeOwned,
{
    let end = start + size;
    let split = end - max_size;
    #[allow(clippy::cast_possible_truncation)]
    let first_half_size = (max_size - start) as usize;
    #[allow(clippy::cast_possible_truncation)]
    let second_half_size = split as usize;

    readable.seek(SeekFrom::Start(start))?;
    let mut first_half = vec![0; first_half_size];
    readable.read_exact(&mut first_half)?;

    readable.seek(SeekFrom::Start(0))?;
    let mut second_half = vec![0; second_half_size];
    readable.read_exact(&mut second_half)?;

    first_half.extend_from_slice(&second_half);
    let buf = first_half;

    bincode::deserialize::<T>(&buf)
}

fn write<T>(
    writable: &mut T,
    start: u64,
    bytes: &[u8],
    max_size: u64,
    should_flush: bool,
) -> PersistResult<()>
where
    T: Write + Seek,
{
    let end = start + bytes.len() as u64;
    if end > max_size {
        #[allow(clippy::cast_possible_truncation)]
        let split = (end - max_size) as usize;
        let bytes_split = bytes.len() - split;

        let first_half = &bytes[..bytes_split];
        writable
            .seek(SeekFrom::Start(start))
            .map_err(RingBufferError::FileIO)?;
        writable
            .write(first_half)
            .map_err(RingBufferError::FileIO)?;

        let second_half = &bytes[bytes_split..];
        writable
            .seek(SeekFrom::Start(0))
            .map_err(RingBufferError::FileIO)?;
        writable
            .write(second_half)
            .map_err(RingBufferError::FileIO)?;
    } else {
        writable
            .seek(SeekFrom::Start(start))
            .map_err(RingBufferError::FileIO)?;
        writable.write(bytes).map_err(RingBufferError::FileIO)?;
    }
    if should_flush {
        writable.flush().map_err(RingBufferError::FileIO)?;
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

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::AfterEachWrite;
    const MAX_FILE_SIZE: u64 = 1024 * 1024;
    const MAX_FILE_SIZE_HALF: u64 = MAX_FILE_SIZE / 2;
    const MAX_FILE_SIZE_NON_ZERO: NonZeroU64 = unsafe { NonZeroU64::new_unchecked(MAX_FILE_SIZE) };
    const MAX_FILE_SIZE_HALF_NON_ZERO: NonZeroU64 =
        unsafe { NonZeroU64::new_unchecked(MAX_FILE_SIZE_HALF) };

    struct TestRingBuffer(RingBuffer);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let result = tempfile::NamedTempFile::new();
            assert_matches!(result, Ok(_));
            let file = result.unwrap();
            let file_path = file.path().to_path_buf();

            let result = RingBuffer::new(&file_path, MAX_FILE_SIZE_NON_ZERO, FLUSH_OPTIONS);
            assert_matches!(result, Ok(_));
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
                let result = RingBuffer::new(
                    &file.path().to_path_buf(),
                    MAX_FILE_SIZE_NON_ZERO,
                    FLUSH_OPTIONS,
                );
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                for _ in 0..100 {
                    let result = rb.insert(&publication);
                    assert_matches!(result, Ok(_));
                    keys.push(result.unwrap());
                }

                let result = rb.batch(40);
                assert_matches!(result, Ok(_));
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 40);
                for key in &keys[..40] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }

                for key in &keys[..39] {
                    assert_matches!(rb.remove(*key), Ok(_));
                }

                read = rb.metadata.file_pointers.read_begin;
                write = rb.metadata.file_pointers.write;
                assert_eq!(rb.metadata.order, 100);
            }
            // Create ring buffer again and validate pointers match where they left off.
            {
                let result = RingBuffer::new(
                    &file.path().to_path_buf(),
                    MAX_FILE_SIZE_NON_ZERO,
                    FLUSH_OPTIONS,
                );
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let loaded_read = rb.metadata.file_pointers.read_begin;
                let loaded_write = rb.metadata.file_pointers.write;
                assert_eq!(read, loaded_read);
                assert_eq!(write, loaded_write);
                let result = rb.batch(61);
                assert_matches!(result, Ok(_));
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 61);
                for key in &keys[39..] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                    assert_matches!(rb.remove(*key), Ok(_));
                }
                assert_eq!(rb.metadata.order, 100);
            }
        });
        assert_matches!(result, Ok(_));
    }

    #[test]
    fn it_inits_ok_with_previous_data_and_write_pointer_before_read_pointer() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let read;
        let write;
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            // write some
            for _ in 0..10 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
            }

            let result = rb.batch(10);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for (key, _) in batch.drain(..) {
                let result = rb.remove(key);
                assert_matches!(result, Ok(_));
            }

            // write till wrap around
            // this will put write before read
            loop {
                let before_write_index = rb.metadata.file_pointers.write;

                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));

                let after_write_index = rb.metadata.file_pointers.write;
                if after_write_index < before_write_index {
                    break;
                }
            }

            // Now we simulate a 'crash' and should be able to get
            // correct pointers and read again.
            read = rb.metadata.file_pointers.read_begin;
            write = rb.metadata.file_pointers.write;
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let rb = result.unwrap();

            let loaded_read = rb.metadata.file_pointers.read_begin;
            let loaded_write = rb.metadata.file_pointers.write;
            let loaded_can_read_from_wrap_around_when_write_full =
                rb.metadata.can_read_from_wrap_around_when_write_full;
            assert_eq!(read, loaded_read);
            assert_eq!(write, loaded_write);
            assert!(!loaded_can_read_from_wrap_around_when_write_full);
        }
    }

    #[test]
    fn it_inits_ok_with_previous_data_and_write_pointer_is_reaches_read_pointer_after_read() {
        let file = tempfile::NamedTempFile::new().unwrap();

        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let data = bincode::serialize(&publication).unwrap();

        let data_size = data.len() as u64;

        let total_size = block_size + data_size;

        let max_file_size = NonZeroU64::new(total_size * 20).unwrap();

        let read;
        let mut write;
        {
            let mut rb =
                RingBuffer::new(&file.path().to_path_buf(), max_file_size, FLUSH_OPTIONS).unwrap();

            // write some
            for _ in 0..10 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
            }

            let mut batch = rb.batch(10).unwrap();
            assert!(!batch.is_empty());

            for (key, _) in batch.drain(..) {
                rb.remove(key)
                    .unwrap_or_else(|_| panic!(format!("unable to remove pub with key {:?}", key)));
            }

            read = rb.metadata.file_pointers.read_begin;

            // write till wrap around
            // this will put write at read
            loop {
                // ignore err if any here, we just want to be full
                let _result = rb.insert(&publication);

                write = rb.metadata.file_pointers.write;
                if write == read {
                    break;
                }
            }

            // Now we simulate a 'crash' and should be able to get
            // correct pointers and read again.
        }
        {
            let rb =
                RingBuffer::new(&file.path().to_path_buf(), max_file_size, FLUSH_OPTIONS).unwrap();

            let loaded_read = rb.metadata.file_pointers.read_begin;
            let loaded_write = rb.metadata.file_pointers.write;
            let loaded_can_read_from_wrap_around_when_write_full =
                rb.metadata.can_read_from_wrap_around_when_write_full;
            assert_eq!(write, loaded_write);
            assert_eq!(read, loaded_read);
            assert!(loaded_can_read_from_wrap_around_when_write_full);
            // We would have written 10 + 20 entries, so the next one if there were a write
            // should be 30. Order starts at 0.
            assert_eq!(rb.metadata.order, 30);
        }
    }

    #[test]
    fn it_inits_err_with_less_max_size_than_previous() {
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let mut keys = vec![];
        // Create ring buffer and perform some operations and then destruct.
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
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
                let key = keys[0];
                assert_matches!(rb.remove(key), Ok(_));
            }

            {
                let key = keys[1];
                assert_matches!(rb.remove(key), Ok(_));
            }

            {
                let key = keys[2];
                assert_matches!(rb.remove(key), Ok(_));
            }

            assert_eq!(rb.metadata.order, 10);
        }
        // Create ring buffer again and validate pointers match where they left off.
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_HALF_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert_matches!(
                result,
                Err(PersistError::RingBuffer(RingBufferError::FileTruncation {
                    current: MAX_FILE_SIZE,
                    new: MAX_FILE_SIZE_HALF,
                }))
            );
        }
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
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
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
                let result = rb.remove(*key);
                assert_matches!(result, Ok(_));
            }
            assert_eq!(rb.metadata.order, 5);
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();
            assert_eq!(rb.metadata.order, 5);

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
            assert_eq!(rb.metadata.order, 10);
        }
    }

    #[test]
    fn it_inserts_ok_after_leftover_and_wrap_around_is_smaller() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let result = bincode::serialize(&publication);
        let data = result.unwrap();

        let data_size = data.len() as u64;

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            rb.0.insert(&publication)
                .expect("Failed to insert into RingBuffer");
        }

        let result = rb.0.batch(2);
        let batch = result.unwrap();
        for entry in batch {
            rb.0.remove(entry.0)
                .expect("Failed to remove from RingBuffer");
        }

        let smaller_publication = Publication {
            topic_name: "t".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        rb.0.insert(&smaller_publication)
            .expect("Failed to insert into RingBuffer");

        rb.0.insert(&publication)
            .expect("Failed to insert into RingBuffer");

        assert_eq!(rb.0.metadata.order, inserts + 2);
        let result = rb.0.insert(&publication);
        assert_matches!(result, Err(PersistError::RingBuffer(RingBufferError::Full)));
        assert_eq!(rb.0.metadata.order, inserts + 2);
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

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));
        let data = result.unwrap();

        let data_size = data.len() as u64;

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            let result = rb.0.insert(&publication);
            assert_matches!(result, Ok(_));
        }
        assert_eq!(rb.0.metadata.order, inserts);
        let result = rb.0.insert(&publication);
        assert_matches!(result, Err(PersistError::RingBuffer(RingBufferError::Full)));
        assert_eq!(rb.0.metadata.order, inserts);
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

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));
        let data = result.unwrap();

        let data_size = data.len() as u64;

        let total_size = block_size + data_size;

        let inserts = MAX_FILE_SIZE / total_size;
        for _ in 0..inserts {
            let result = rb.0.insert(&publication);
            assert_matches!(result, Ok(_));
        }
        assert_eq!(rb.0.metadata.order, inserts);

        let result = rb.0.batch(1);
        assert_matches!(result, Ok(_));
        let batch = result.unwrap();

        let result = rb.0.remove(batch[0].0);
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
        assert_matches!(result, Err(PersistError::RingBuffer(RingBufferError::Full)));
        assert_eq!(rb.0.metadata.order, inserts);
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
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
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
                let result = rb.remove(*key);
                assert_matches!(result, Ok(_));
            }
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
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
    fn it_batches_ok_when_leftover_from_file_and_write_only_wrap_around() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let mut counter = 0;

        let mut entries = vec![];
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            // write some
            for _ in 0..10 {
                let publication = Publication {
                    topic_name: counter.to_string(),
                    qos: QoS::AtMostOnce,
                    retain: true,
                    payload: Bytes::new(),
                };
                counter += 1;
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                let key = result.unwrap();
                entries.push((key, publication));
            }

            let result = rb.batch(10);
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            assert!(!batch.is_empty());

            for (key, publication) in entries.drain(..) {
                let maybe_entry = batch.remove(0);
                assert!(maybe_entry.is_some());
                let entry = maybe_entry.unwrap();
                assert_eq!(key, entry.0);
                assert_eq!(publication, entry.1);
                let result = rb.remove(key);
                assert_matches!(result, Ok(_));
            }

            // write till wrap around
            // this will put write before read
            loop {
                let before_write_index = rb.metadata.file_pointers.write;

                let publication = Publication {
                    topic_name: counter.to_string(),
                    qos: QoS::AtMostOnce,
                    retain: true,
                    payload: Bytes::new(),
                };
                counter += 1;

                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));

                let after_write_index = rb.metadata.file_pointers.write;

                let key = result.unwrap();
                entries.push((key, publication));
                if after_write_index < before_write_index {
                    break;
                }
            }
            // Now we simulate a 'crash' and should be able to get
            // correct pointers and read again.
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();

            let result = rb.batch(entries.len());
            assert_matches!(result, Ok(_));
            let mut batch = result.unwrap();
            while !batch.is_empty() {
                for (key, publication) in entries.drain(..) {
                    let maybe_entry = batch.remove(0);
                    assert!(maybe_entry.is_some());
                    let entry = maybe_entry.unwrap();
                    assert_eq!(key, entry.0);
                    assert_eq!(publication, entry.1);
                }
            }
        }
    }

    #[test]
    fn it_batches_err_when_bad_block_from_file() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            MAX_FILE_SIZE_NON_ZERO,
            FLUSH_OPTIONS,
        );
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
        assert_matches!(result, Err(_));
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
        let block_size = *SERIALIZED_BLOCK_SIZE;

        let data_size = data.len() as u64;
        let file_size = 10 * (block_size + data_size);

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            NonZeroU64::new(file_size).unwrap(),
            FLUSH_OPTIONS,
        );
        assert!(result.is_ok());
        let mut rb = result.unwrap();
        assert_eq!(rb.metadata.can_read_from_wrap_around_when_write_full, false);

        let mut keys = vec![];
        for _ in 0..10 {
            let result = rb.insert(&publication);
            assert_matches!(result, Ok(_));
            keys.push(result.unwrap());
        }
        assert_eq!(rb.metadata.can_read_from_wrap_around_when_write_full, true);

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
    fn it_batches_ok_when_write_reaches_begin_again_after_leftover() {
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let result = bincode::serialize(&publication);
        assert_matches!(result, Ok(_));

        let data = result.unwrap();
        let block_size = *SERIALIZED_BLOCK_SIZE;
        let data_size = data.len() as u64;
        let file_size = 10 * (block_size + data_size);

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let mut keys = vec![];
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                NonZeroU64::new(file_size).unwrap(),
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();
            assert_eq!(rb.metadata.can_read_from_wrap_around_when_write_full, false);
            for _ in 0..10 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                keys.push(result.unwrap());
            }
            assert_eq!(rb.metadata.can_read_from_wrap_around_when_write_full, true);
        }

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            NonZeroU64::new(file_size).unwrap(),
            FLUSH_OPTIONS,
        );
        assert!(result.is_ok());
        let mut rb = result.unwrap();
        assert_eq!(rb.metadata.can_read_from_wrap_around_when_write_full, true);

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
        let result = rb.0.remove(Key { offset: 1 });
        assert_matches!(result, Err(_));
        assert_matches!(
            result.unwrap_err(),
            PersistError::RingBuffer(RingBufferError::RemoveBeforeRead)
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

        let result = rb.0.remove(Key { offset: 1 });
        assert_matches!(result, Err(_));
        assert_matches!(
            result.unwrap_err(),
            PersistError::RingBuffer(RingBufferError::RemovalIndex)
        );
    }

    #[test]
    fn it_errs_on_remove_with_key_that_does_not_exist() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            MAX_FILE_SIZE_NON_ZERO,
            FLUSH_OPTIONS,
        );
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

        let result = rb.remove(Key { offset: 0 });
        assert_matches!(result, Err(_));
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

            let result = rb.0.remove(key);
            assert_matches!(result, Ok(_));
        }
    }
}
