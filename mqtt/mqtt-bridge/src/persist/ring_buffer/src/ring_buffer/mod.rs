pub mod block;
pub mod error;
pub mod ring_buffer_1;
pub mod ring_buffer_2;
pub mod ring_buffer_3;

use memmap::MmapMut;
use ring_buffer_1::RingBuffer1;
use ring_buffer_2::RingBuffer2;
use ring_buffer_3::RingBuffer3;

use crate::{error::StorageError, Storage, StorageResult};

use self::error::RingBufferError;
use std::{
    collections::VecDeque,
    fs::{File, OpenOptions},
    ops::Deref,
    path::PathBuf,
};

pub type RingBufferResult<T> = Result<T, RingBufferError>;

pub enum RingBufferType {
    WithMutex {
        file_size: usize,
        block_size: usize,
        file_path: PathBuf,
    },
    WithAtomicSpin {
        file_size: usize,
        block_size: usize,
        file_path: PathBuf,
    },
    WithFileIO {
        file_size: usize,
        block_size: usize,
        file_path: PathBuf,
    },
}

fn create_file(file_path: PathBuf) -> RingBufferResult<File> {
    OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(&file_path)
        .map_err(|err| {
            RingBufferError::from_err("Failed to open test file".to_owned(), Box::new(err))
        })
}

fn create_file_with_len(file_path: PathBuf, file_size: usize) -> RingBufferResult<File> {
    let file = create_file(file_path)?;
    file.set_len(file_size as u64).map_err(|err| {
        RingBufferError::from_err("Failed to set file size".to_owned(), Box::new(err))
    })?;
    Ok(file)
}

fn create_mmap(file_path: PathBuf, file_size: usize) -> RingBufferResult<MmapMut> {
    let file = create_file_with_len(file_path, file_size)?;
    Ok(unsafe {
        MmapMut::map_mut(&file).map_err(|err| {
            RingBufferError::from_err("Failed to create mmap".to_owned(), Box::new(err))
        })?
    })
}

pub struct Rb(Box<dyn RingBuffer>);

unsafe impl Send for Rb {}
unsafe impl Sync for Rb {}

impl Deref for Rb {
    type Target = Box<dyn RingBuffer>;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

pub fn create_ring_buffer(rbt: RingBufferType) -> RingBufferResult<Rb> {
    let inner: Box<dyn RingBuffer> = match rbt {
        RingBufferType::WithMutex {
            file_size,
            block_size,
            file_path,
        } => {
            let mmap = create_mmap(file_path, file_size)?;
            Box::new(RingBuffer1::new(file_size, block_size, mmap))
        }
        RingBufferType::WithAtomicSpin {
            file_size,
            block_size,
            file_path,
        } => {
            let mmap = create_mmap(file_path, file_size)?;
            Box::new(RingBuffer2::new(file_size, block_size, mmap))
        }
        RingBufferType::WithFileIO {
            file_size,
            block_size,
            file_path,
        } => {
            let file = create_file_with_len(file_path, file_size)?;
            Box::new(RingBuffer3::new(file_size, block_size, file))
        }
    };
    Ok(Rb(inner))
}

pub trait RingBuffer: Send + Sync {
    fn init(&mut self) -> RingBufferResult<()>;
    fn save(&self, value: &Vec<u8>) -> RingBufferResult<usize>;
    fn load(&self) -> RingBufferResult<Option<(usize, Vec<u8>)>>;
    fn batch_load(&self, batch_size: usize) -> RingBufferResult<VecDeque<(usize, Vec<u8>)>>;
    fn remove(&self, key: &usize) -> RingBufferResult<()>;
}

impl Storage for Rb {
    type Key = usize;
    type Value = Vec<u8>;

    fn init(&mut self, _names: Vec<String>) -> StorageResult<()> {
        self.0.init().map_err(|err| {
            StorageError::from_err("Failed to init storage".to_owned(), Box::new(err))
        })
    }

    fn save(&self, _name: String, value: &Self::Value) -> StorageResult<Self::Key> {
        self.0.save(value).map_err(|err| {
            StorageError::from_err("Failed to save to storage".to_owned(), Box::new(err))
        })
    }

    fn load(&self, _name: String) -> StorageResult<Option<(Self::Key, Self::Value)>> {
        self.0.load().map_err(|err| {
            StorageError::from_err("Failed to load from storage".to_owned(), Box::new(err))
        })
    }

    fn batch_load(
        &self,
        _name: String,
        batch_size: usize,
    ) -> StorageResult<VecDeque<(Self::Key, Self::Value)>> {
        self.0.batch_load(batch_size).map_err(|err| {
            StorageError::from_err(
                "Failed to batch load from storage".to_owned(),
                Box::new(err),
            )
        })
    }

