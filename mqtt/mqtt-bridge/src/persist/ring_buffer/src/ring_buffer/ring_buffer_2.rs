use std::collections::VecDeque;
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

type SafeVec = Vec<AtomicBool>;

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
            safe_vec.push(AtomicBool::new(false));
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
        loop {
            // sleep(LOCK_DELAY);
            if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
                break;
            }
        }
        let result = if let Some(hashed_block) =
            mmap_read(&self.mmap, self.block_size, self.file_size, read_index)?
        {
            self.validate(&hashed_block)?;
            Some((read_index, hashed_block))
        } else {
            None
        };
        let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);
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
        loop {
            // sleep(LOCK_DELAY);
            if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
                break;
            }
        }
        let _ = self.save_block(write_index, value)?;
        let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);
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
        loop {
            if !in_use.compare_and_swap(false, true, Ordering::SeqCst) {
                break;
            }
        }
        mmap_delete(&self.mmap, self.block_size, self.file_size, index)?;
        let _ = in_use.compare_and_swap(true, false, Ordering::SeqCst);

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

// #[cfg(test)]
// mod tests {
//     use std::{
//         fs::{remove_file, File, OpenOptions},
//         path::PathBuf,
//     };

//     use super::*;

//     const MAX_FILE_SIZE: usize = 1024 * 1024 /*orig: 1024*/;
//     const BLOCK_SIZE: usize = 4 * 1024/*orig: 256*/;

//     fn random_data(data_length: usize) -> Vec<u8> {
//         (0..data_length).map(|_| rand::random::<u8>()).collect()
//     }

//     fn create_test_file(file_name: &'static str) -> RingBufferResult<File> {
//         OpenOptions::new()
//             .read(true)
//             .write(true)
//             .create(true)
//             .open(&PathBuf::from(file_name))
//             .map_err(|err| {
//                 RingBufferError::from_err("Failed to open test file".to_owned(), Box::new(err))
//             })
//     }

//     fn cleanup_test_file(file_name: &'static str) {
//         let path = PathBuf::from(file_name);
//         if path.exists() {
//             let result = remove_file(path);
//             assert!(result.is_ok());
//         }
//     }

//     fn create_mmap(file_name: &'static str) -> RingBufferResult<MmapMut> {
//         let file = create_test_file(file_name)?;
//         file.set_len(MAX_FILE_SIZE as u64).map_err(|err| {
//             RingBufferError::from_err("Failed to set file size".to_owned(), Box::new(err))
//         })?;
//         Ok(unsafe {
//             MmapMut::map_mut(&file).map_err(|err| {
//                 RingBufferError::from_err("Failed to create mmap".to_owned(), Box::new(err))
//             })?
//         })
//     }

//     fn create_ring_buffer(file_name: &'static str) -> RingBufferResult<RingBuffer2> {
//         let mmap = create_mmap(file_name)?;
//         Ok(RingBuffer2::new(MAX_FILE_SIZE, BLOCK_SIZE, mmap))
//     }

//     mod new {
//         use super::*;
//         #[test]
//         fn test_new() {
//             let file_name = "test_new.txt";
//             cleanup_test_file(file_name);
//             let result = create_ring_buffer(file_name);
//             assert!(result.is_ok());
//             let rb = result.unwrap();
//             assert_eq!(rb.file_size, MAX_FILE_SIZE);
//             assert_eq!(rb.read_index.load(Ordering::SeqCst), 0);
//             assert_eq!(rb.write_index.load(Ordering::SeqCst), 0);
//             assert_eq!(rb.block_size, BLOCK_SIZE);
//             cleanup_test_file(file_name);
//         }
//     }

//     mod init {
//         use super::*;

//         #[test]
//         fn test_init_with_previous_data_written() {
//             let file_name = "test_init_with_previous_data_written.txt";
//             cleanup_test_file(file_name);
//             // Firstly write some garbage data
//             {
//                 let result = create_ring_buffer(file_name);
//                 assert!(result.is_ok());
//                 let rb = result.unwrap();
//                 let result = rb.init();
//                 assert!(result.is_ok());
//                 let data = random_data(rb.optimal_data_size_for_block());
//                 let result = rb.save(&0, &data);
//                 assert!(result.is_ok());
//             }
//             // Secondly init a second time
//             {
//                 let result = create_ring_buffer(file_name);
//                 assert!(result.is_ok());
//                 let rb = result.unwrap();
//                 let result = rb.init();
//                 assert!(result.is_ok());
//             }
//             cleanup_test_file(file_name);
//         }

