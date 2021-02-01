mod block;
pub mod error;
mod flush;
mod serialize;

use crate::persist::{
    Key, StorageError,
};
use bincode::Result as BincodeResult;
use block::{
    calculate_hash, serialized_block_size, validate, BlockHeaderWithHash, Data, BLOCK_HINT,
};
use error::RingBufferError;
use memmap::MmapMut;
// use mmap::{mmap_read, mmap_write};
use mqtt3::proto::Publication;
use parking_lot::Mutex;
use serde::{Deserialize, Serialize};
use std::io::Result as IOResult;
use std::{
    collections::VecDeque,
    fs::{File, OpenOptions},
    path::PathBuf,
    sync::{
        atomic::{AtomicUsize, Ordering},
        Arc,
    },
    fmt::Debug,
    task::Waker,
    time::Instant,
};

use self::{flush::{FlushOptions, FlushState}, serialize::{binary_deserialize, binary_serialize}};

#[allow(dead_code)]
pub type StorageResult<T> = Result<T, StorageError>;



#[allow(dead_code)]
pub(crate) fn mmap_read(mmap: Arc<Mutex<MmapMut>>, start: usize, end: usize) -> Vec<u8> {
    let mmap = mmap.lock();
    let bytes = &mmap[start..end];
    Vec::from(bytes)
}

#[allow(dead_code)]
pub(crate) fn mmap_write(
    mmap: Arc<Mutex<MmapMut>>,
    start: usize,
    end: usize,
    bytes: &[u8],
    should_flush: bool,
) -> StorageResult<()> {
    let mut mmap = mmap.lock();
    mmap[start..end].clone_from_slice(&bytes);
    if should_flush {
        mmap.flush_async_range(start, end - start)
            .map_err(RingBufferError::Flush)?;
    }
    Ok(())
}

#[allow(dead_code)]
fn create_file(file_name: &String) -> IOResult<File> {
    OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(&PathBuf::from(file_name))
}

#[allow(dead_code)]
fn deserialize_block(
    mmap: Arc<Mutex<MmapMut>>,
    start: usize,
    end: usize,
) -> BincodeResult<BlockHeaderWithHash> {
    let bytes = mmap_read(mmap.clone(), start, end);
    binary_deserialize::<BlockHeaderWithHash>(&bytes)
}

#[allow(dead_code)]
fn should_flush(flush_options: FlushOptions, flush_state: &FlushState) -> bool {
    match flush_options {
        FlushOptions::AfterEachWrite => true,
        FlushOptions::AfterXWrites(xwrites) => flush_state.writes.load(Ordering::SeqCst) >= xwrites,
        FlushOptions::AfterXBytes(xbytes) => {
            flush_state.bytes_written.load(Ordering::SeqCst) >= xbytes
        }
        FlushOptions::AfterXMilliseconds(xmillis_elapsed) => {
            flush_state.millis_elapsed.load(Ordering::SeqCst) >= xmillis_elapsed
        }
        FlushOptions::Off => false,
    }
}

#[allow(dead_code)]
fn read_data_maybe_wrap_arond(
    mmap: Arc<Mutex<MmapMut>>,
    start: usize,
    end: usize,
    file_size: usize,
) -> Vec<u8> {
    let mmap = mmap.lock();
    // Need to wrap around
    if end > file_size {
        let file_split = end - file_size;
        let mut v = vec![];
        v.append(&mut Vec::from(&mmap[start..file_size]));
        v.append(&mut Vec::from(&mmap[0..file_split]));
        v
    } else {
        Vec::from(&mmap[start..end])
    }
}

#[allow(dead_code)]
fn write_data_maybe_wrap_around(
    mmap: Arc<Mutex<MmapMut>>,
    start: usize,
    end: usize,
    data: &[u8],
    file_size: usize,
    should_flush: bool,
) -> StorageResult<()> {
    // Need to wrap around
    if end > file_size {
        let file_split = end - file_size;
        let data_split = data.len() - file_split;
        mmap_write(
            mmap.clone(),
            start,
            file_size,
            &data[..data_split],
            should_flush,
        )?;
        mmap_write(
            mmap.clone(),
            0,
            file_split,
            &data[data_split..],
            should_flush,
        )
    } else {
        mmap_write(mmap.clone(), start, end, &data, should_flush)
    }
}

