use std::cmp::Ordering;
use std::ops::RangeBounds;
use std::{iter::Iterator, time::Duration};

use anyhow::Error;
use anyhow::Result;
use async_trait::async_trait;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use thiserror::Error;

mod simple_message_loader;
mod simple_queue;
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

// TODO: we probably don't want to order by ttl so should we implement the comparator's manually?
#[derive(Eq, PartialEq, PartialOrd, Ord, Clone, Debug)]
pub struct Key {
    priority: u32,
    offset: u32,
    ttl: Duration,
}

// impl Ord for Key {
//     fn cmp(&self, other: &Self) -> Ordering {}
//     fn max(self, other: Self) -> Self {}
//     fn min(self, other: Self) -> Self {}
// }

// impl PartialEq for Key {
//     fn eq(&self, other: Key) -> bool {
//         if other.priority == self.priority && other.offset == self.offset {
//             true
//         } else {
//             false
//         }
//     }
// }

// impl PartialOrd for Key {
//     fn partial_cmp(&self, other: Key) -> Option<Ordering> {
//         if other.priority == self.priority && other.offset == self.offset {
//             Some(Ordering::Equal)
//         } else if other.priority < self.priority
//             || other.priority == self.priority && other.offset < self.offset
//         {
//             Some(Ordering::Less)
//         } else {
//             Some(Ordering::Greater)
//         }
//     }
// }

// trait MessageLoader<'a> {
//     type Iter: Iterator<Item = (&'a String, &'a Publication)> + 'a;

//     // TODO: change to keys
//     fn range(&'a self, keys: impl RangeBounds<(String, Publication)>) -> Result<Self::Iter>;
// }

#[derive(Debug, Error)]
pub enum QueueError {
    #[error("Failed to remove messages from queue")]
    Removal(),

    #[error("Failed loading message from queue")]
    LoadMessage(),
}
