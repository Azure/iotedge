use std::{collections::VecDeque, sync::{Mutex, RwLock}};
use std::{
    cell::UnsafeCell,
    mem::size_of,
    sync::{
        atomic::AtomicUsize,
        atomic::{AtomicBool, Ordering},
    },
    thread::sleep,
    time::Duration,
};

use memmap::MmapMut;

use crate::ring_buffer::{
    block::{
        binary_deserialize, binary_serialize, calculate_hash, serialized_block_size_minus_data,
        FixedBlock, HashedBlock,
    },
    error::RingBufferError,
    RingBufferResult,
};

use super::RingBuffer;

const LOCK_DELAY: Duration = Duration::from_nanos(500);

fn mmap_read(
    mmap_cell: &UnsafeCell<MmapMut>,
    block_size: usize,
    file_size: usize,
    read_index: usize,
) -> RingBufferResult<Option<HashedBlock>> {
    let offset_begin = (read_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    let mmap = unsafe { &*mmap_cell.get() };
    let bytes = &mmap[offset_begin..offset_end];
    let result = if bytes.into_iter().map(|x| *x as usize).sum::<usize>() == 0 {
        None
    } else {
        Some(binary_deserialize(bytes)?)
    };
    Ok(result)
}

fn mmap_write(
    mmap_cell: &UnsafeCell<MmapMut>,
    block_size: usize,
    file_size: usize,
    write_index: usize,
    hashed_block: &HashedBlock,
) -> RingBufferResult<()> {
    let bytes = binary_serialize(hashed_block)?;
    let offset_begin = (write_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    // let zeroes = vec![0; block_size];
    let mmap = unsafe { &mut *mmap_cell.get() };
    // mmap[offset_begin..offset_end].clone_from_slice(&zeroes);
    mmap[offset_begin..offset_end].clone_from_slice(&bytes);
    mmap.flush_async_range((write_index * block_size) % file_size, block_size)
        .map_err(|err| {
            RingBufferError::from_err("Failed to flush on mmap".to_owned(), Box::new(err))
        })
}

fn mmap_delete(
    mmap_cell: &UnsafeCell<MmapMut>,
    block_size: usize,
    file_size: usize,
    delete_index: usize,
) -> RingBufferResult<()> {
    let offset_begin = (delete_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    let zeroes = vec![0; block_size];
    let mmap = unsafe { &mut *mmap_cell.get() };
    mmap[offset_begin..offset_end].clone_from_slice(&zeroes);
    mmap.flush_async_range((delete_index * block_size) % file_size, block_size)
        .map_err(|err| {
            RingBufferError::from_err("Failed to flush on mmap".to_owned(), Box::new(err))
        })
}

type SafeVec = Vec<RwLock<()>>;

#[derive(Debug)]
pub struct RingBuffer2 {
    write_index: AtomicUsize,
    read_index: AtomicUsize,
    has_init: AtomicBool,
    block_size: usize,
    file_size: usize,
    safe_vec: SafeVec,
    mmap: UnsafeCell<MmapMut>,
}

unsafe impl Send for RingBuffer2 {}
unsafe impl Sync for RingBuffer2 {}

impl RingBuffer2 {
    pub fn new(file_size: usize, block_size: usize, mmap: MmapMut) -> Self {
        if block_size == 0 || file_size == 0 {
            panic!("block_size and file_size must be greater than 0");
        }
        if block_size > file_size {
            panic!("block_size must be less than file_size");
        }
        if file_size % block_size != 0 {
            panic!("file_size must be divisible by block_size");
        }
        let min_block_size = 2 * serialized_block_size_minus_data();
        if block_size < min_block_size {
            panic!(format!(
                "block_size needs to be at least {} big",
                min_block_size
            ))
        }

        let mut safe_vec = SafeVec::new();
        for _ in 0..(file_size / block_size) {
            safe_vec.push(RwLock::new(()));
        }

        Self {
            file_size,
            block_size,
            write_index: AtomicUsize::new(0),
            read_index: AtomicUsize::new(0),
            has_init: AtomicBool::new(false),
            safe_vec,
            mmap: UnsafeCell::from(mmap),
        }
    }

    fn optimal_data_size_for_block(&self) -> usize {
        self.block_size - serialized_block_size_minus_data()
    }

    fn save_block(&self, write_index: usize, buf: &[u8]) -> RingBufferResult<()> {
        let block_size = self.block_size;
        let file_size = self.file_size;
        let fixed_block = FixedBlock::new(block_size, Vec::from(buf), write_index);
        let hash = calculate_hash(buf);
        let hashed_block = HashedBlock::new(fixed_block, hash);
        mmap_write(
            &self.mmap,
            block_size,
            file_size,
            write_index,
            &hashed_block,
        )
    }

    fn load_block(&self) -> RingBufferResult<Option<(usize, HashedBlock)>> {
        let read_index = self.read_index.fetch_add(1, Ordering::SeqCst);

        let index = read_index % (self.file_size / self.block_size);
        let in_use = &self.safe_vec[index];
        let lock = in_use.read().unwrap();
        // loop {
        //     // sleep(LOCK_DELAY);
        //     if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
        //         break;
        //     }
        // }
        let result = if let Some(hashed_block) =
            mmap_read(&self.mmap, self.block_size, self.file_size, read_index)?
        {
            self.validate(&hashed_block)?;
            Some((read_index, hashed_block))
        } else {
            None
        };
        drop(lock);
        // let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);
        Ok(result)
    }

    fn validate(&self, hashed_block: &HashedBlock) -> RingBufferResult<()> {
        let fixed_block = &hashed_block.fixed_block();
        let attributes_hash = fixed_block.attributes_hash();
        let attributes = &fixed_block.attributes();
        let calculated_attributes_hash = calculate_hash(attributes);
        if *attributes_hash != calculated_attributes_hash {
            return Err(RingBufferError::new(
                format!(
                    "Unexpected attributes hash value from {:?} expected {} but got {}",
                    hashed_block, calculated_attributes_hash, attributes_hash
                ),
                None,
            ));
        }
        if *attributes.block_size() != self.block_size {
            return Err(RingBufferError::new(
                format!(
                    "Unexpected block size from {:?} expected {} but got {}",
                    hashed_block,
                    self.block_size,
                    attributes.block_size()
                ),
                None,
            ));
        }
        let data = &fixed_block.data();
        if data.len() != *attributes.data_size() {
            return Err(RingBufferError::new(
                format!(
                    "Unexpected data length from {:?} expected {} but got {}",
                    hashed_block,
                    attributes.data_size(),
                    data.len()
                ),
                None,
            ));
        }
        let hash = calculate_hash(data);
        if hash != *hashed_block.hash() {
            return Err(RingBufferError::new(
                format!(
                    "Unexpected hash value from {:?} expected {:?} but got {:?}",
                    hashed_block,
                    hashed_block.hash(),
                    hash
                ),
                None,
            ));
        }
        Ok(())
    }
}

impl RingBuffer for RingBuffer2 {
    fn init(&mut self) -> RingBufferResult<()> {
        let has_init = self
            .has_init
            .compare_and_swap(false, true, Ordering::SeqCst);
        if has_init {
            return Err(RingBufferError::new(
                "Cannot initialize ring buffer more than once".to_owned(),
                None,
            ));
        }
        let maybe_curr_block = self.load_block()?;
        if maybe_curr_block.is_none() {
            return Ok(());
        }
        let (_, curr_block) = maybe_curr_block.unwrap();
        let mut curr: usize = *curr_block.fixed_block().attributes().index();

        loop {
            let maybe_next_block = self.load_block()?;
            if maybe_next_block.is_none() {
                return Ok(());
            }
            let (_, next_block) = maybe_next_block.unwrap();
            let next = *next_block.fixed_block().attributes().index();
            curr = next;
            if next <= curr {
                break;
            }
        }
        Ok(())
    }

    fn save(&self, value: &Vec<u8>) -> RingBufferResult<usize> {
        let write_index = self.write_index.fetch_add(1, Ordering::SeqCst);
        let index = write_index % (self.file_size / self.block_size);
        let in_use = &self.safe_vec[index];
        let lock = in_use.write().unwrap();
        // loop {
        //     // sleep(LOCK_DELAY);
        //     if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
        //         break;
        //     }
        // }
        let _ = self.save_block(write_index, value)?;
        drop(lock);
        // let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);
        Ok(write_index)
    }

    fn load(&self) -> RingBufferResult<Option<(usize, Vec<u8>)>> {
        let result = if let Some((index, hashed_block)) = self.load_block()? {
            Some((index, hashed_block.fixed_block().data().clone()))
        } else {
            None
        };
        Ok(result)
    }

    fn batch_load(&self, batch_size: usize) -> RingBufferResult<VecDeque<(usize, Vec<u8>)>> {
        let mut results = VecDeque::default();
        for _ in 0..batch_size {
            if let Some((index, block)) = self.load_block()? {
                results.push_back((index, block.fixed_block().data().clone()))
            }
        }
        Ok(results)
    }

    fn remove(&self, key: &usize) -> RingBufferResult<()> {
        let index = key % (self.file_size / self.block_size);
        let in_use = &self.safe_vec[index];
        let lock = in_use.write().unwrap();
        // loop {
        //     if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
        //         break;
        //     }
        // }
        mmap_delete(&self.mmap, self.block_size, self.file_size, index)?;
        drop(lock);
        // let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use std::{
        fs::{remove_file, File, OpenOptions},
        path::PathBuf,
    };

    use super::*;

    const MAX_FILE_SIZE: usize = 1024 * 1024 /*orig: 1024*/;
    const BLOCK_SIZE: usize = 4 * 1024/*orig: 256*/;

    fn random_data(data_length: usize) -> Vec<u8> {
        (0..data_length).map(|_| rand::random::<u8>()).collect()
    }

    fn create_test_file(file_name: &'static str) -> RingBufferResult<File> {
        OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .open(&PathBuf::from(file_name))
            .map_err(|err| {
                RingBufferError::from_err("Failed to open test file".to_owned(), Box::new(err))
            })
    }

    fn cleanup_test_file(file_name: &'static str) {
        let path = PathBuf::from(file_name);
        if path.exists() {
            let result = remove_file(path);
            assert!(result.is_ok());
        }
    }

    fn create_mmap(file_name: &'static str) -> RingBufferResult<MmapMut> {
        let file = create_test_file(file_name)?;
        file.set_len(MAX_FILE_SIZE as u64).map_err(|err| {
            RingBufferError::from_err("Failed to set file size".to_owned(), Box::new(err))
        })?;
        Ok(unsafe {
            MmapMut::map_mut(&file).map_err(|err| {
                RingBufferError::from_err("Failed to create mmap".to_owned(), Box::new(err))
            })?
        })
    }

    fn create_ring_buffer(file_name: &'static str) -> RingBufferResult<RingBuffer2> {
        let mmap = create_mmap(file_name)?;
        Ok(RingBuffer2::new(MAX_FILE_SIZE, BLOCK_SIZE, mmap))
    }

    #[test]
    fn test_new() {
        let file_name = "ring_buffer_2_new.txt";
        cleanup_test_file(file_name);
        let result = create_ring_buffer(file_name);
        assert!(result.is_ok());
        let rb = result.unwrap();
        assert_eq!(rb.file_size, MAX_FILE_SIZE);
        assert_eq!(rb.read_index.load(Ordering::Relaxed), 0);
        assert_eq!(rb.write_index.load(Ordering::Relaxed), 0);
        assert_eq!(rb.block_size, BLOCK_SIZE);
        cleanup_test_file(file_name);
    }
}