#[allow(dead_code)]
#[derive(Debug, Default, Deserialize, Serialize)]
pub(crate) struct FilePointers {
    write: AtomicUsize,
    read: AtomicUsize,
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
/// It is possible that W could wrap around
/// and cause us to lose data due to variable
/// sized data being written.
/// If W + data size > R and should write precede read,
/// the data could become corrupted.
/// Either this is prevented by scanning the file for
/// the next block and checking if the write will fit.
/// Or, W + data must not pass both D and R.
/// Not passing D helps with guaranteeing all data read was handled.
/// Not passing R helps avoid corruption.
#[allow(dead_code)]
pub(crate) struct RingBuffer {
    flush_options: FlushOptions,
    flush_state: Arc<FlushState>,
    inner: Arc<Mutex<MmapMut>>,
    max_file_size: usize,
    order: AtomicUsize,
    pointers: Arc<FilePointers>,
    waker: Option<Waker>,
}

unsafe impl Send for RingBuffer {}
unsafe impl Sync for RingBuffer {}

#[allow(dead_code)]
fn find_pointers_post_crash(
    mmap: Arc<Mutex<MmapMut>>,
    max_file_size: usize,
) -> StorageResult<FilePointers> {
    let mut block;
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
                write: AtomicUsize::new(write),
                read: AtomicUsize::new(read),
            });
        }
        block = match deserialize_block(mmap.clone(), start, end) {
            Ok(block) => {
                if block.inner().hint() == BLOCK_HINT {
                    block
                } else {
                    start += 1;
                    end += 1;
                    continue;
                }
            }
            Err(_) => {
                start += 1;
                end += 1;
                continue;
            }
        };
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
                write: AtomicUsize::new(write),
                read: AtomicUsize::new(read),
            });
        }

        // Check next block
        order = inner.order();
        start = end + data_size;
        end = start + block_size;
        block = match deserialize_block(mmap.clone(), start, end) {
            Ok(block) => block,
            Err(_) => {
                return Ok(FilePointers {
                    write: AtomicUsize::new(write),
                    read: AtomicUsize::new(read),
                })
            }
        };
    }
}

#[allow(dead_code)]
impl RingBuffer {
    pub fn new(file_name: String, max_file_size: usize, flush_options: FlushOptions) -> Self {
        let file = create_file(&file_name).expect("Failed to create file");
        file.set_len(max_file_size as u64)
            .expect("Failed to set file size");

        let mmap = Arc::new(Mutex::new(unsafe {
            MmapMut::map_mut(&file).expect("Failed to open mmap")
        }));

        let file_pointers = find_pointers_post_crash(mmap.clone(), max_file_size)
            .expect("Failed to init file pointers");
        Self {
            flush_options,
            flush_state: Arc::new(FlushState::new()),
            inner: mmap.clone(),
            max_file_size,
            order: AtomicUsize::default(),
            pointers: Arc::new(file_pointers),
            waker: None,
        }
    }

