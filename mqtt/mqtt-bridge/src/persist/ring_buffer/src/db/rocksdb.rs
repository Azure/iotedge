use std::{
    collections::{HashMap, VecDeque},
    path::Path,
    sync::atomic::{AtomicUsize, Ordering},
};

use rocksdb::{IteratorMode, Options, DB};

use super::{error::DbError, DbResult, EmbeddedDatabase};

pub struct Rocksdb {
    db: DB,
    write_offsets: HashMap<String, AtomicUsize>,
    read_offsets: HashMap<String, AtomicUsize>,
}

impl Rocksdb {
    pub fn new(
        path: impl AsRef<Path> + Send + Sync + 'static,
    ) -> Self {
        if path.as_ref().exists() {
            std::fs::remove_dir_all(&path).unwrap();
        }

        let mut db_opts = Options::default();
        db_opts.create_missing_column_families(true);
        db_opts.create_if_missing(true);
        db_opts.set_allow_mmap_reads(true);
        db_opts.set_allow_mmap_writes(true);
        db_opts.set_max_write_buffer_number(4);
        db_opts.set_min_write_buffer_number_to_merge(2);
        db_opts.set_bytes_per_sync(1024 * 1024 * 1024);

        let db = DB::open(&db_opts, &path).unwrap();

        Self {
            db,
            write_offsets: HashMap::new(),
            read_offsets: HashMap::new(),
        }
    }
}

impl EmbeddedDatabase for Rocksdb {
    fn init(&mut self, names: Vec<String>) -> DbResult<()> {
        for name in &names {
            let opts = Options::default();
            self.db.create_cf(&name, &opts).map_err(|err| {
                DbError::from_err(format!("Failed to create cf {}", name), Box::new(err))
            })?;
            self.write_offsets.insert(name.clone(), AtomicUsize::default());
            self.read_offsets.insert(name.clone(), AtomicUsize::default());
        }
        Ok(())
    }

    fn save(&self, queue_name: &str, value: &Vec<u8>) -> DbResult<usize> {
        match self.db.cf_handle(queue_name) {
            Some(cf) => {
                let offset = self.write_offsets.get(queue_name).unwrap();
                let offset = offset.fetch_add(1, Ordering::SeqCst);

                self.db
                    .put_cf(cf, offset.to_string(), value)
                    .map_err(|err| {
                        DbError::from_err(
                            format!("Failed to put key {} into cf {}", offset, queue_name),
                            Box::new(err),
                        )
                    })?;

                Ok(offset)
            }
            None => Err(DbError::new(
                format!("No column family for {}", queue_name),
                None,
            )),
        }
    }

    fn load(&self, queue_name: &str) -> DbResult<Option<(usize, Vec<u8>)>> {
        match self.db.cf_handle(queue_name) {
            Some(cf) => {
                let offset = self.read_offsets.get(queue_name).unwrap();
                let offset = offset.fetch_add(1, Ordering::SeqCst);

                let option = self.db.get_cf(cf, offset.to_string()).map_err(|err| {
                    DbError::from_err(
                        format!("Failed to get with key {} from cf {}", offset, queue_name),
                        Box::new(err),
                    )
                })?;
                Ok(match option {
                    Some(value) => Some((offset, value)),
                    None => None,
                })
            }
            None => Err(DbError::new(
                format!("No column family for {}", queue_name),
                None,
            )),
        }
    }

    fn batch(&self, queue_name: &str, size: usize) -> DbResult<VecDeque<(usize, Vec<u8>)>> {
        match self.db.cf_handle(queue_name) {
            Some(cf) => {
                let batch = self
                    .db
                    .iterator_cf(cf, IteratorMode::Start)
                    .take(size)
                    .map(|(k, v)| (std::str::from_utf8(&k).unwrap().parse().unwrap(), v.into()))
                    .collect();
                Ok(batch)
            }
            None => Err(DbError::new(
                format!("No column family for {}", queue_name),
                None,
            )),
        }
    }

    fn remove(&self, queue_name: &str, key: &usize) -> DbResult<()> {
        match self.db.cf_handle(queue_name) {
            Some(cf) => self.db.delete_cf(cf, key.to_string()).map_err(|err| {
                DbError::from_err(format!("Failed to remove key {}", key), Box::new(err))
            }),
            None => Err(DbError::new(
                format!("No column family for {}", queue_name),
                None,
            )),
        }
    }
}
