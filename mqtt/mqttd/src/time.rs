//! `tokio::time::sleep` and `tokio::time::delay_until` has a bug that
//! prevents to schedule a very long timeout (more than 2 years).
//! To mitigate this issue we split a long interval by the number
//! of small intervals and schedule small intervals and waits a small one until
//! we reach a target.

use std::{
    cmp,
    future::Future,
    pin::Pin,
    task::{Context, Poll},
    time::Duration,
};

use futures_util::ready;
use pin_project::pin_project;
use tokio::time::{self, Instant, Sleep as TokioSleep};

const DEFAULT_DURATION: Duration = Duration::from_secs(30 * 24 * 60 * 60); // 30 days

/// Waits until `duration` has elapsed.
pub fn sleep(duration: Duration) -> Sleep {
    sleep_until(Instant::now() + duration)
}

/// Waits until `deadline` is reached.
pub fn sleep_until(deadline: Instant) -> Sleep {
    Sleep {
        deadline,
        delay: time::sleep_until(next_deadline(deadline, DEFAULT_DURATION)),
    }
}

/// A future returned by `sleep` and `sleep_until`.
#[pin_project]
pub struct Sleep {
    deadline: Instant,

    #[pin]
    delay: TokioSleep,
}

impl Future for Sleep {
    type Output = ();

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Self::Output> {
        let mut this = self.project();
        ready!(this.delay.as_mut().poll(cx));

        if Instant::now() >= *this.deadline {
            Poll::Ready(())
        } else {
            let deadline = next_deadline(*this.deadline, DEFAULT_DURATION);
            this.delay.reset(deadline);

            Poll::Pending
        }
    }
}

fn next_deadline(deadline: Instant, duration: Duration) -> Instant {
    cmp::min(deadline, Instant::now() + duration)
}
