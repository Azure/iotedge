use criterion::{black_box, criterion_group, criterion_main, Criterion};
use rand::Rng;
use ring_buffer::fixed::FixedBlockRingBuffer;
use ring_buffer::{error::RingBufferError, fixed_mmap::MmapRingBuffer};

use futures::executor::block_on;
use ring_buffer::RingBufferResult;
use std::{error::Error as StdError, fs::remove_file};
use std::{path::PathBuf, time::Instant};

#[derive(Clone, Copy, Debug)]
struct DataSizeRange {
    min: u64,
    max: u64,
}

#[derive(Clone, Copy, Debug)]
struct RingBufferConfig {
    block_size: usize,
    file_size: usize,
    file_name: &'static str,
}

#[derive(Clone, Copy, Debug)]
struct Ratio {
    writes: usize,
    reads: usize,
}

fn random_data(data_length: u16) -> Vec<u8> {
    (0..data_length).map(|_| rand::random::<u8>()).collect()
}

fn to_ring_buffer_err(message: &'static str, err: Box<dyn StdError>) -> RingBufferError {
    RingBufferError::new(message.to_owned(), Some(err))
}

fn cleanup_test_file(file_name: &'static str) {
    let path = PathBuf::from(file_name);
    if path.exists() {
        let result = remove_file(path);
        assert!(result.is_ok());
    }
}

fn create_test_data(data_size_range: DataSizeRange) -> Vec<Vec<u8>> {
    let mut rng = rand::thread_rng();
    let mut data_set = vec![];
    for _ in 0..1 {
        let data_size = rng.gen_range(data_size_range.min..=data_size_range.max);
        let data = random_data(data_size as u16);
        data_set.push(data);
    }
    data_set
}

mod fixed_async {
    use tokio::{
        fs::{File, OpenOptions},
        runtime::Builder,
        task::LocalSet,
    };

    use super::*;

    async fn open_rw(filepath: &'_ PathBuf) -> RingBufferResult<File> {
        OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .open(filepath)
            .await
            .map_err(|err| {
                to_ring_buffer_err("Failed to open file for read and write", Box::from(err))
            })
    }

    async fn create_ring_buffer(
        ring_buffer_config: RingBufferConfig,
    ) -> RingBufferResult<FixedBlockRingBuffer> {
        let mut rb = FixedBlockRingBuffer::new(
            ring_buffer_config.file_size,
            ring_buffer_config.block_size,
            open_rw(&PathBuf::from(ring_buffer_config.file_name)).await?,
        );
        rb.init().await?;
        Ok(rb)
    }

    async fn test(
        rb: &mut FixedBlockRingBuffer,
        data_set: &Vec<Vec<u8>>,
        data_size_range: DataSizeRange,
        ratio: Ratio,
    ) -> RingBufferResult<()> {
        for data in data_set {
            for _ in 0..ratio.writes {
                rb.save(data).await?;
            }
            for _ in 0..ratio.reads {
                let mut buf = vec![0; data_size_range.max as usize];
                rb.load(&mut buf).await?;
            }
        }
        Ok(())
    }

    async fn bench_helper(
        c: &mut Criterion,
        ring_buffer_config: RingBufferConfig,
        data_size_range: DataSizeRange,
        ratio: Ratio,
    ) {
        let local = LocalSet::new();
        let bench_name = ring_buffer_config.file_name.clone();
        c.bench_function(bench_name, |b| {
            b.iter_custom(|iters| {
                let data_set = create_test_data(data_size_range);
                let t = local.run_until(async move {
                    let mut rb = create_ring_buffer(ring_buffer_config)
                        .await
                        .expect("Failed to create ring buffer");
                    let start = Instant::now();
                    for _ in 0..iters {
                        test(
                            black_box(&mut rb),
                            black_box(&data_set),
                            black_box(data_size_range),
                            black_box(ratio),
                        )
                        .await
                        .expect("Failed to ops on ring buffer");
                    }
                    start.elapsed()
                });
                block_on(t)
            })
        });
        cleanup_test_file(bench_name);
    }

    fn bench(
        c: &mut Criterion,
        ring_buffer_config: RingBufferConfig,
        data_size_range: DataSizeRange,
        ratio: Ratio,
    ) {
        // cleanup_test_file(bench_name);
        let mut rt = Builder::new()
            .core_threads(4)
            .max_threads(8)
            .enable_all()
            .build()
            .expect("Creating tokio runtime failed.");
        rt.block_on(bench_helper(c, ring_buffer_config, data_size_range, ratio));
        // cleanup_test_file(bench_name);
    }

