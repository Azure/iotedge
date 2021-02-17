mod block;
pub mod error;
pub mod flush;

use std::{
    collections::VecDeque,
    fmt::Debug,
    fs::{create_dir_all, File, OpenOptions},
    io::{Read, Result as IOResult, Seek, SeekFrom, Write},
    num::NonZeroU64,
    path::PathBuf,
    task::Waker,
    time::{Duration, Instant},
};

use block::BlockHeaderV1;
use mqtt3::proto::Publication;

use bincode::Result as BincodeResult;
use serde::{de::DeserializeOwned, Deserialize, Serialize};
use tracing::error;

use crate::persist::{
    waking_state::ring_buffer::{
        block::{
            calculate_hash, validate, BlockHeaderWithHash, BlockVersion, BLOCK_HINT,
            SERIALIZED_BLOCK_SIZE,
        },
        error::{BlockError, RingBufferError},
        flush::{FlushOptions, FlushState},
    },
    Key, StorageError,
};

pub type StorageResult<T> = Result<T, StorageError>;

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
    can_read_from_wrap_around: bool,

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
    // `RingBuffer` as well as saving state externally.
    metadata: RingBufferMetadata,

    // Stateful file for holding metadata.
    metadata_file: File,

    // A waker for updating any pending batch after an insert.
    waker: Option<Waker>,
}

impl RingBuffer {
    pub(crate) fn new(
        file_path: &PathBuf,
        metadata_file_path: &PathBuf,
        max_file_size: NonZeroU64,
        flush_options: FlushOptions,
    ) -> StorageResult<Self> {
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

        let mut metadata_file =
            create_file(metadata_file_path).map_err(RingBufferError::FileCreate)?;

        // Retrieving from metadata file is only best effort.
        let best_guess_metadata = retrieve_ring_buffer_metadata(&mut metadata_file)?;

        // For correctness, need to scan after best guess and see if can get more accurate.
        let metadata =
            find_pointers_and_order_post_crash(&mut file, max_file_size, best_guess_metadata);

        file.seek(SeekFrom::Start(0)).unwrap();
        let mut buf = vec![];
        file.read_to_end(&mut buf).unwrap();

        // Put file back at correct position.
        file.seek(SeekFrom::Start(metadata.file_pointers.write))
            .map_err(RingBufferError::FileIO)?;

        Ok(Self {
            flush_options,
            flush_state: FlushState::default(),
            has_read: false,
            max_file_size,
            file,
            metadata,
            metadata_file,
            waker: None,
        })
    }

    fn insert(&mut self, publication: &Publication) -> StorageResult<Key> {
        let timer = Instant::now();
        let data = bincode::serialize(publication)?;

        let data_size = data.len() as u64;
        let data_hash = calculate_hash(&data);

        let write_index = self.metadata.file_pointers.write;
        let read_index = self.metadata.file_pointers.read_begin;
        let order = self.metadata.order;
        let key = write_index;

        let v1 = BlockHeaderV1::new(BLOCK_HINT, data_hash, data_size, order, write_index);
        let versioned_block = BlockVersion::Version1(v1);

        let block_header = BlockHeaderWithHash::new(versioned_block);

        let block_size = *SERIALIZED_BLOCK_SIZE;
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
        let result = load_block_header(&mut self.file, start, block_size, self.max_file_size);
        if let Ok(block_header) = result {
            let BlockVersion::Version1(inner) = block_header.inner();
            let should_not_overwrite = inner.should_not_overwrite();
            if should_not_overwrite {
                return Err(StorageError::RingBuffer(RingBufferError::Full));
            }
        }

        let should_flush = self.should_flush();

        save_block_header(
            &mut self.file,
            &block_header,
            start,
            self.max_file_size,
            should_flush,
        )?;

        let mut end = start + block_size;
        start = end % self.max_file_size;

        save_data(
            &mut self.file,
            &data,
            start,
            self.max_file_size,
            should_flush,
        )?;

        end = start + data_size;
        self.metadata.file_pointers.write = end % self.max_file_size;

        // should only happen if we wrap around, this is a special case
        // where write has filled whole buffer and is **exactly** on read ptr
        // again as if we just started.
        if self.metadata.file_pointers.write == self.metadata.file_pointers.read_begin {
            self.metadata.can_read_from_wrap_around = true;
        }

        self.wake_up_task();

        self.metadata.order += 1;

        self.flush_state_update(should_flush, 1, total_size, timer.elapsed());

        Ok(Key { offset: key })
    }

    fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        let write_index = self.metadata.file_pointers.write;
        let read_index = self.metadata.file_pointers.read_begin;

        let block_size = *SERIALIZED_BLOCK_SIZE;

        // If read would go into where writes are happening then we don't have data to read.
        if self.metadata.file_pointers.read_end == write_index
            && !self.metadata.can_read_from_wrap_around
        {
            return Ok(VecDeque::new());
        }

