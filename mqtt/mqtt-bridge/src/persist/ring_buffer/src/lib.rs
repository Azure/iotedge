#[macro_use]
pub mod error_macro;
pub mod db;
pub mod error;
pub mod ring_buffer;

use crate::ring_buffer::{create_ring_buffer, RingBufferType};
use db::{create_embedded_database, DbType};

use crate::error::StorageError;
use std::collections::VecDeque;

pub type StorageResult<T> = Result<T, StorageError>;

pub enum StorageType {
    Db(DbType),
    RingBuffer(RingBufferType),
}

pub fn create_storage(
    st: StorageType,
) -> StorageResult<Box<dyn Storage<Key = usize, Value = Vec<u8>>>> {
    let kind: Box<dyn Storage<Key = usize, Value = Vec<u8>>> = match st {
        StorageType::Db(db_type) => {
            let storage = create_embedded_database(db_type).map_err(|err| {
                StorageError::from_err("Failed to create embedded db".to_owned(), Box::new(err))
            })?;
            Box::new(storage)
        }
        StorageType::RingBuffer(rb_type) => {
            let storage = create_ring_buffer(rb_type).map_err(|err| {
                StorageError::from_err("Failed to create ring buffer".to_owned(), Box::new(err))
            })?;
            Box::new(storage)
        }
    };
    Ok(kind)
}

pub trait Storage: Send + Sync {
    type Key;
    type Value;

    fn init(&mut self, names: Vec<String>) -> StorageResult<()>;
    fn save(&self, name: String, value: &Self::Value) -> StorageResult<Self::Key>;
    fn load(&self, name: String) -> StorageResult<Option<(Self::Key, Self::Value)>>;
    fn batch_load(
        &self,
        name: String,
        batch_size: usize,
    ) -> StorageResult<VecDeque<(Self::Key, Self::Value)>>;
    fn remove(&self, name: String, key: &Self::Key) -> StorageResult<()>;
}

#[cfg(test)]
mod tests {
    use std::{
        fs::{remove_dir_all, remove_file},
        path::PathBuf,
    };

    use super::*;

    const MAX_FILE_SIZE: usize = 1024 * 1024 /*orig: 1024*/;
    const BLOCK_SIZE: usize = 4 * 1024/*orig: 256*/;

    fn random_data(data_length: usize) -> Vec<u8> {
        (0..data_length).map(|_| rand::random::<u8>()).collect()
    }

