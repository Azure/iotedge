use std::{
    collections::VecDeque,
    path::PathBuf,
    sync::atomic::{AtomicUsize, Ordering},
    task::Waker,
    time::Instant,
};

use sled::{Db, Result as SledResult, Tree};

use super::{error::StorageError, FlushOptions, FlushState, IOStatus, Storage, StorageResult};

pub struct Sled {
    _db: Db,
    queue: Queue,
    waker: Option<Waker>,
}

impl Sled {
    pub fn new(path: String, tree_name: String, flush_options: FlushOptions) -> Self {
        let path = PathBuf::from(path);
        if path.exists() && path.is_dir() {
            std::fs::remove_dir_all(&path).unwrap();
        }

        let db = sled::open(&path).unwrap();
        let tree = db
            .open_tree(tree_name.clone())
            .expect(format!("Failed to open tree {}", tree_name.clone()).as_str());
        let queue = Queue::new(tree, flush_options);

        Self {
            _db: db,
            queue,
            waker: None,
        }
    }
}

impl Storage<usize, Vec<u8>> for Sled {
    fn insert(&self, value: &Vec<u8>) -> StorageResult<IOStatus<()>> {
        self.queue
            .push(value)
            .map(|_| IOStatus::Ready(()))
            .map_err(StorageError::from)
    }

    fn batch(&self, size: usize) -> StorageResult<IOStatus<VecDeque<(usize, Vec<u8>)>>> {
        self.queue
            .batch(size)
            .map(|values| IOStatus::Ready(values))
            .map_err(StorageError::from)
    }

    fn remove(&self, key: &usize) -> StorageResult<IOStatus<()>> {
        self.queue
            .remove(*key)
            .map(|_| IOStatus::Ready(()))
            .map_err(StorageError::from)
    }

    fn set_waker(&mut self, waker: Waker) {
        self.waker = Some(waker)
    }
    
    fn waker(&mut self) -> &mut Option<Waker> {
        &mut self.waker
    }
}

struct Queue {
    tree: Tree,
    write_offset: AtomicUsize,
    flush_options: FlushOptions,
    flush_state: FlushState,
}

impl Queue {
    fn new(tree: Tree, flush_options: FlushOptions) -> Self {
        Self {
            tree,
            write_offset: AtomicUsize::default(),
            flush_options,
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

    pub fn push(&self, value: &Vec<u8>) -> SledResult<()> {
        let timer = Instant::now();
        let key = self.write_offset.fetch_add(1, Ordering::SeqCst);

        self.tree.insert(key.to_string(), value.clone())?;

        let should_flush = self.should_flush();

        if should_flush {
            self.tree.flush()?;
        }

        self.flush_state
            .update(1, value.len(), timer.elapsed().as_millis() as usize);

        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(())
    }

    pub fn remove(&self, key: usize) -> SledResult<()> {
        let timer = Instant::now();
        self.tree.remove(key.to_string())?;
        let should_flush = self.should_flush();

        if should_flush {
            self.tree.flush()?;
        }

        self.flush_state
            .update(0, 0, timer.elapsed().as_millis() as usize);

        if should_flush {
            self.flush_state.reset(&self.flush_options);
        }

        Ok(())
    }

    pub fn batch(&self, count: usize) -> SledResult<VecDeque<(usize, Vec<u8>)>> {
        let results = self
            .tree
            .iter()
            .take(count)
            .filter_map(|result| {
                result.map_or_else(
                    |_| None,
                    |(k, v)| {
                        Some((
                            std::str::from_utf8(&k).unwrap().parse::<usize>().unwrap(),
                            v.to_vec(),
                        ))
                    },
                )
            })
            .collect();
        Ok(results)
    }
}