//         #[test]
//         fn test_init_without_previous_data_written() {
//             let file_name = "test_init_without_previous_data_written.txt";
//             cleanup_test_file(file_name);

//             {
//                 let result = create_ring_buffer(file_name);
//                 assert!(result.is_ok());
//                 let rb = result.unwrap();
//                 let result = rb.init();
//                 assert!(result.is_ok());
//             }
//             {
//                 let result = create_ring_buffer(file_name);
//                 assert!(result.is_ok());
//                 let rb = result.unwrap();
//                 let result = rb.init();
//                 assert!(result.is_ok());
//             }

//             cleanup_test_file(file_name);
//         }

//         #[test]
//         fn test_init_with_previous_data_written_and_different_block_sizes() {
//             let file_name = "test_init_with_previous_data_written_and_different_block_sizes.txt";
//             cleanup_test_file(file_name);

//             {
//                 let result = create_mmap(file_name);
//                 assert!(result.is_ok());
//                 let mmap = result.unwrap();
//                 let rb = RingBuffer2::new(MAX_FILE_SIZE, BLOCK_SIZE / 2, mmap);
//                 let result = rb.init();
//                 assert!(result.is_ok());
//                 let result = rb.save(&0, &Vec::from("Hello World!".as_bytes()));
//                 assert!(result.is_ok());
//             }
//             {
//                 let result = create_ring_buffer(file_name);
//                 assert!(result.is_ok());
//                 let rb = result.unwrap();
//                 let result = rb.init();
//                 assert!(result.is_err());
//             }
//             cleanup_test_file(file_name);
//         }
//     }

//     mod load {

//         use std::io::Write;

//         use super::*;

//         #[test]
//         fn test_load_with_buffer_size_large_enough_for_block() {
//             let file_name = "test_load_with_buffer_size_large_enough_for_block.txt";
//             cleanup_test_file(file_name);
//             let result = create_ring_buffer(file_name);
//             assert!(result.is_ok());
//             let rb = result.unwrap();
//             let optimal_data_size = rb.optimal_data_size_for_block();
//             let data = random_data(optimal_data_size);
//             {
//                 let result = create_test_file(file_name);
//                 assert!(result.is_ok());
//                 let mut file = result.unwrap();
//                 let fixed_block = FixedBlock::new(BLOCK_SIZE, data.clone(), 0);
//                 let hash = calculate_hash(&data);
//                 let hashed_block = HashedBlock::new(fixed_block, hash);
//                 let result = bincode::serialize(&hashed_block);
//                 assert!(result.is_ok());
//                 let serialized_block = result.unwrap();
//                 let result = file.write(&serialized_block);
//                 assert!(result.is_ok());
//                 let result = file.sync_all();
//                 assert!(result.is_ok());
//             }
//             let result = rb.load();
//             assert!(result.is_ok());
//             let option = result.unwrap();
//             assert!(option.is_some());
//             let (_, buf) = option.unwrap();
//             assert_eq!(data, buf);
//             cleanup_test_file(file_name);
//         }

//         #[test]
//         fn load_with_buffer_size_smaller_than_block() {}
//         #[test]
//         fn load_where_previous_write_was_different_block_size() {}
//         #[test]
//         fn load_with_hash_mismatch() {}
//         #[test]
//         fn load_with_data_size_mismatch() {}
//         #[test]
//         fn load_works_after_crash_with_same_block_size() {}
//     }

//     mod save {
//         use super::*;

//         #[test]
//         fn save_with_data_too_large_for_block() {}
//         #[test]
//         fn test_save_with_data_within_limits_for_block() {
//             let file_name = "test_save_with_data_within_limits_for_block.txt";
//             cleanup_test_file(file_name);
//             let result = create_ring_buffer(file_name);
//             assert!(result.is_ok());
//             let rb = result.unwrap();
//             let optimal_data_size = rb.optimal_data_size_for_block();
//             let data = random_data(optimal_data_size);
//             let result = rb.save(&0, &data);
//             assert!(result.is_ok());
//             let result = rb.load();
//             assert!(result.is_ok());
//             let option = result.unwrap();
//             assert!(option.is_some());
//             let (_, buf) = option.unwrap();
//             assert_eq!(data, buf);
//             cleanup_test_file(file_name);
//         }
//         #[test]
//         fn save_where_serialize_fails() {}
//         #[test]
//         fn save_where_file_does_not_exist() {}
//         #[test]
//         fn save_fails_after_crash_with_different_block_size() {}
//         #[test]
//         fn save_works_after_crash_with_same_block_size() {}
//     }

