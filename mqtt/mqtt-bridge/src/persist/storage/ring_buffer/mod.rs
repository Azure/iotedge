mod block;
pub mod error;
mod mmap;

use std::{
    collections::VecDeque,
    fs::{File, OpenOptions},
    future::Future,
    path::PathBuf,
    pin::Pin,
    sync::{
        atomic::{AtomicUsize, Ordering},
        Arc,
    },
    task::{Context, Poll, Waker},
    time::Instant,
};

use crate::persist::storage::{
    serialize::{binary_deserialize, binary_serialize, binary_serialize_size},
    FlushOptions, FlushState, Storage, StorageResult,
};
use memmap::MmapMut;
use parking_lot::Mutex;

use std::io::Result as IOResult;

use block::{calculate_hash, validate, BlockHeaderWithHash, Data};
use error::RingBufferError;
use mmap::{mmap_read, mmap_write};

pub type RingBufferResult<T> = Result<T, error::RingBufferError>;

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
    pointers: Arc<FilePointers>,
    waker: Option<Waker>,
    flush_state: Arc<FlushState>,
}

unsafe impl Send for RingBuffer {}
unsafe impl Sync for RingBuffer {}

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
            pointers: Arc::new(FilePointers::default()),
            waker: None,
            flush_state: Arc::new(FlushState::new()),
        }
    }
}

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

struct RingBufferWriteFuture {
    pointers: Arc<FilePointers>,
    flush_options: FlushOptions,
    mmap: Arc<Mutex<MmapMut>>,
    flush_state: Arc<FlushState>,
    value: Vec<u8>,
    max_file_size: usize,
}

impl Future for RingBufferWriteFuture {
    type Output = StorageResult<()>;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        let timer = Instant::now();
        let data = binary_serialize(&self.value)?;
        let data = Data::new(data);
        let serialized_data = binary_serialize(&data)?;

        let data_size = serialized_data.len();
        let data_hash = calculate_hash(&data);

        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let read_index = self.pointers.read.load(Ordering::SeqCst);

        let block_header = BlockHeaderWithHash::new(data_size, write_index, data_hash);
        let serialized_block_header = binary_serialize(&block_header)?;
        let block_size = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0))?;

        let total_size = block_size + data_size;
        // Check to see if we might corrupt data if we write, if so, return pending.
        // As trying later might be successful.
        if write_index < read_index {
            if write_index + total_size > read_index {
                println!("write pending");
                return Poll::Pending;
            }
        }
        cx.waker().wake_by_ref();

        let should_flush = should_flush(self.flush_options, &self.flush_state);
        let start = write_index;
        let end = start + block_size;

        // Check if an existing block header is present. If the block is there
        // and if the overwrite flag is not set then we shouldn't write.
        let maybe_block_bytes = mmap_read(self.mmap.clone(), start, end);
        let maybe_deserialized_block =
            binary_deserialize::<BlockHeaderWithHash>(&maybe_block_bytes);
        if maybe_deserialized_block.is_ok() {
            let deserialized_block = maybe_deserialized_block.unwrap();
            let should_not_overwrite = *deserialized_block.inner().should_not_overwrite();
            if should_not_overwrite {
                println!("write pending because block {:?}", deserialized_block);
                cx.waker().wake_by_ref();
                return Poll::Pending;
            }
        }

        if write_index + total_size < self.max_file_size {
            mmap_write(
                self.mmap.clone(),
                start,
                end,
                &serialized_block_header,
                should_flush,
            )?;
            let start = end;
            let end = start + data_size;
            mmap_write(
                self.mmap.clone(),
                start,
                end,
                &serialized_data,
                should_flush,
            )?;
        } else {
            let wrap_around = total_size % (self.max_file_size - write_index);
            if end + wrap_around > self.max_file_size {
                return Poll::Ready(Err(RingBufferError::WrapAround.into()));
            }
            mmap_write(
                self.mmap.clone(),
                start,
                end,
                &serialized_block_header,
                should_flush,
            )?;
            let start = end;
            let end = total_size;
            let split = end - start;
            mmap_write(
                self.mmap.clone(),
                start,
                self.max_file_size,
                &serialized_data[..split],
                should_flush,
            )?;
            let start = 0;
            let end = wrap_around;
            mmap_write(
                self.mmap.clone(),
                start,
                end,
                &serialized_data[split..],
                should_flush,
            )?;
        }
        println!("we{}", (write_index + total_size) % self.max_file_size);
        self.pointers.write.store(
            (write_index + total_size) % self.max_file_size,
            Ordering::SeqCst,
        );
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        println!("w{}", write_index);

        self.flush_state
            .update(1, total_size, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Poll::Ready(Ok(()))
    }
}

struct RingBufferBatchFuture {
    pointers: Arc<FilePointers>,
    mmap: Arc<Mutex<MmapMut>>,
    amount: usize,
    max_file_size: usize,
}