    fn insert(&mut self, publication: Publication) -> StorageResult<Key> {
        let timer = Instant::now();
        let data = binary_serialize(&publication)?;
        let data = Data::new(data);
        let serialized_data = binary_serialize(&data)?;

        let data_size = serialized_data.len();
        let data_hash = calculate_hash(&data);

        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let read_index = self.pointers.read.load(Ordering::SeqCst);
        let order = self.order.load(Ordering::SeqCst);
        let key = write_index;

        let block_header =
            BlockHeaderWithHash::new(data_hash, data_size, order, read_index, write_index);
        let serialized_block_header = binary_serialize(&block_header)?;
        let block_size = serialized_block_size()?;

        let total_size = block_size + data_size;
        // Check to see if we might corrupt data if we write, if so, return pending.
        // As trying later might be successful.
        if write_index < read_index {
            if write_index + total_size > read_index {
                return Err(StorageError::RingBuffer(RingBufferError::Full));
            }
        }

        let start = write_index;
        let end = start + block_size;

        // Check if an existing block header is present. If the block is there
        // and if the overwrite flag is not set then we shouldn't write.
        let result = deserialize_block(self.inner.clone(), start, end);
        if result.is_ok() {
            let deserialized_block = result.unwrap();
            let should_not_overwrite = deserialized_block.inner().should_not_overwrite();
            if should_not_overwrite {
                return Err(StorageError::RingBuffer(RingBufferError::Full));
            }
        }

        let should_flush = should_flush(self.flush_options, &self.flush_state);

        write_data_maybe_wrap_around(
            self.inner.clone(),
            start,
            end,
            &serialized_block_header,
            self.max_file_size,
            should_flush,
        )?;
        let start = end;
        let end = start + data_size;
        write_data_maybe_wrap_around(
            self.inner.clone(),
            start,
            end,
            &serialized_data,
            self.max_file_size,
            should_flush,
        )?;

        self.pointers
            .write
            .store(end % self.max_file_size, Ordering::SeqCst);

        self.order.store(order + 1, Ordering::SeqCst);

        self.flush_state
            .update(1, total_size, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        self.wake_up_task();

        Ok(Key { offset: key as u64 })
    }

    fn batch(&self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let read_index = self.pointers.read.load(Ordering::SeqCst);
        let block_size = serialized_block_size()?;

        // If read would go into where writes are happening then we don't have data to read.
        if read_index == write_index {
            return Ok(VecDeque::new());
        }

        let mut start = read_index;
        let mut vdata = VecDeque::new();
        for _ in 0..count {
            // This case shouldn't be pending, as we must have gotten something we can process.
            if start >= write_index {
                break;
            }
            let block;
            {
                let mmap = self.inner.lock();
                let end = start + block_size;
                if end > self.max_file_size {
                    return Err(RingBufferError::WrapAround.into());
                }
                let bytes = &mmap[start..end];
                // this is unused memory
                if bytes.into_iter().map(|&x| x as usize).sum::<usize>() == 0 {
                    break;
                }
                block = binary_deserialize::<BlockHeaderWithHash>(bytes)?;
            }
            let data_size = block.inner().data_size();
            let index = block.inner().write_index();
            start = start + block_size;
            let end = start + data_size;
            let bytes =
                read_data_maybe_wrap_arond(self.inner.clone(), start, end, self.max_file_size);
            let data = binary_deserialize::<Data>(&bytes)?;
            start = end % self.max_file_size;

            // to move to beginning of next block header
            // start += 1;
            validate(&block, &data)?;
            let publication = binary_deserialize::<Publication>(data.inner())?;
            let key = Key {
                offset: index as u64,
            };
            vdata.push_back((key, publication));
        }

        Ok(vdata)
    }

    fn remove(&mut self, key: usize) -> StorageResult<()> {
        let timer = Instant::now();
        let read_index = self.pointers.read.load(Ordering::SeqCst);
        if key != read_index {
            return Err(StorageError::RingBuffer(RingBufferError::RemovalIndex));
        }
        let block_size = serialized_block_size()?;

        let start = key;
        let mut end = start + block_size;
        if end > self.max_file_size {
            return Err(StorageError::RingBuffer(RingBufferError::WrapAround.into()));
        }

        let maybe_block = deserialize_block(self.inner.clone(), start, end);
        if maybe_block.is_err() {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
        }

        let data_size;
        let mut block = maybe_block.unwrap();
        if block.inner().hint() != BLOCK_HINT {
            return Err(StorageError::RingBuffer(RingBufferError::NonExistantKey));
        }

        {
            let inner_block = block.inner_mut();
            inner_block.set_should_not_overwrite(false);
            data_size = inner_block.data_size();
        }

        let bytes = binary_serialize(&block)?;
        let should_flush = should_flush(self.flush_options, &self.flush_state);
        mmap_write(self.inner.clone(), start, end, &bytes, should_flush)?;

        end += data_size;

        self.pointers
            .read
            .store(end % self.max_file_size, Ordering::SeqCst);

        self.flush_state
            .update(0, 0, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(())
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }

    fn wake_up_task(&mut self) {
        if let Some(waker) = self.waker.take() {
            waker.wake();
        }
    }
}

#[allow(dead_code)]
impl Drop for RingBuffer {
    fn drop(&mut self) {
        let mmap = self.inner.lock();
        mmap.flush().expect("Failed to flush");
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bytes::Bytes;
    use matches::assert_matches;
    use mqtt3::proto::QoS;
    use rand::{distributions::Alphanumeric, thread_rng, Rng};
    use std::{
        fs::{remove_dir_all, remove_file},
        panic,
    };

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const FILE_NAME: &'static str = "test_file";
    const MAX_FILE_SIZE: usize = 1024;

    fn cleanup_test_file(file_name: String) {
        let path = &PathBuf::from(file_name);
        if path.exists() {
            if path.is_file() {
                let result = remove_file(path);
                assert!(result.is_ok());
            }
            if path.is_dir() {
                let result = remove_dir_all(path);
                assert!(result.is_ok());
            }
        }
    }

    fn create_rand_str() -> String {
        thread_rng()
            .sample_iter(&Alphanumeric)
            .take(10)
            .map(char::from)
            .collect()
    }

    struct TestRingBuffer(RingBuffer, String);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let file_name = FILE_NAME.to_owned() + &create_rand_str();
            Self::new(file_name)
        }
    }

    impl Drop for TestRingBuffer {
        fn drop(&mut self) {
            cleanup_test_file(self.1.clone())
        }
    }

    #[allow(dead_code)]
    impl TestRingBuffer {
        fn new(file_name: String) -> Self {
            let rb = RingBuffer::new(file_name.clone(), MAX_FILE_SIZE, FLUSH_OPTIONS);
            TestRingBuffer(rb, file_name.clone())
        }

        fn insert(&mut self, value: Publication) -> StorageResult<Key> {
            self.0.insert(value)
        }

        fn batch(&mut self, count: usize) -> StorageResult<VecDeque<(Key, Publication)>> {
            self.0.batch(count)
        }

        fn remove(&mut self, key: Key) -> StorageResult<()> {
            self.0.remove(key.offset as usize)
        }

        fn set_waker(&mut self, waker: &Waker) {
            self.0.set_waker(waker)
        }
    }

    #[test]
    fn it_inits_ok_with_no_previous_data() {
        let result = panic::catch_unwind(|| TestRingBuffer::default());
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
            let file_name = FILE_NAME.to_owned() + &create_rand_str();
            cleanup_test_file(file_name.clone());
            let mut keys = vec![];
            // Create ring buffer and perform some operations and then destruct.
            {
                let mut rb = RingBuffer::new(file_name.clone(), MAX_FILE_SIZE, FLUSH_OPTIONS);
                for _ in 0..10 {
                    let result = rb.insert(publication.clone());
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
                assert!(rb.remove(keys[0].offset as usize).is_ok());
                assert!(rb.remove(keys[1].offset as usize).is_ok());
                assert!(rb.remove(keys[2].offset as usize).is_ok());
                read = rb.pointers.read.load(Ordering::SeqCst);
                write = rb.pointers.write.load(Ordering::SeqCst);
            }
            // Create ring buffer again and validate pointers match where they left off.
            {
                let mut rb = TestRingBuffer::new(file_name);
                let loaded_read = rb.0.pointers.read.load(Ordering::SeqCst);
                let loaded_write = rb.0.pointers.write.load(Ordering::SeqCst);
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
    fn it_batches_correctly_after_insert() {
        let mut rb = TestRingBuffer::default();
        let publication = Publication {
            topic_name: "test".to_owned(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // first with 1
        let result = rb.0.insert(publication.clone());
        assert!(result.is_ok());
        let key = result.unwrap();
        let result = rb.0.batch(1);
        assert!(result.is_ok());
        let batch = result.unwrap();
        assert_eq!(key, batch[0].0);

        // now with 9 more
        let mut keys = vec![];
        keys.push(key);
        for _ in 0..9 {
            let result = rb.0.insert(publication.clone());
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
        let rb = TestRingBuffer::default();
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
        for _ in 0..10usize {
            let result = rb.0.insert(publication.clone());
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
            let result = rb.0.remove(key.offset as usize);
            assert!(result.is_ok());
        }
    }
}
