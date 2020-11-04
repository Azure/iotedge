//! `tokio::time::delay_for` and `tokio::time::delay_until` has a bug that
//! prevents to schedule a very long timeout (more than 2 years).
//! To mitigate this issue we split a long interval by the number
//! of small intervals and schedule small intervals and waits a small one until
//! we reach a target.

use std::{
    cmp,
    future::Future,
    pin::Pin,
    task::{Context, Poll},
};

use futures_util::{ready, FutureExt};
use tokio::time::{self, Delay, Duration, Instant};

const DEFAULT_DURATION: Duration = Duration::from_secs(30 * 24 * 60 * 60); // 30 days

/// Waits until `duration` has elapsed.
pub fn sleep(duration: Duration) -> Sleep {
    sleep_until(Instant::now() + duration)
}

/// Waits until `deadline` is reached.
pub fn sleep_until(deadline: Instant) -> Sleep {
    Sleep {
        deadline,
        delay: next_delay(deadline, DEFAULT_DURATION),
    }
}

/// A future returned by `sleep` and `sleep_until`
pub struct Sleep {
    deadline: Instant,
    delay: Delay,
}

impl Future for Sleep {
    type Output = ();

    fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        ready!(self.delay.poll_unpin(cx));

        if Instant::now() >= self.deadline {
            Poll::Ready(())
        } else {
            self.delay = next_delay(self.deadline, DEFAULT_DURATION);
            cx.waker().wake_by_ref();
            Poll::Pending
        }
    }
}

fn next_delay(deadline: Instant, duration: Duration) -> Delay {
    let delay = cmp::min(deadline, Instant::now() + duration);
    time::delay_until(delay)
}