    pub(crate) fn small_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 128,
                file_size: 1024 * 1024,
                file_name: "small_write_only",
            },
            DataSizeRange { min: 10, max: 20 },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn small_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 128,
                file_size: 1024 * 1024,
                file_name: "small_read_write",
            },
            DataSizeRange { min: 10, max: 20 },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }

    pub(crate) fn medium_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 256,
                file_size: 1024 * 1024 * 10,
                file_name: "medium_write_only",
            },
            DataSizeRange { min: 100, max: 200 },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn medium_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 256,
                file_size: 1024 * 1024 * 10,
                file_name: "medium_read_write",
            },
            DataSizeRange { min: 100, max: 200 },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }

    pub(crate) fn large_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 2048,
                file_size: 1024 * 1024 * 100,
                file_name: "large_write_only",
            },
            DataSizeRange {
                min: 1000,
                max: 2000,
            },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn large_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 2048,
                file_size: 1024 * 1024 * 100,
                file_name: "large_read_write",
            },
            DataSizeRange {
                min: 1000,
                max: 2000,
            },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }
}

mod fixed_mmap {
    use std::fs::OpenOptions;

    use memmap::MmapMut;

    use super::*;

    fn create_ring_buffer(
        ring_buffer_config: RingBufferConfig,
    ) -> RingBufferResult<MmapRingBuffer> {
        let file = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .open(ring_buffer_config.file_name)
            .map_err(|err| to_ring_buffer_err("Failed to open bench file", Box::new(err)))?;
        file.set_len(ring_buffer_config.file_size as u64)
            .map_err(|err| to_ring_buffer_err("Failed to set bench file len", Box::new(err)))?;
        let mmap = unsafe {
            MmapMut::map_mut(&file)
                .map_err(|err| to_ring_buffer_err("Failed to create bench mmap", Box::new(err)))?
        };
        let mut rb = MmapRingBuffer::new(
            ring_buffer_config.file_size,
            ring_buffer_config.block_size,
            mmap,
        );
        rb.init()?;
        Ok(rb)
    }

    fn test(
        rb: &mut MmapRingBuffer,
        data_set: &Vec<Vec<u8>>,
        ratio: Ratio,
    ) -> RingBufferResult<()> {
        for data in data_set {
            for _ in 0..ratio.writes {
                rb.save(data)?;
            }
            for _ in 0..ratio.reads {
                let _ = rb.load()?;
            }
        }
        Ok(())
    }

    fn bench(
        c: &mut Criterion,
        ring_buffer_config: RingBufferConfig,
        data_size_range: DataSizeRange,
        ratio: Ratio,
    ) {
        let bench_name = ring_buffer_config.file_name.clone();
        c.bench_function(bench_name, |b| {
            let mut rb =
                create_ring_buffer(ring_buffer_config).expect("Failed to create ring buffer");
            b.iter(|| {
                let data_set = create_test_data(data_size_range);
                test(black_box(&mut rb), black_box(&data_set), black_box(ratio))
                    .expect("Failed to ops on ring buffer");
            });
        });
        cleanup_test_file(bench_name);
    }

    pub(crate) fn small_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 128,
                file_size: 1024 * 1024,
                file_name: "small_write_only",
            },
            DataSizeRange { min: 10, max: 20 },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn small_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 128,
                file_size: 1024 * 1024,
                file_name: "small_read_write",
            },
            DataSizeRange { min: 10, max: 20 },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }

    pub(crate) fn medium_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 256,
                file_size: 1024 * 1024 * 10,
                file_name: "medium_write_only",
            },
            DataSizeRange { min: 100, max: 200 },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn medium_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 256,
                file_size: 1024 * 1024 * 10,
                file_name: "medium_read_write",
            },
            DataSizeRange { min: 100, max: 200 },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }

    pub(crate) fn large_data_bench_write_only(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 2048,
                file_size: 1024 * 1024 * 100,
                file_name: "large_write_only",
            },
            DataSizeRange {
                min: 1000,
                max: 2000,
            },
            Ratio {
                writes: 1,
                reads: 0,
            },
        );
    }

    pub(crate) fn large_data_bench_read_write(c: &mut Criterion) {
        bench(
            c,
            RingBufferConfig {
                block_size: 2048,
                file_size: 1024 * 1024 * 100,
                file_name: "large_read_write",
            },
            DataSizeRange {
                min: 1000,
                max: 2000,
            },
            Ratio {
                writes: 1,
                reads: 1,
            },
        );
    }
}
criterion_group!(
    benches,
    fixed_mmap::small_data_bench_read_write,
    fixed_mmap::small_data_bench_write_only,
    fixed_mmap::medium_data_bench_read_write,
    fixed_mmap::medium_data_bench_write_only,
    fixed_mmap::large_data_bench_read_write,
    fixed_mmap::large_data_bench_write_only,
);
// criterion_group!(
//     benches,
//     fixed_async::small_data_bench_read_write,
//     fixed_async::small_data_bench_write_only,
//     fixed_async::medium_data_bench_read_write,
//     fixed_async::medium_data_bench_write_only,
//     fixed_async::large_data_bench_read_write,
//     fixed_async::large_data_bench_write_only,
// );
criterion_main!(benches);
