use std::cell::RefCell;
use std::collections::btree_map::Range;
use std::collections::BTreeMap;
use std::rc::Rc;
use std::sync::Arc;
use std::task::Waker;
use std::{iter::Iterator, time::Duration};

use anyhow::{Error, Result};
use async_trait::async_trait;
use mqtt3::proto::Publication;
// TODO REVIEW: do we need this tokio mutex
use tokio::sync::Mutex;

use crate::queue::{simple_message_loader::InMemoryMessageLoader, Key, Queue, QueueError};
pub struct WakingMap {
    map: BTreeMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingMap {
    pub fn new(map: BTreeMap<Key, Publication>) -> Self {
        WakingMap { map, waker: None }
    }

    pub fn insert(&mut self, key: Key, value: Publication) {
        self.map.insert(key, value);

        if let Some(waker) = self.waker.clone() {
            waker.wake();
        }
    }

    pub fn remove(&mut self, key: Key) -> Option<Publication> {
        self.map.remove(&key)
    }

    // exposed for specific loading logic
    pub fn get_map(&self) -> &BTreeMap<Key, Publication> {
        &self.map
    }

    pub fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};

    use crate::queue::QueueError;
    use crate::queue::{Key, Queue};

    #[tokio::test]
    async fn insert() {
        todo!()
    }

    #[tokio::test]
    async fn remove() {
        todo!()
    }

    #[tokio::test]
    async fn get_map() {
        todo!()
    }

    #[tokio::test]
    async fn set_waker() {
        todo!()
    }
}