        // If we are reading then we can reset wrap around flag
        self.metadata.can_read_from_wrap_around = false;

        let mut start = read_index;
        let mut vdata = VecDeque::new();
        for _ in 0..count {
            let block = load_block_header(&mut self.file, start, block_size, self.max_file_size)?;

            // this means we read bytes that don't make a block, this is
            // a really bad state to be in as somehow the pointers don't
            // match to where data really is at.
            let BlockVersion::Version1(inner_block) = block.inner();
            if inner_block.hint() != BLOCK_HINT {
                self.file.seek(SeekFrom::Start(0)).unwrap();
                let mut buf = vec![];
                self.file.read_to_end(&mut buf).unwrap();

                return Err(RingBufferError::Validate(BlockError::Hint).into());
            }

            let data_size = inner_block.data_size();
            let index = inner_block.write_index();
            start += block_size;
            let end = start + data_size;
            let publication = load_data(&mut self.file, start, data_size, self.max_file_size)?;

            // Update start for the next block.
            start = end % self.max_file_size;
            self.metadata.file_pointers.read_end = start;

            // Validate the block and data. This should be data section in the file
            // after a `BlockHeaderWithHash`.
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

    fn remove(&mut self, key: u64) -> StorageResult<()> {
        if !self.has_read {
            return Err(StorageError::RingBuffer(RingBufferError::RemoveBeforeRead));
        }
        let timer = Instant::now();
        let read_index = self.metadata.file_pointers.read_begin;
        if key != read_index {
            return Err(StorageError::RingBuffer(RingBufferError::RemovalIndex));
        }

        let block_size = *SERIALIZED_BLOCK_SIZE;

        let start = key;
        let mut block = load_block_header(&mut self.file, start, block_size, self.max_file_size)
            .map_err(StorageError::Serialization)?;

        let BlockVersion::Version1(inner_block) = block.inner_mut();
        if inner_block.hint() != BLOCK_HINT {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
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

    fn save_ring_buffer_metadata(&mut self) -> BincodeResult<()> {
        let buf = bincode::serialize(&self.metadata)?;
        self.metadata_file.seek(SeekFrom::Start(0))?;
        self.metadata_file.write_all(&buf)?;
        self.metadata_file.flush()?;
        Ok(())
    }
}

impl Drop for RingBuffer {
    fn drop(&mut self) {
        let result = self.file.flush();
        if let Some(err) = result.err() {
            error!("Failed to flush {:?}", err);
        }
        let result = self.save_ring_buffer_metadata();
        if let Some(err) = result.err() {
            error!("Failed to flush metadata {:?}", err);
        }
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

fn retrieve_ring_buffer_metadata(file: &mut File) -> BincodeResult<RingBufferMetadata> {
    let mut buf = vec![];
    file.read_to_end(&mut buf)?;
    if buf.is_empty() {
        return Ok(RingBufferMetadata::default());
    }
    bincode::deserialize::<RingBufferMetadata>(&buf)
}

fn find_pointers_and_order_post_crash(
    file: &mut File,
    max_file_size: u64,
    best_guess_metadata: RingBufferMetadata,
) -> RingBufferMetadata {
    let block_size = *SERIALIZED_BLOCK_SIZE;

    let mut start = best_guess_metadata.file_pointers.write;
    let mut end = start + block_size;
    let mut read = best_guess_metadata.file_pointers.read_begin;
    let mut write = best_guess_metadata.file_pointers.write;
    let mut order = best_guess_metadata.order;
    let can_read_from_wrap_around = best_guess_metadata.can_read_from_wrap_around;

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

    // if we have more write_updates than read_updates and write == read
    // then we must be in wrap_around case.
    let mut write_updates = 0;
    let mut read_updates = 0;
    let mut block = match load_block_header(file, start, block_size, max_file_size) {
        Ok(block) => block,
        Err(_) => {
            // Failed to load block, so go with what we already have.

            return best_guess_metadata;
        }
    };

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
                can_read_from_wrap_around: can_read_from_wrap_around
                    || (write == read && write_updates > read_updates),
            };
        }

        let found_overwrite_block = !inner.should_not_overwrite();
        let data_size = inner.data_size();

        // Found a block that was removed, so update the read pointer.
        if found_overwrite_block && inner.hint() == BLOCK_HINT {
            read_updates += 1;
            read = end + data_size;
        }

        // Found the last write, take whatever we got for read and write
        // and return the pointers.
        if inner.order() < order {
            write = start;

            return RingBufferMetadata {
                file_pointers: FilePointers {
                    write,
                    read_begin: read,
                    read_end: read,
                },
                order,
                // Check to see if wrap around case.
                can_read_from_wrap_around: can_read_from_wrap_around
                    || (write == read && write_updates > read_updates),
            };
        }

        // Check next block
        write_updates += 1;
        order += 1; // We want to start with the next order number.
        start = (end + data_size) % max_file_size;
        end = (start + block_size) % max_file_size;

        block = match load_block_header(file, start, block_size, max_file_size) {
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
                    can_read_from_wrap_around: can_read_from_wrap_around
                        || (write == read && write_updates > read_updates),
                };
            }
        };
    }
}