//     mod enqueue {}

//     mod dequeue {}

//     mod bench {

//         use std::{sync::Arc, thread, time::Instant};

//         use rand::Rng;
//         use thread::JoinHandle;

//         use super::*;

//         fn random_data(data_length: u16) -> Vec<u8> {
//             (0..data_length).map(|_| rand::random::<u8>()).collect()
//         }

//         fn create_test_data(size: usize, min: usize, max: usize) -> Vec<Vec<u8>> {
//             let mut rng = rand::thread_rng();
//             let mut data_set = vec![];
//             for _ in 0..size {
//                 let data_size = rng.gen_range(min..=max);
//                 let data = random_data(data_size as u16);
//                 data_set.push(data);
//             }
//             data_set
//         }

//         fn create_mmap(file_name: &'static str, file_size: usize) -> RingBufferResult<MmapMut> {
//             let file = create_test_file(file_name)?;
//             file.set_len(file_size as u64).map_err(|err| {
//                 RingBufferError::from_err("Failed to set file size".to_owned(), Box::new(err))
//             })?;
//             Ok(unsafe {
//                 MmapMut::map_mut(&file).map_err(|err| {
//                     RingBufferError::from_err("Failed to create mmap".to_owned(), Box::new(err))
//                 })?
//             })
//         }

//         fn create_ring_buffer(
//             file_name: &'static str,
//             file_size: usize,
//             block_size: usize,
//         ) -> RingBufferResult<RingBuffer2> {
//             let mmap = create_mmap(file_name, file_size)?;
//             Ok(RingBuffer2::new(file_size, block_size, mmap))
//         }

//         fn save_load_in_thread(
//             rb: Arc<RingBuffer2>,
//             vchunk: Vec<Vec<u8>>,
//             atomic_count: Arc<AtomicUsize>,
//             dequeue_start: usize,
//         ) -> JoinHandle<()> {
//             thread::spawn(move || {
//                 for data in vchunk {
//                     let _ = rb.save(&0, &data).expect("Failed to save");
//                     let count = atomic_count.load(Ordering::SeqCst);
//                     if count >= dequeue_start {
//                         let _ = rb.load().expect("Failed to load");
//                         // rb.remove(count - dequeue_start).expect("Failed to remove");
//                     }
//                     atomic_count.store(count + 1, Ordering::SeqCst);
//                 }
//             })
//         }

//         #[derive(Debug)]
//         struct Parameters {
//             file_name: &'static str,
//             file_size: usize,
//             block_size: usize,
//             amount_of_packets: usize,
//             min_packet_size: usize,
//             max_packet_size: usize,
//             dequeue_start: usize,
//             threads: usize,
//         }

//         fn test_helper(parameters: Parameters) {
//             cleanup_test_file(parameters.file_name);
//             let data_set = create_test_data(
//                 parameters.amount_of_packets,
//                 parameters.min_packet_size,
//                 parameters.max_packet_size,
//             );
//             let rb = create_ring_buffer(
//                 parameters.file_name,
//                 parameters.file_size,
//                 parameters.block_size,
//             )
//             .expect("Failed to get ring buffer");
//             let arb = Arc::from(rb);
//             println!("Perf test start");
//             let start = Instant::now();
//             let atomic_count = Arc::from(AtomicUsize::new(0));
//             let mut handles = vec![];
//             let data_set_partition =
//                 data_set.chunks(parameters.amount_of_packets / parameters.threads);
//             for chunk in data_set_partition {
//                 let vchunk = Vec::from(chunk);
//                 let handle = save_load_in_thread(
//                     arb.clone(),
//                     vchunk,
//                     atomic_count.clone(),
//                     parameters.dequeue_start,
//                 );
//                 handles.push(handle);
//             }
//             for handle in handles {
//                 handle.join().expect("Failed to join thread");
//             }
//             let duration = start.elapsed();
//             println!("For {:?} duration is {:?}", parameters, duration);
//             cleanup_test_file(parameters.file_name);
//         }

