use std::{cmp::min, collections::HashMap, collections::VecDeque, task::Waker};

use mqtt3::proto::Publication;
use tracing::error;

use crate::persist::waking_state::StreamWakeableState;
use crate::persist::Key;
use crate::persist::PersistError;

/// Responsible for waking waiting streams when new elements are added.
///
/// Exposes a get method for retrieving a count of elements in order of insertion.
/// When elements are retrieved they are moved to the in flight collection.
pub struct WakingMap {
    queue: VecDeque<(Key, Publication)>,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingMap {
    pub fn new() -> Self {
        let queue: VecDeque<(Key, Publication)> = VecDeque::new();
        let in_flight = HashMap::new();

        WakingMap {
            queue,
            in_flight,
            waker: None,
        }
    }
}

impl StreamWakeableState for WakingMap {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    fn get(&mut self, count: usize) -> Vec<(Key, Publication)> {
        let count = min(count, self.queue.len());
        let mut output = vec![];
        for _ in 0..count {
            let removed = self.queue.pop_front();

            if let Some(pair) = removed {
                output.push((pair.0.clone(), pair.1.clone()));
                self.in_flight.insert(pair.0, pair.1);
            } else {
                error!("failed retrieving message from persistence");
                continue;
            }
        }

        output
    }

    fn remove_in_flight(&mut self, key: &Key) -> Option<Publication> {
        self.in_flight.remove(key)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