fn load_block_header(
    file: &mut File,
    mut start: u64,
    size: u64,
    file_size: u64,
) -> BincodeResult<BlockHeaderWithHash> {
    if start > file_size {
        start %= file_size;
    }
    let end = start + size as u64;

    if end > file_size {
        file_read_wrap_around(file, start, size, file_size)
    } else {
        file_read(file, start, size)
    }
}

fn save_block_header(
    file: &mut File,
    block: &BlockHeaderWithHash,
    start: u64,
    file_size: u64,
    should_flush: bool,
) -> StorageResult<()> {
    let bytes = bincode::serialize(block)?;
    file_write(file, start, &bytes, file_size, should_flush)
}

fn save_data(
    file: &mut File,
    serialized_data: &[u8],
    start: u64,
    file_size: u64,
    should_flush: bool,
) -> StorageResult<()> {
    file_write(file, start, &serialized_data, file_size, should_flush)
}

fn load_data(
    file: &mut File,
    mut start: u64,
    size: u64,
    file_size: u64,
) -> BincodeResult<Publication> {
    if start > file_size {
        start %= file_size;
    }
    let end = start + size as u64;

    if end > file_size {
        file_read_wrap_around(file, start, size, file_size)
    } else {
        file_read(file, start, size)
    }
}

fn file_read<T>(file: &mut File, start: u64, size: u64) -> BincodeResult<T>
where
    T: DeserializeOwned,
{
    file.seek(SeekFrom::Start(start))?;
    #[allow(clippy::cast_possible_truncation)]
    let mut buf = vec![0; size as usize];
    file.read_exact(&mut buf)?;
    bincode::deserialize::<T>(&buf)
}

fn file_read_wrap_around<T>(
    file: &mut File,
    start: u64,
    size: u64,
    file_size: u64,
) -> BincodeResult<T>
where
    T: DeserializeOwned,
{
    let end = start + size as u64;
    let file_split = end - file_size;
    #[allow(clippy::cast_possible_truncation)]
    let first_half_size = (file_size - start) as usize;
    #[allow(clippy::cast_possible_truncation)]
    let second_half_size = file_split as usize;

    file.seek(SeekFrom::Start(start))?;
    let mut first_half = vec![0; first_half_size];
    file.read_exact(&mut first_half)?;

    file.seek(SeekFrom::Start(0))?;
    let mut second_half = vec![0; second_half_size];
    file.read_exact(&mut second_half)?;

    first_half.extend_from_slice(&second_half);
    let buf = first_half;

    bincode::deserialize::<T>(&buf)
}

