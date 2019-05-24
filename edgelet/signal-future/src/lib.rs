use std::sync::{Arc, Mutex};

use futures::prelude::*;
use futures::task::{current, Task};
use futures::Poll;

struct State {
    waker: Option<Task>,
    signalled: bool,
}

impl Default for State {
    fn default() -> Self {
        State {
            waker: None,
            signalled: false,
        }
    }
}

#[derive(Clone)]
pub struct SignalFuture {
    state: Arc<Mutex<State>>,
}

impl Default for SignalFuture {
    fn default() -> Self {
        SignalFuture {
            state: Arc::new(Mutex::new(Default::default())),
        }
    }
}

pub fn signal() -> SignalFuture {
    Default::default()
}

impl SignalFuture {
    pub fn signal(&mut self) {
        let mut state = self.state.lock().expect("Mutex lock error");
        state.signalled = true;
        if let Some(waker) = state.waker.as_ref() {
            waker.notify();
        }
    }
}

impl Future for SignalFuture {
    type Item = ();
    type Error = ();

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        let mut state = self.state.lock().expect("Mutex lock error");

        if state.waker.is_none() {
            state.waker = Some(current());
        }

        if state.signalled {
            Ok(Async::Ready(()))
        } else {
            Ok(Async::NotReady)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::time::{Duration, Instant};

    use tokio::timer::Delay;

    const WAIT_SECS: u64 = 2;

    #[test]
    fn signal_test() {
        let mut sf = signal();
        let sf2 = sf.clone();

        let signalled = Arc::new(AtomicBool::new(false));

        let t1 = Delay::new(Instant::now() + Duration::from_secs(WAIT_SECS))
            .map_err(|_| ())
            .map(move |_| sf.signal());

        let signalled_copy = signalled.clone();
        let t2 = sf2.map(move |_| signalled_copy.store(true, Ordering::SeqCst));

        tokio::run(t1.join(t2).map(|_| ()));
        assert_eq!(true, signalled.load(Ordering::SeqCst));
    }
}
