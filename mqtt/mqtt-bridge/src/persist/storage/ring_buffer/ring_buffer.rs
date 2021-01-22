use std::{
    collections::VecDeque,
    fs::{File, OpenOptions},
    path::PathBuf,
    sync::{
        atomic::{AtomicUsize, Ordering},
        Arc,
    },
    task::Waker,
    time::Instant,
};

use crate::persist::storage::{
    serialize::{binary_deserialize, binary_serialize, binary_serialize_size},
    FlushOptions, FlushState, IOStatus, Storage, StorageResult,
};
use memmap::MmapMut;
use parking_lot::Mutex;

use std::io::Result as IOResult;

use super::{
    block::{calculate_hash, validate, BlockHeaderWithHash, Data},
    error::RingBufferError,
    mmap::{mmap_read, mmap_write},
};

#[derive(Default)]
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
pub struct RingBuffer {
    inner: Arc<Mutex<MmapMut>>,
    max_file_size: usize,
    flush_options: FlushOptions,
    pointers: FilePointers,
    waker: Option<Waker>,
    flush_state: FlushState,
}

fn create_file(file_name: String) -> IOResult<File> {
    OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(&PathBuf::from(file_name))
}

impl RingBuffer {
    pub fn new(file_name: String, max_file_size: usize, flush_options: FlushOptions) -> Self {
        let file = create_file(file_name).expect("Failed to create file");
        file.set_len(max_file_size as u64)
            .expect("Failed to set file size");
        let mmap = unsafe { MmapMut::map_mut(&file).expect("Failed to open mmap") };
        Self {
            inner: Arc::new(Mutex::new(mmap)),
            max_file_size,
            flush_options,
            pointers: FilePointers::default(),
            waker: None,
            flush_state: FlushState::new(),
        }
    }

    fn should_flush(&self) -> bool {
        match self.flush_options {
            FlushOptions::AfterEachWrite => true,
            FlushOptions::AfterXWrites(xwrites) => {
                self.flush_state.writes.load(Ordering::SeqCst) >= xwrites
            }
            FlushOptions::AfterXBytes(xbytes) => {
                self.flush_state.bytes_written.load(Ordering::SeqCst) >= xbytes
            }
            FlushOptions::AfterXMilliseconds(xmillis_elapsed) => {
                self.flush_state.millis_elapsed.load(Ordering::SeqCst) >= xmillis_elapsed
            }
            FlushOptions::Off => false,
        }
    }
}