    fn remove(&self, _name: String, key: &Self::Key) -> StorageResult<()> {
        self.0.remove(key).map_err(|err| {
            StorageError::from_err("Failed to remove from storage".to_owned(), Box::new(err))
        })
    }
}

#[cfg(test)]
mod tests {
    use memmap::MmapMut;

    use super::{
        ring_buffer_1::RingBuffer1, ring_buffer_2::RingBuffer2, ring_buffer_3::RingBuffer3,
        RingBuffer, RingBufferError, RingBufferResult,
    };
    use std::{
        fs::{remove_file, File, OpenOptions},
        path::PathBuf,
    };

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

    fn create_mmap(file_name: &'static str) -> RingBufferResult<MmapMut> {
        let file = create_file(file_name)?;
        Ok(unsafe {
            MmapMut::map_mut(&file).map_err(|err| {
                RingBufferError::from_err("Failed to create mmap".to_owned(), Box::new(err))
            })?
        })
    }

    #[derive(Clone, Copy)]
    enum TestRingBufferType {
        WithMutex,
        WithAtomicSpin,
        WithFileIO,
    }

    fn create_ring_buffer(
        file_name: &'static str,
        rbt: TestRingBufferType,
    ) -> RingBufferResult<Box<dyn RingBuffer>> {
        let mmapmut = create_mmap(file_name)?;
        Ok(match rbt {
            TestRingBufferType::WithMutex => {
                Box::new(RingBuffer1::new(MAX_FILE_SIZE, BLOCK_SIZE, mmapmut))
            }
            TestRingBufferType::WithAtomicSpin => {
                Box::new(RingBuffer2::new(MAX_FILE_SIZE, BLOCK_SIZE, mmapmut))
            }
            TestRingBufferType::WithFileIO => Box::new(RingBuffer3::new(
                MAX_FILE_SIZE,
                BLOCK_SIZE,
                create_file(file_name)?,
            )),
        })
    }

    mod init {
        use super::*;
        use test_case::test_case;

        #[test_case("test_init_with_previous_data_mutex.txt", TestRingBufferType::WithMutex ; "Init with mutex")]
        #[test_case("test_init_with_previous_data_atomic_spin.txt", TestRingBufferType::WithAtomicSpin ; "Init with atomic spin")]
        #[test_case("test_init_with_previous_data_file_io.txt", TestRingBufferType::WithFileIO ; "Init with file io")]
        fn test_init_with_previous_data_written(file_name: &'static str, rbt: TestRingBufferType) {
            cleanup_test_file(file_name);
            // Firstly write some garbage data
            {
                let result = create_ring_buffer(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_ok());
                let data = random_data(100);
                let result = rb.save(&data);
                assert!(result.is_ok());
            }
            // Secondly init a second time
            {
                let result = create_ring_buffer(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_ok());
            }
            cleanup_test_file(file_name);
        }

        #[test_case("test_init_without_previous_data_mutex.txt", TestRingBufferType::WithMutex ; "Init with mutex")]
        #[test_case("test_init_without_previous_data_atomic_spin.txt", TestRingBufferType::WithAtomicSpin ; "Init with atomic spin")]
        #[test_case("test_init_without_previous_data_file_io.txt", TestRingBufferType::WithFileIO ; "Init with file io")]
        fn test_init_without_previous_data_written(
            file_name: &'static str,
            rbt: TestRingBufferType,
        ) {
            cleanup_test_file(file_name);

            {
                let result = create_ring_buffer(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_ok());
            }
            {
                let result = create_ring_buffer(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_ok());
            }

            cleanup_test_file(file_name);
        }

        fn create_ring_buffer_with_smaller_block_size(
            file_name: &'static str,
            rbt: TestRingBufferType,
        ) -> RingBufferResult<Box<dyn RingBuffer>> {
            let mmap = create_mmap(file_name)?;
            Ok(match rbt {
                TestRingBufferType::WithMutex => {
                    Box::new(RingBuffer1::new(MAX_FILE_SIZE, BLOCK_SIZE / 2, mmap))
                }
                TestRingBufferType::WithAtomicSpin => {
                    Box::new(RingBuffer2::new(MAX_FILE_SIZE, BLOCK_SIZE / 2, mmap))
                }
                TestRingBufferType::WithFileIO => Box::new(RingBuffer3::new(
                    MAX_FILE_SIZE / 2,
                    BLOCK_SIZE / 2,
                    create_file(file_name)?,
                )),
            })
        }