impl Future for RingBufferBatchFuture {
    type Output = StorageResult<VecDeque<(usize, Vec<u8>)>>;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let mut read_index = self.pointers.read.load(Ordering::SeqCst);
        let block_size = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0))?;

        let mut vdata = VecDeque::new();
        let mut total_size = 0;
        for _ in 0..self.amount {
            // If read would go into where writes are happening then we must wait.
            if read_index >= write_index {
                println!("pending r{} w{}", read_index, write_index);
                return Poll::Pending;
            }
            cx.waker().wake_by_ref();
            let block;
            {
                let mmap = self.mmap.lock();
                let start = read_index;
                let end = start + block_size;
                if end > self.max_file_size {
                    return Poll::Ready(Err(RingBufferError::WrapAround.into()));
                }
                let bytes = &mmap[start..end];
                block = binary_deserialize::<BlockHeaderWithHash>(bytes)?;
            }
            let data_size = *block.inner().data_size();
            let index = *block.inner().index();
            let data;
            total_size += data_size + block_size;
            if read_index + data_size < self.max_file_size {
                let mmap = self.mmap.lock();
                let start = read_index + block_size;
                let end = start + data_size;
                let bytes = &mmap[start..end];
                data = binary_deserialize::<Data>(bytes)?;
            } else {
                let start = read_index + block_size;
                let end = start + data_size;
                let split = end - self.max_file_size;
                let mmap = self.mmap.lock();
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

        Poll::Ready(Ok(vdata))
    }
}

struct RingBufferRemoveFuture {
    pointers: Arc<FilePointers>,
    flush_options: FlushOptions,
    mmap: Arc<Mutex<MmapMut>>,
    flush_state: Arc<FlushState>,
    key: usize,
    max_file_size: usize,
}

impl Future for RingBufferRemoveFuture {
    type Output = StorageResult<()>;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        let timer = Instant::now();
        let write_index = self.pointers.write.load(Ordering::SeqCst);
        let block_size_result = binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0));

        if block_size_result.is_err() {
            return Poll::Ready(Err(RingBufferError::Serialization(
                block_size_result.unwrap_err(),
            )
            .into()));
        }

        let block_size = block_size_result.unwrap();

        if self.key >= write_index {
            return Poll::Pending;
        }
        cx.waker().wake_by_ref();

        let start = self.key;
        let end = start + block_size;
        if end > self.max_file_size {
            return Poll::Ready(Err(RingBufferError::WrapAround.into()));
        }
        let bytes = mmap_read(self.mmap.clone(), start, end);

        let mut block = binary_deserialize::<BlockHeaderWithHash>(&bytes)?;

        let inner_block = block.inner_mut();
        inner_block.set_should_not_overwrite(false);

        let bytes = binary_serialize(&block)?;

        let should_flush = should_flush(self.flush_options, &self.flush_state);
        mmap_write(self.mmap.clone(), start, end, &bytes, should_flush)?;

        self.flush_state
            .update(0, 0, timer.elapsed().as_millis() as usize);
        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Poll::Ready(Ok(()))
    }
}

impl Storage<usize, Vec<u8>> for RingBuffer {
    fn insert_future<'a>(
        &self,
        value: &Vec<u8>,
    ) -> Pin<Box<dyn Future<Output = StorageResult<()>> + Send + 'a>> {
        let future = RingBufferWriteFuture {
            pointers: self.pointers.clone(),
            flush_options: self.flush_options,
            mmap: self.inner.clone(),
            flush_state: self.flush_state.clone(),
            value: value.clone(),
            max_file_size: self.max_file_size,
        };
        Pin::from(Box::from(future))
    }

    fn batch_future<'a>(
        &self,
        amount: usize,
    ) -> Pin<Box<dyn Future<Output = StorageResult<VecDeque<(usize, Vec<u8>)>>> + Send + 'a>> {
        let future = RingBufferBatchFuture {
            pointers: self.pointers.clone(),
            mmap: self.inner.clone(),
            amount,
            max_file_size: self.max_file_size,
        };
        Pin::from(Box::from(future))
    }

    fn remove_future<'a>(
        &self,
        key: &usize,
    ) -> Pin<Box<dyn Future<Output = StorageResult<()>> + Send + 'a>> {
        let future = RingBufferRemoveFuture {
            pointers: self.pointers.clone(),
            flush_options: self.flush_options,
            mmap: self.inner.clone(),
            flush_state: self.flush_state.clone(),
            key: *key,
            max_file_size: self.max_file_size,
        };
        Pin::from(Box::from(future))
    }

    fn set_waker(&mut self, waker: Waker) {
        self.waker = Some(waker);
    }

    fn waker(&mut self) -> &mut Option<Waker> {
        &mut self.waker
    }
}