    fn cleanup_test_file(file_name: &'static str) {
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

    #[derive(Copy, Clone, Debug)]
    enum TestStorageType {
        RocksDb,
        Sled,
        RBWithMutex,
        RBWithAtomicSpin,
    }

    fn create_test_storage(
        file_name: &'static str,
        tst: TestStorageType,
    ) -> StorageResult<Box<dyn Storage<Key = usize, Value = Vec<u8>>>> {
        match tst {
            TestStorageType::RBWithAtomicSpin => {
                let rbt = RingBufferType::WithAtomicSpin {
                    file_size: MAX_FILE_SIZE,
                    block_size: BLOCK_SIZE,
                    file_path: PathBuf::from(file_name),
                };
                let st = StorageType::RingBuffer(rbt);
                create_storage(st)
            }
            TestStorageType::RocksDb => {
                let dbt = DbType::RocksDb(PathBuf::from(file_name));
                let st = StorageType::Db(dbt);
                create_storage(st)
            }
            TestStorageType::Sled => {
                let dbt = DbType::Sled(PathBuf::from(file_name));
                let st = StorageType::Db(dbt);
                create_storage(st)
            }
            TestStorageType::RBWithMutex => {
                let rbt = RingBufferType::WithAtomicSpin {
                    file_size: MAX_FILE_SIZE,
                    block_size: BLOCK_SIZE,
                    file_path: PathBuf::from(file_name),
                };
                let st = StorageType::RingBuffer(rbt);
                create_storage(st)
            }
        }
    }

    mod init {
        use super::*;
    }

    mod load {
        use super::*;
    }

    mod save {
        use super::*;
    }

    mod bench {

        use std::{
            ops::Range,
            sync::{
                atomic::{AtomicUsize, Ordering},
                Arc,
            },
            thread,
            time::Instant,
        };

        use rand::Rng;
        use test_case::test_case;
        use thread::JoinHandle;

        use super::*;

        fn random_data(data_length: u16) -> Vec<u8> {
            (0..data_length).map(|_| rand::random::<u8>()).collect()
        }

        fn create_test_data(packet_range: &Range<usize>) -> Vec<u8> {
            let mut rng = rand::thread_rng();
            let data_size = rng.gen_range(packet_range.clone());
            random_data(data_size as u16)
        }

        fn create_test_storage(
            file_name: &'static str,
            file_size: usize,
            block_size: usize,
            tst: TestStorageType,
        ) -> StorageResult<Box<dyn Storage<Key = usize, Value = Vec<u8>>>> {
            match tst {
                TestStorageType::RBWithAtomicSpin => {
                    let rbt = RingBufferType::WithAtomicSpin {
                        file_size,
                        block_size,
                        file_path: PathBuf::from(file_name),
                    };
                    let st = StorageType::RingBuffer(rbt);
                    create_storage(st)
                }
                TestStorageType::RocksDb => {
                    let dbt = DbType::RocksDb(PathBuf::from(file_name));
                    let st = StorageType::Db(dbt);
                    create_storage(st)
                }
                TestStorageType::Sled => {
                    let dbt = DbType::Sled(PathBuf::from(file_name));
                    let st = StorageType::Db(dbt);
                    create_storage(st)
                }
                TestStorageType::RBWithMutex => {
                    let rbt = RingBufferType::WithAtomicSpin {
                        file_size,
                        block_size,
                        file_path: PathBuf::from(file_name),
                    };
                    let st = StorageType::RingBuffer(rbt);
                    create_storage(st)
                }
            }
        }

        fn save_only_in_thread(
            storage: Arc<Box<dyn Storage<Key = usize, Value = Vec<u8>>>>,
            name: String,
            data_range: Range<usize>,
            packet_range: Range<usize>,
            _atomic_count: Arc<AtomicUsize>,
            _dequeue_interval: usize,
        ) -> JoinHandle<()> {
            thread::spawn(move || {
                for _ in data_range {
                    let data = create_test_data(&packet_range);
                    let _ = storage.save(name.clone(), &data).expect("Failed to save");
                }
            })
        }

        fn save_batch_in_thread(
            storage: Arc<Box<dyn Storage<Key = usize, Value = Vec<u8>>>>,
            name: String,
            data_range: Range<usize>,
            packet_range: Range<usize>,
            atomic_count: Arc<AtomicUsize>,
            dequeue_interval: usize,
        ) -> JoinHandle<()> {
            thread::spawn(move || {
                for _ in data_range {
                    let data = create_test_data(&packet_range);
                    let _ = storage.save(name.clone(), &data).expect("Failed to save");
                    let count = atomic_count.load(Ordering::SeqCst);
                    if count % dequeue_interval == 0 {
                        let results = storage
                            .batch_load(name.clone(), dequeue_interval)
                            .expect("Failed to batch load");
                        for (key, _) in results {
                            storage
                                .remove(name.clone(), &key)
                                .expect("Failed to remove");
                        }
                    }
                    atomic_count.store(count + 1, Ordering::SeqCst);
                }
            })
        }

        fn save_load_in_thread(
            storage: Arc<Box<dyn Storage<Key = usize, Value = Vec<u8>>>>,
            name: String,
            data_range: Range<usize>,
            packet_range: Range<usize>,
            atomic_count: Arc<AtomicUsize>,
            dequeue_start: usize,
        ) -> JoinHandle<()> {
            thread::spawn(move || {
                for _ in data_range {
                    let data = create_test_data(&packet_range);
                    let _ = storage.save(name.clone(), &data).expect("Failed to save");
                    let count = atomic_count.load(Ordering::SeqCst);
                    if count >= dequeue_start {
                        if let Some((key, _value)) =
                            storage.load(name.clone()).expect("Failed to load")
                        {
                            storage
                                .remove(name.clone(), &key)
                                .expect("Failed to remove");
                        }
                    }
                    atomic_count.store(count + 1, Ordering::SeqCst);
                }
            })
        }

        #[derive(Debug)]
        struct Parameters {
            file_size: usize,
            block_size: usize,
            amount_of_packets: usize,
            min_packet_size: usize,
            max_packet_size: usize,
            dequeue_start: usize,
            threads: usize,
        }

        fn make_test(
            test_name: &'static str,
            mut test_fn: impl FnMut(
                Arc<Box<dyn Storage<Key = usize, Value = Vec<u8>>>>,
                String,
                Range<usize>,
                Range<usize>,
                Arc<AtomicUsize>,
                usize,
            ) -> JoinHandle<()>,
            parameters: &Parameters,
            file_name: &'static str,
            tst: TestStorageType,
        ) {
            cleanup_test_file(file_name);
            let mut storage =
                create_test_storage(file_name, parameters.file_size, parameters.block_size, tst)
                    .expect("Failed to create storage");
            let name = "test".to_owned();
            storage.init(vec![name.clone()]).expect("Failed to init");
            let astorage: Arc<Box<dyn Storage<Key = usize, Value = Vec<u8>>>> = Arc::from(storage);
            let start = Instant::now();
            let atomic_count = Arc::from(AtomicUsize::new(0));
            let mut handles = vec![];
            for _ in 0..parameters.threads {
                let handle = test_fn(
                    astorage.clone(),
                    name.clone(),
                    0..(parameters.amount_of_packets / parameters.threads),
                    parameters.min_packet_size..parameters.max_packet_size,
                    atomic_count.clone(),
                    parameters.dequeue_start,
                );
                handles.push(handle);
            }
            for handle in handles {
                handle.join().expect("Failed to join thread");
                if start.elapsed().as_secs() > 30 {
                    cleanup_test_file(file_name);
                    panic!("Took too long to process for {}", file_name);
                }
            }
            let duration = start.elapsed();
            println!(
                "For {}: [{} {:?} {:?}] duration is {:?}",
                test_name, file_name, tst, parameters, duration
            );
            cleanup_test_file(file_name);
        }

        #[test_case("perf_1_1_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_1_1_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_1_1_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_1_1_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_1_1(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 128,
                amount_of_packets: 100_000,
                min_packet_size: 10,
                max_packet_size: 20,
                dequeue_start: 10,
                threads: 1,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_1_4_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_1_4_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_1_4_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_1_4_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_1_4(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 128,
                amount_of_packets: 100_000,
                min_packet_size: 10,
                max_packet_size: 20,
                dequeue_start: 10,
                threads: 4,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_1_8_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_1_8_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_1_8_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_1_8_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_1_8(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 128,
                amount_of_packets: 100_000,
                min_packet_size: 10,
                max_packet_size: 20,
                dequeue_start: 10,
                threads: 8,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_1_16_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_1_16_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_1_16_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_1_16_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_1_16(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 128,
                amount_of_packets: 100_000,
                min_packet_size: 10,
                max_packet_size: 20,
                dequeue_start: 10,
                threads: 16,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_2_1_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_2_1_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_2_1_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_2_1_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_2_1(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 256,
                amount_of_packets: 100_000,
                min_packet_size: 100,
                max_packet_size: 190,
                dequeue_start: 10,
                threads: 1,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_2_8_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_2_8_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_2_8_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_2_8_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_2_4(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 256,
                amount_of_packets: 100_000,
                min_packet_size: 100,
                max_packet_size: 190,
                dequeue_start: 10,
                threads: 4,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_2_8_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_2_8_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_2_8_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_2_8_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_2_8(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 256,
                amount_of_packets: 100_000,
                min_packet_size: 100,
                max_packet_size: 190,
                dequeue_start: 10,
                threads: 8,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_2_16_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_2_16_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_2_16_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_2_16_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_2_16(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024,
                block_size: 256,
                amount_of_packets: 100_000,
                min_packet_size: 100,
                max_packet_size: 190,
                dequeue_start: 10,
                threads: 16,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_3_1_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_3_1_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_3_1_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_3_1_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_3_1(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1280,
                block_size: 1280,
                amount_of_packets: 100_000,
                min_packet_size: 500,
                max_packet_size: 1000,
                dequeue_start: 10,
                threads: 1,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_3_4_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_3_4_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_3_4_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_3_4_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_3_4(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1280,
                block_size: 1280,
                amount_of_packets: 100_000,
                min_packet_size: 500,
                max_packet_size: 1000,
                dequeue_start: 10,
                threads: 4,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_3_8_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_3_8_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_3_8_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_3_8_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_3_8(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1280,
                block_size: 1280,
                amount_of_packets: 100_000,
                min_packet_size: 500,
                max_packet_size: 1000,
                dequeue_start: 10,
                threads: 8,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_3_16_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_3_16_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_3_16_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_3_16_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_3_16(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1280,
                block_size: 1280,
                amount_of_packets: 100_000,
                min_packet_size: 500,
                max_packet_size: 1000,
                dequeue_start: 10,
                threads: 16,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_4_1_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_4_1_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_4_1_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_4_1_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_4_1(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024 * 4,
                block_size: 4096,
                amount_of_packets: 100_000,
                min_packet_size: 2000,
                max_packet_size: 4000,
                dequeue_start: 10,
                threads: 1,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_4_4_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_4_4_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_4_4_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_4_4_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_4_4(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024 * 4,
                block_size: 4096,
                amount_of_packets: 100_000,
                min_packet_size: 2000,
                max_packet_size: 4000,
                dequeue_start: 10,
                threads: 4,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_4_8_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_4_8_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_4_8_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_4_8_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_4_8(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024 * 4,
                block_size: 4096,
                amount_of_packets: 100_000,
                min_packet_size: 2000,
                max_packet_size: 4000,
                dequeue_start: 10,
                threads: 8,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }

        #[test_case("perf_4_16_with_rocksdb.txt", TestStorageType::RocksDb ; "With RocksDb")]
        #[test_case("perf_4_16_with_sled.txt", TestStorageType::Sled ; "With Sled")]
        #[test_case("perf_4_16_with_rbm.txt", TestStorageType::RBWithMutex ; "With RBWithMutex")]
        #[test_case("perf_4_16_with_rbas.txt", TestStorageType::RBWithAtomicSpin ; "With RBWithAtomicSpin")]
        fn test_emulated_packet_performance_4_16(file_name: &'static str, tst: TestStorageType) {
            let parameters = Parameters {
                file_size: 1024 * 1024 * 4,
                block_size: 4096,
                amount_of_packets: 100_000,
                min_packet_size: 2000,
                max_packet_size: 4000,
                dequeue_start: 10,
                threads: 16,
            };
            make_test(
                "Save only",
                save_only_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save batch",
                save_batch_in_thread,
                &parameters,
                file_name,
                tst,
            );
            make_test(
                "Save load",
                save_load_in_thread,
                &parameters,
                file_name,
                tst,
            );
        }
    }
}
