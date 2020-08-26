use std::ops::RangeBounds;
use std::{iter::Iterator, time::Duration};

use anyhow::Error;
use anyhow::Result;
use mqtt3::proto::Publication;
use thiserror::Error;

mod simple_message_loader;
mod simple_queue;

// TODO: are these lifetimes correct?
trait Queue {
    type Loader: MessageLoader<'static>;

    // TODO: add name as per spec?
    fn new() -> Self;

    // TODO: futureproof make return key type a struct that takes all req fields but also convert to string
    fn insert(
        &mut self,
        priority: u32,
        ttl: Duration,
        message: Publication,
    ) -> Result<String, Error>;

    fn remove(&mut self, key: String) -> Result<bool, Error>;

    fn get_loader(&mut self, count: usize) -> Self::Loader;
}

// TODO: implement stream
// TODO: has reference to btreemap and will extract highest pri values in batches. next() will get from the current batch or trigger a batch update
// TODO: are we okay violating ttl if obtained in next batch?
trait MessageLoader<'a> {
    type Iter: Iterator<Item = (&'a String, &'a Publication)> + 'a;

    // TODO: change to keys
    fn range(&'a self, keys: impl RangeBounds<(String, Publication)>) -> Result<Self::Iter>;
}

#[derive(Debug, Error)]
pub enum QueueError {
    #[error("Failed to remove messages from queue")]
    Removal(),

    #[error("Failed loading message from queue")]
    LoadMessage(),
}