fn file_write(
    file: &mut File,
    start: u64,
    bytes: &[u8],
    file_size: u64,
    should_flush: bool,
) -> StorageResult<()> {
    let end = start + bytes.len() as u64;
    if end > file_size {
        #[allow(clippy::cast_possible_truncation)]
        let file_split = (end - file_size) as usize;
        let bytes_split = bytes.len() - file_split;

        let first_half = &bytes[..bytes_split];
        file.seek(SeekFrom::Start(start))
            .map_err(RingBufferError::FileIO)?;
        file.write(first_half).map_err(RingBufferError::FileIO)?;

        let second_half = &bytes[bytes_split..];
        file.seek(SeekFrom::Start(0))
            .map_err(RingBufferError::FileIO)?;
        file.write(second_half).map_err(RingBufferError::FileIO)?;
    } else {
        file.seek(SeekFrom::Start(start))
            .map_err(RingBufferError::FileIO)?;
        file.write(bytes).map_err(RingBufferError::FileIO)?;
    }
    if should_flush {
        file.flush().map_err(RingBufferError::FileIO)?;
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
    const MAX_FILE_SIZE: u64 = 1024;
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

            let result = tempfile::NamedTempFile::new();
            assert_matches!(result, Ok(_));
            let file = result.unwrap();
            let metadata_file_path = file.path().to_path_buf();

            let result = RingBuffer::new(
                &file_path,
                &metadata_file_path,
                MAX_FILE_SIZE_NON_ZERO,
                FLUSH_OPTIONS,
            );
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

            let result = tempfile::NamedTempFile::new();
            assert_matches!(result, Ok(_));
            let metadata_file = result.unwrap();

            let mut keys = vec![];
            // Create ring buffer and perform some operations and then destruct.
            {
                let result = RingBuffer::new(
                    &file.path().to_path_buf(),
                    &metadata_file.path().to_path_buf(),
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
                    let key = keys[0].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                {
                    let key = keys[1].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                {
                    let key = keys[2].offset;
                    assert_matches!(rb.remove(key), Ok(_));
                }

                read = rb.metadata.file_pointers.read_begin;
                write = rb.metadata.file_pointers.write;
                assert_eq!(rb.metadata.order, 10);
            }
            // Create ring buffer again and validate pointers match where they left off.
            {
                let result = RingBuffer::new(
                    &file.path().to_path_buf(),
                    &metadata_file.path().to_path_buf(),
                    MAX_FILE_SIZE_NON_ZERO,
                    FLUSH_OPTIONS,
                );
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let loaded_read = rb.metadata.file_pointers.read_begin;
                let loaded_write = rb.metadata.file_pointers.write;
                assert_eq!(read, loaded_read);
                assert_eq!(write, loaded_write);
                let result = rb.batch(2);
                assert_matches!(result, Ok(_));
                let mut batch = result.unwrap();
                assert_eq!(batch.len(), 2);
                for key in &keys[3..5] {
                    assert_eq!(batch.pop_front().unwrap().0, *key);
                }
                assert_eq!(rb.metadata.order, 10);
            }
        });
        assert_matches!(result, Ok(_));
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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

        let mut keys = vec![];
        // Create ring buffer and perform some operations and then destruct.
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                &metadata_file.path().to_path_buf(),
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
                let key = keys[0].offset;
                assert_matches!(rb.remove(key), Ok(_));
            }

            {
                let key = keys[1].offset;
                assert_matches!(rb.remove(key), Ok(_));
            }

            {
                let key = keys[2].offset;
                assert_matches!(rb.remove(key), Ok(_));
            }

            assert_eq!(rb.metadata.order, 10);
        }
        // Create ring buffer again and validate pointers match where they left off.
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                &metadata_file.path().to_path_buf(),
                MAX_FILE_SIZE_HALF_NON_ZERO,
                FLUSH_OPTIONS,
            );
            assert_matches!(
                result,
                Err(StorageError::RingBuffer(RingBufferError::FileTruncation {
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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

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
                &metadata_file.path().to_path_buf(),
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
                let result = rb.remove(key.offset);
                assert_matches!(result, Ok(_));
            }
            assert_eq!(rb.metadata.order, 5);
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                &metadata_file.path().to_path_buf(),
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
        assert_matches!(result, Err(StorageError::RingBuffer(RingBufferError::Full)));
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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

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
                &metadata_file.path().to_path_buf(),
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
                let result = rb.remove(key.offset);
                assert_matches!(result, Ok(_));
            }
        }
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                &metadata_file.path().to_path_buf(),
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
    fn it_batches_err_when_bad_block_from_file() {
        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let file = result.unwrap();

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            &metadata_file.path().to_path_buf(),
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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            &metadata_file.path().to_path_buf(),
            NonZeroU64::new(file_size).unwrap(),
            FLUSH_OPTIONS,
        );
        assert!(result.is_ok());
        let mut rb = result.unwrap();
        assert_eq!(rb.metadata.can_read_from_wrap_around, false);

        let mut keys = vec![];
        for _ in 0..10 {
            let result = rb.insert(&publication);
            assert_matches!(result, Ok(_));
            keys.push(result.unwrap());
        }
        assert_eq!(rb.metadata.can_read_from_wrap_around, true);

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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

        let mut keys = vec![];
        {
            let result = RingBuffer::new(
                &file.path().to_path_buf(),
                &metadata_file.path().to_path_buf(),
                NonZeroU64::new(file_size).unwrap(),
                FLUSH_OPTIONS,
            );
            assert!(result.is_ok());
            let mut rb = result.unwrap();
            assert_eq!(rb.metadata.can_read_from_wrap_around, false);
            for _ in 0..10 {
                let result = rb.insert(&publication);
                assert_matches!(result, Ok(_));
                keys.push(result.unwrap());
            }
            assert_eq!(rb.metadata.can_read_from_wrap_around, true);
        }

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            &metadata_file.path().to_path_buf(),
            NonZeroU64::new(file_size).unwrap(),
            FLUSH_OPTIONS,
        );
        assert!(result.is_ok());
        let mut rb = result.unwrap();
        assert_eq!(rb.metadata.can_read_from_wrap_around, true);

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

        let result = tempfile::NamedTempFile::new();
        assert_matches!(result, Ok(_));
        let metadata_file = result.unwrap();

        let result = RingBuffer::new(
            &file.path().to_path_buf(),
            &metadata_file.path().to_path_buf(),
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

        let result = rb.remove(0);
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

            let result = rb.0.remove(key.offset);
            assert_matches!(result, Ok(_));
        }
    }
}