//         #[test]
//         fn test_emulated_packet_performance_1_1() {
//             test_helper(Parameters {
//                 file_name: "perf_1_1",
//                 file_size: 1024 * 1024,
//                 block_size: 128,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 10,
//                 max_packet_size: 20,
//                 dequeue_start: 10,
//                 threads: 1,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_1_4() {
//             test_helper(Parameters {
//                 file_name: "perf_1_4",
//                 file_size: 1024 * 1024,
//                 block_size: 128,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 10,
//                 max_packet_size: 20,
//                 dequeue_start: 10,
//                 threads: 4,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_1_8() {
//             test_helper(Parameters {
//                 file_name: "perf_1_8",
//                 file_size: 1024 * 1024,
//                 block_size: 128,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 10,
//                 max_packet_size: 20,
//                 dequeue_start: 10,
//                 threads: 8,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_1_16() {
//             test_helper(Parameters {
//                 file_name: "perf_1_16",
//                 file_size: 1024 * 1024,
//                 block_size: 128,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 10,
//                 max_packet_size: 20,
//                 dequeue_start: 10,
//                 threads: 16,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_2_1() {
//             test_helper(Parameters {
//                 file_name: "perf_2_1",
//                 file_size: 1024 * 1024,
//                 block_size: 256,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 100,
//                 max_packet_size: 190,
//                 dequeue_start: 10,
//                 threads: 1,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_2_4() {
//             test_helper(Parameters {
//                 file_name: "perf_2_4",
//                 file_size: 1024 * 1024,
//                 block_size: 256,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 100,
//                 max_packet_size: 190,
//                 dequeue_start: 10,
//                 threads: 4,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_2_8() {
//             test_helper(Parameters {
//                 file_name: "perf_2_8",
//                 file_size: 1024 * 1024,
//                 block_size: 256,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 100,
//                 max_packet_size: 190,
//                 dequeue_start: 10,
//                 threads: 8,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_2_16() {
//             test_helper(Parameters {
//                 file_name: "perf_2_16",
//                 file_size: 1024 * 1024,
//                 block_size: 256,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 100,
//                 max_packet_size: 190,
//                 dequeue_start: 10,
//                 threads: 16,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_3_1() {
//             test_helper(Parameters {
//                 file_name: "perf_3_1",
//                 file_size: 1024 * 1280,
//                 block_size: 1280,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 500,
//                 max_packet_size: 1000,
//                 dequeue_start: 10,
//                 threads: 1,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_3_4() {
//             test_helper(Parameters {
//                 file_name: "perf_3_4",
//                 file_size: 1024 * 1280,
//                 block_size: 1280,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 500,
//                 max_packet_size: 1000,
//                 dequeue_start: 10,
//                 threads: 4,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_3_8() {
//             test_helper(Parameters {
//                 file_name: "perf_3_8",
//                 file_size: 1024 * 1280,
//                 block_size: 1280,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 500,
//                 max_packet_size: 1000,
//                 dequeue_start: 10,
//                 threads: 8,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_3_16() {
//             test_helper(Parameters {
//                 file_name: "perf_3_16",
//                 file_size: 1024 * 1280,
//                 block_size: 1280,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 500,
//                 max_packet_size: 1000,
//                 dequeue_start: 10,
//                 threads: 16,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_4_1() {
//             test_helper(Parameters {
//                 file_name: "perf_4_1",
//                 file_size: 1024 * 1024 * 4,
//                 block_size: 4096,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 2000,
//                 max_packet_size: 4000,
//                 dequeue_start: 10,
//                 threads: 1,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_4_4() {
//             test_helper(Parameters {
//                 file_name: "perf_4_4",
//                 file_size: 1024 * 1024 * 4,
//                 block_size: 4096,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 2000,
//                 max_packet_size: 4000,
//                 dequeue_start: 10,
//                 threads: 4,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_4_8() {
//             test_helper(Parameters {
//                 file_name: "perf_4_8",
//                 file_size: 1024 * 1024 * 4,
//                 block_size: 4096,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 2000,
//                 max_packet_size: 4000,
//                 dequeue_start: 10,
//                 threads: 8,
//             });
//         }

//         #[test]
//         fn test_emulated_packet_performance_4_16() {
//             test_helper(Parameters {
//                 file_name: "perf_4_16",
//                 file_size: 1024 * 1024 * 4,
//                 block_size: 4096,
//                 amount_of_packets: 100_000,
//                 min_packet_size: 2000,
//                 max_packet_size: 4000,
//                 dequeue_start: 10,
//                 threads: 16,
//             });
//         }
//     }
// }