impl Storage<usize, Vec<u8>> for RingBuffer {
    fn insert(&self, value: &Vec<u8>) -> StorageResult<IOStatus<()>> {
        let timer = Instant::now();
        let data = binary_serialize(value)?;

        let data_size = data.len();
        let data_hash = calculate_hash(&data);

        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let read_index = self.pointers.read.load(Ordering::SeqCst);

        let block_header = BlockHeaderWithHash::new(data_size, write_index, data_hash);
        let serialized_block_header = binary_serialize(&block_header)?;
        let block_size = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0))?;
        let data = Data::new(data);
        let serialized_data = binary_serialize(&data)?;

        let total_size = block_size + data_size;
        // Check to see if we might corrupt data if we write, if so, return pending.
        // As trying later might be successful.
        if write_index < read_index {
            if write_index + total_size > read_index {
                return Ok(IOStatus::Pending);
            }
        }

        let should_flush = self.should_flush();
        let start = write_index;
        let end = start + block_size;

        // Check if an existing block header is present. If the block is there
        // and if the overwrite flag is not set then we shouldn't write.
        let maybe_block_bytes = mmap_read(self.inner.clone(), start, end);
        let maybe_deserialized_block =
            binary_deserialize::<BlockHeaderWithHash>(&maybe_block_bytes);
        if maybe_deserialized_block.is_ok() {
            let deserialized_block = maybe_deserialized_block.unwrap();
            let overwrite = *deserialized_block.inner().overwrite();
            if !overwrite {
                return Ok(IOStatus::Pending);
            }
        }

        if write_index + total_size < self.max_file_size {
            mmap_write(
                self.inner.clone(),
                start,
                end,
                &serialized_block_header,
                should_flush,
            )?;
            let start = end;
            let end = start + data_size;
            mmap_write(
                self.inner.clone(),
                start,
                end,
                &serialized_data,
                should_flush,
            )?;
        } else {
            let wrap_around = total_size % (self.max_file_size - write_index);
            if end + wrap_around > self.max_file_size {
                return Err(RingBufferError::WrapAround.into());
            }
            mmap_write(
                self.inner.clone(),
                start,
                end,
                &serialized_block_header,
                should_flush,
            )?;
            let start = end;
            let end = total_size;
            let split = end - start;
            mmap_write(
                self.inner.clone(),
                start,
                self.max_file_size,
                &serialized_data[..split],
                should_flush,
            )?;
            let start = 0;
            let end = wrap_around;
            mmap_write(
                self.inner.clone(),
                start,
                end,
                &serialized_data[split..],
                should_flush,
            )?;
        }

        self.pointers.write.store(
            (write_index + total_size) % self.max_file_size,
            Ordering::SeqCst,
        );

        self.flush_state
            .update(1, total_size, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(IOStatus::Ready(()))
    }

    fn batch(&self, amount: usize) -> StorageResult<IOStatus<VecDeque<(usize, Vec<u8>)>>> {
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let mut read_index = self.pointers.read.load(Ordering::SeqCst);
        let block_size = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0))?;

        let mut vdata = VecDeque::new();
        let mut total_size = 0;
        for _ in 0..amount {
            // If read would go into where writes are happening then we must wait.
            if read_index >= write_index {
                return Ok(IOStatus::Pending);
            }
            let block;
            {
                let mmap = self.inner.lock();
                let start = read_index;
                let end = start + block_size;
                if end > self.max_file_size {
                    return Err(RingBufferError::WrapAround.into());
                }
                let bytes = &mmap[start..end];
                block = binary_deserialize::<BlockHeaderWithHash>(bytes)?;
            }
            let data_size = *block.inner().data_size();
            let index = *block.inner().index();
            let data;
            total_size += data_size + block_size;
            if read_index + data_size < self.max_file_size {
                let mmap = self.inner.lock();
                let start = read_index + block_size;
                let end = start + data_size;
                let bytes = &mmap[start..end];
                data = binary_deserialize::<Data>(bytes)?;
            } else {
                let start = read_index + block_size;
                let end = start + data_size;
                let split = end - self.max_file_size;
                let mmap = self.inner.lock();
                let left = &mmap[start..self.max_file_size];
                let right = &mmap[0..split];
                let mut bytes = Vec::from(left);
                bytes.append(&mut Vec::from(right));
                data = binary_deserialize::<Data>(&bytes)?;
            }
            validate(&block, &data)?;
            let something = binary_deserialize::<Vec<u8>>(data.inner())?;
            vdata.push_back((index, something));
            read_index += block_size + data_size;
        }

        self.pointers.read.store(
            (read_index + total_size) % self.max_file_size,
            Ordering::SeqCst,
        );

        Ok(IOStatus::Ready(vdata))
    }

    fn remove(&self, key: &usize) -> StorageResult<IOStatus<()>> {
        let timer = Instant::now();
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let block_size = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0))?;

        if *key >= write_index {
            return Ok(IOStatus::Pending);
        }

        let start = *key;
        let end = start + block_size;
        if end > self.max_file_size {
            return Err(RingBufferError::WrapAround.into());
        }
        let bytes = mmap_read(self.inner.clone(), start, end);

        let mut block = binary_deserialize::<BlockHeaderWithHash>(&bytes)?;

        let inner_block = block.inner_mut();
        inner_block.set_overwrite(true);

        let bytes = binary_serialize(&block)?;

        let should_flush = self.should_flush();
        mmap_write(self.inner.clone(), start, end, &bytes, should_flush)?;

        self.flush_state
            .update(0, 0, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(IOStatus::Ready(()))
    }

    fn set_waker(&mut self, waker: Waker) {
        self.waker = Some(waker);
    }
}