        #[test_case("test_init_mutex.txt", TestRingBufferType::WithMutex ; "Init with mutex")]
        #[test_case("test_init_atomic_spin.txt", TestRingBufferType::WithAtomicSpin ; "Init with atomic spin")]
        #[test_case("test_init_file_io.txt", TestRingBufferType::WithFileIO ; "Init with file io")]
        fn test_init_with_previous_data_written_and_different_block_sizes(
            file_name: &'static str,
            rbt: TestRingBufferType,
        ) {
            cleanup_test_file(file_name);

            {
                let result = create_ring_buffer_with_smaller_block_size(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_ok());
                let result = rb.save(&Vec::from("Hello World!".as_bytes()));
                assert!(result.is_ok());
            }
            {
                let result = create_ring_buffer(file_name, rbt);
                assert!(result.is_ok());
                let mut rb = result.unwrap();
                let result = rb.init();
                assert!(result.is_err());
            }
            cleanup_test_file(file_name);
        }
    }

    mod load {

        use std::io::Write;

        use crate::ring_buffer::block::{calculate_hash, FixedBlock, HashedBlock};

        use super::*;

        use test_case::test_case;

        #[test_case("test_load_with_large_buffer_mutex.txt", TestRingBufferType::WithMutex ; "Load with mutex")]
        #[test_case("test_load_with_large_buffer_atomic_spin.txt", TestRingBufferType::WithAtomicSpin ; "Load with atomic spin")]
        #[test_case("test_load_with_large_buffer_file_io.txt", TestRingBufferType::WithFileIO ; "Load with file io")]
        fn test_load_with_buffer_size_large_enough_for_block(
            file_name: &'static str,
            rbt: TestRingBufferType,
        ) {
            cleanup_test_file(file_name);
            let result = create_ring_buffer(file_name, rbt);
            assert!(result.is_ok());
            let rb = result.unwrap();
            let data_size = 100;
            let data = random_data(data_size);
            {
                let result = create_test_file(file_name);
                assert!(result.is_ok());
                let mut file = result.unwrap();
                let fixed_block = FixedBlock::new(BLOCK_SIZE, data.clone(), 0);
                let hash = calculate_hash(&data);
                let hashed_block = HashedBlock::new(fixed_block, hash);
                let result = bincode::serialize(&hashed_block);
                assert!(result.is_ok());
                let serialized_block = result.unwrap();
                let result = file.write(&serialized_block);
                assert!(result.is_ok());
                let result = file.sync_all();
                assert!(result.is_ok());
            }
            let result = rb.load();
            assert!(result.is_ok());
            let option = result.unwrap();
            assert!(option.is_some());
            let (_, buf) = option.unwrap();
            assert_eq!(data, buf);
            cleanup_test_file(file_name);
        }

        #[test]
        fn load_with_buffer_size_smaller_than_block() {}
        #[test]
        fn load_where_previous_write_was_different_block_size() {}
        #[test]
        fn load_with_hash_mismatch() {}
        #[test]
        fn load_with_data_size_mismatch() {}
        #[test]
        fn load_works_after_crash_with_same_block_size() {}
    }

    mod save {
        use super::*;
        use test_case::test_case;

        #[test]
        fn save_with_data_too_large_for_block() {}

        #[test_case("test_save_with_small_data_mutex.txt", TestRingBufferType::WithMutex ; "Save with mutex")]
        #[test_case("test_save_with_small_data_atomic_spin.txt", TestRingBufferType::WithAtomicSpin ; "Save with atomic spin")]
        #[test_case("test_save_with_small_data_file_io.txt", TestRingBufferType::WithFileIO ; "Save with file io")]
        fn test_save_with_data_within_limits_for_block(
            file_name: &'static str,
            rbt: TestRingBufferType,
        ) {
            cleanup_test_file(file_name);
            let result = create_ring_buffer(file_name, rbt);
            assert!(result.is_ok());
            let rb = result.unwrap();
            let data_size = 100;
            let data = random_data(data_size);
            let result = rb.save(&data);
            assert!(result.is_ok());
            let result = rb.load();
            assert!(result.is_ok());
            let option = result.unwrap();
            assert!(option.is_some());
            let (_, buf) = option.unwrap();
            assert_eq!(data, buf);
            cleanup_test_file(file_name);
        }
        #[test]
        fn save_where_serialize_fails() {}
        #[test]
        fn save_where_file_does_not_exist() {}
        #[test]
        fn save_fails_after_crash_with_different_block_size() {}
        #[test]
        fn save_works_after_crash_with_same_block_size() {}
    }
}
