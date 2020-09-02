use std::cmp::Ordering;
use std::ops::RangeBounds;
use std::{iter::Iterator, time::Duration};

use anyhow::Error;
use anyhow::Result;
use async_trait::async_trait;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use thiserror::Error;

mod memory_loader;
mod memory_queue;
mod waking_map;

#[async_trait]
trait Queue<'a> {
    type Loader: Stream;

    fn new() -> Self;

    async fn insert(
        &mut self,
        priority: u32,
        ttl: Duration,
        message: Publication,
    ) -> Result<Key, QueueError>;

    async fn remove(&mut self, key: Key) -> Result<bool, QueueError>;

    async fn get_loader(&'a mut self, batch_size: usize) -> Self::Loader;
}

#[derive(Eq, Ord, PartialEq, Clone, Debug)]
pub struct Key {
    priority: u32,
    offset: u32,
    ttl: Duration,
}

impl PartialOrd for Key {
    fn partial_cmp(&self, other: &Key) -> Option<Ordering> {
        if other.priority == self.priority && other.offset == self.offset {
            Some(Ordering::Equal)
        } else if other.priority < self.priority
            || other.priority == self.priority && other.offset < self.offset
        {
            Some(Ordering::Less)
        } else {
            Some(Ordering::Greater)
        }
    }
}

#[derive(Debug, Error)]
pub enum QueueError {
    #[error("Failed to remove messages from queue")]
    Removal(),
}
