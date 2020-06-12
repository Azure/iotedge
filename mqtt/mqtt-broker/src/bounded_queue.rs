use std::{
    cmp,
    collections::{HashMap, HashSet, VecDeque},
    fmt, mem,
    num::NonZeroUsize,
};

use tracing::{debug, warn};

use mqtt3::proto;

use crate::configuration::QueueFullAction;

/// BoundedQueue is a queue of publications with bounds by count and total payload size in bytes.
///
/// Packets will be queued until either of `max_len` or `max_size` limits is reached, and
/// then `when_full` strategy is applied.
///
/// None for `max_len` or `max_size` means "unbounded".
struct BoundedQueue {
    inner: VecDeque<proto::Publication>,
    max_len: Option<NonZeroUsize>,
    max_size: Option<NonZeroUsize>,
    when_full: QueueFullAction,
    cur_size: usize,
}

impl BoundedQueue {
    pub fn new(
        max_len: Option<NonZeroUsize>,
        max_size: Option<NonZeroUsize>,
        when_full: QueueFullAction,
    ) -> Self {
        BoundedQueue {
            inner: VecDeque::new(),
            max_len,
            max_size,
            when_full,
            cur_size: 0,
        }
    }

    pub fn pop_front(&mut self) -> Option<proto::Publication> {
        match self.inner.pop_front() {
            Some(publication) => {
                self.cur_size -= publication.payload.len();
                Some(publication)
            }
            None => None,
        }
    }

    pub fn push_back(&mut self, publication: proto::Publication) {
        if let Some(max_len) = self.max_len {
            if self.inner.len() >= max_len.get() {
                return self.handle_queue_limit(publication);
            }
        }

        if let Some(max_size) = self.max_size {
            let pub_len = publication.payload.len();
            if self.cur_size + pub_len > max_size.get() {
                return self.handle_queue_limit(publication);
            }
        }

        self.cur_size += publication.payload.len();
        self.inner.push_back(publication);
    }

    fn handle_queue_limit(&mut self, publication: proto::Publication) {
        match self.when_full {
            QueueFullAction::DropNew => {
                // do nothing
            }
            QueueFullAction::DropOld => {
                let _ = self.pop_front();
                self.cur_size += publication.payload.len();
                self.inner.push_back(publication);
            }
        };
    }
}
