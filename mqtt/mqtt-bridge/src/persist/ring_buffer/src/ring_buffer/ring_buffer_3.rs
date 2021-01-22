use std::{collections::VecDeque, io::{Read, SeekFrom, Write}};
use std::{
    fs::File,
    io::Seek,
    mem::size_of,
    sync::{atomic::AtomicUsize, atomic::Ordering, Arc, Mutex},
};

use crate::ring_buffer::{
    block::{
        binary_deserialize, binary_serialize, calculate_hash, serialized_block_size_minus_data,
        FixedBlock, HashedBlock,
    },
    error::RingBufferError,
    RingBufferResult,
};

use super::RingBuffer;

fn file_read(
    file: Arc<Mutex<File>>,
    block_size: usize,
    file_size: usize,
    read_index: usize,
) -> RingBufferResult<Option<HashedBlock>> {
    let offset_begin = (read_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    let mut lock = file.lock().unwrap();
    lock.seek(SeekFrom::Start(offset_begin as u64))
        .map_err(|err| {
            RingBufferError::from_err(format!("Failed to seek to {}", offset_begin), Box::new(err))
        })?;
    let mut bytes= vec![0u8; block_size];
    lock.read(&mut bytes).map_err(|err| {
        RingBufferError::from_err(
            format!("Failed to read at {} for {}", offset_begin, block_size),
            Box::new(err),
        )
    })?;
    let result = if bytes.iter().map(|x| *x as usize).sum::<usize>() == 0 {
        None
    } else {
        Some(binary_deserialize(&bytes)?)
    };
    Ok(result)
}

fn file_write(
    mmap: Arc<Mutex<File>>,
    block_size: usize,
    file_size: usize,
    write_index: usize,
    hashed_block: &HashedBlock,
) -> RingBufferResult<()> {
    let bytes = binary_serialize(hashed_block)?;
    let offset_begin = (write_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    let mut lock = mmap.lock().unwrap();
    lock.seek(SeekFrom::Start(offset_begin as u64))
        .map_err(|err| {
            RingBufferError::from_err(format!("Failed to seek to {}", offset_begin), Box::new(err))
        })?;
    lock.write(&bytes).map_err(|err| {
        RingBufferError::from_err(
            format!("Failed to write at {}", offset_begin),
            Box::new(err),
        )
    }).map(|_| ())
    // lock.sync_data()
    //     .map_err(|err| {
    //         RingBufferError::from_err("Failed to flush on mmap".to_owned(), Box::new(err))
    //     })
}

fn file_delete(
    mmap: Arc<Mutex<File>>,
    block_size: usize,
    file_size: usize,
    delete_index: usize,
) -> RingBufferResult<()> {
    let offset_begin = (delete_index * block_size) % file_size;
    let offset_end = offset_begin + block_size;
    let zeroes = vec![0; block_size];
    let mut lock = mmap.lock().unwrap();
      lock.seek(SeekFrom::Start(offset_begin as u64))
        .map_err(|err| {
            RingBufferError::from_err(format!("Failed to seek to {}", offset_begin), Box::new(err))
        })?;
    lock.write(&zeroes).map_err(|err| {
        RingBufferError::from_err(
            format!("Failed to delete at {}", offset_begin),
            Box::new(err),
        )
    }).map(|_| ())
    // lock.sync_data()
    //     .map_err(|err| {
    //         RingBufferError::from_err("Failed to flush on mmap".to_owned(), Box::new(err))
    //     })
}

#[derive(Debug)]
pub struct RingBuffer3 {
    write_index: AtomicUsize,
    read_index: AtomicUsize,
    block_size: usize,
    file_size: usize,
    file: Arc<Mutex<File>>,
}

impl RingBuffer3 {
    pub fn new(file_size: usize, block_size: usize, file: File) -> Self {
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
        Self {
            file_size,
            block_size,
            write_index: AtomicUsize::new(0),
            read_index: AtomicUsize::new(0),
            file: Arc::from(Mutex::from(file)),
        }
    }

    fn optimal_data_size_for_block(&self) -> usize {
        self.block_size - serialized_block_size_minus_data()
    }

    fn load_block(&self) -> RingBufferResult<Option<(usize, HashedBlock)>> {
        let read_index = self.read_index.fetch_add(1, Ordering::Acquire);
        let result = if let Some(hashed_block) = file_read(
            self.file.clone(),
            self.block_size,
            self.file_size,
            read_index,
        )? {
            self.validate(&hashed_block)?;
            Some((read_index, hashed_block))
        } else {
            None
        };
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

impl RingBuffer for RingBuffer3 {
    fn init(&mut self) -> RingBufferResult<()> {
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
        let write_index = self.write_index.fetch_add(1, Ordering::Acquire);
        let fixed_block = FixedBlock::new(self.block_size, value.clone(), write_index);
        let hash = calculate_hash(value);
        let hashed_block = HashedBlock::new(fixed_block, hash);
        let _ = file_write(
            self.file.clone(),
            self.block_size,
            self.file_size,
            write_index,
            &hashed_block,
        )?;
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
        file_delete(self.file.clone(), self.block_size, self.file_size, *key)
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

    fn create_file(file_name: &'static str) -> RingBufferResult<File> {
        let file = create_test_file(file_name)?;
        file.set_len(MAX_FILE_SIZE as u64).map_err(|err| {
            RingBufferError::from_err("Failed to set file size".to_owned(), Box::new(err))
        })?;
        Ok(file)
    }

    fn create_ring_buffer(file_name: &'static str) -> RingBufferResult<RingBuffer3> {
        let file = create_file(file_name)?;
        Ok(RingBuffer3::new(MAX_FILE_SIZE, BLOCK_SIZE, file))
    }

    #[test]
    fn test_new() {
        let file_name = "ring_buffer_1_new.txt";
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
