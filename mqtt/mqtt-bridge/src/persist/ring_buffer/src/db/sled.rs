use std::{
    collections::{HashMap, VecDeque},
    path::Path,
    sync::atomic::{AtomicUsize, Ordering},
};

use sled::{Db, Tree};

use super::{error::DbError, DbResult, EmbeddedDatabase};

pub struct Sled {
    db: Db,
    queues: HashMap<String, Queue>,
}

impl Sled {
    pub fn new(path: impl AsRef<Path> + Send + Sync + 'static) -> Self {
        if path.as_ref().exists() {
            std::fs::remove_dir_all(&path).unwrap();
        }

        let db = sled::open(&path).unwrap();
        let queues = HashMap::new();

        Self { db, queues }
    }
}

impl EmbeddedDatabase for Sled {
    fn init(&mut self, names: Vec<String>) -> DbResult<()> {
        for name in names {
            let tree = self.db.open_tree(&name).map_err(|err| {
                DbError::from_err(format!("Failed to open with {}", name), Box::new(err))
            })?;
            self.queues.insert(name, Queue::new(tree));
        }
        Ok(())
    }

    fn save(&self, name: &str, value: &Vec<u8>) -> DbResult<usize> {
        match self.queues.get(name) {
            Some(queue) => queue.save(value),
            None => Err(DbError::new(format!("No tree for {}", name), None)),
        }
    }

    fn load(&self, name: &str) -> DbResult<Option<(usize, Vec<u8>)>> {
        match self.queues.get(name) {
            Some(queue) => queue.load(),
            None => Err(DbError::new(format!("No tree for {}", name), None)),
        }
    }

    fn batch(&self, name: &str, size: usize) -> DbResult<VecDeque<(usize, Vec<u8>)>> {
        match self.queues.get(name) {
            Some(queue) => queue.batch(size),
            None => Err(DbError::new(format!("No tree for {}", name), None)),
        }
    }

    fn remove(&self, name: &str, key: &usize) -> DbResult<()> {
        match self.queues.get(name) {
            Some(queue) => queue.remove(*key),
            None => Err(DbError::new(format!("No tree for {}", name), None)),
        }
    }
}

struct Queue {
    tree: Tree,
    write_offset: AtomicUsize,
    read_offset: AtomicUsize,
}

impl Queue {
    fn new(tree: Tree) -> Self {
        Self {
            tree,
            write_offset: AtomicUsize::default(),
            read_offset: AtomicUsize::default(),
        }
    }

    fn save(&self, item: &Vec<u8>) -> DbResult<usize> {
        let key = self.write_offset.fetch_add(1, Ordering::SeqCst);

        self.tree
            .insert(key.to_string(), item.clone())
            .map_err(|err| {
                DbError::from_err(format!("Failed to insert with key {}", key), Box::new(err))
            })?;
        // if key % 1000000 == 0 {
        //     self.tree
        //         .flush()
        //         .map_err(|err| DbError::from_err("Failed to flush".to_owned(), Box::new(err)))?;
        // }
        Ok(key)
    }

    fn load(&self) -> DbResult<Option<(usize, Vec<u8>)>> {
        let key = self.read_offset.fetch_add(1, Ordering::SeqCst);

        let option = self.tree.get(key.to_string()).map_err(|err| {
            DbError::from_err(format!("Failed to get with key {}", key), Box::new(err))
        })?;

        Ok(match option {
            Some(data) => Some((key, data.to_vec())),
            None => None,
        })
    }

    fn remove(&self, key: usize) -> DbResult<()> {
        self.tree.remove(key.to_string()).map_err(|err| {
            DbError::from_err("Failed to remove with key {}".to_owned(), Box::new(err))
        })?;
        // if key % 1000000 == 0 {
        //     self.tree
        //         .flush()
        //         .map_err(|err| DbError::from_err("Failed to flush".to_owned(), Box::new(err)))?;
        // }
        Ok(())
    }

    fn batch(&self, count: usize) -> DbResult<VecDeque<(usize, Vec<u8>)>> {
        Ok(self
            .tree
            .iter()
            .take(count)
            .filter_map(|i| {
                i.map_or_else(
                    |_| None,
                    |(k, v)| {
                        Some((
                            std::str::from_utf8(&k).unwrap().parse::<usize>().unwrap(),
                            v.to_vec(),
                        ))
                    },
                )
            })
            .collect())
    }
}
