use std::time::Duration;

use serde::Deserialize;

/// `RingBuffer` can be configured to flush at certain intervals:
///
/// `AfterEachWrite` - Every write to buffer will cause a flush. This gives the
///                    most durability but will cause decrease in performance.
///
/// `AfterXWrites` - Similar to `AfterEachWrite` but  will instead only flush
///                  after 'X' writes to the `RingBuffer`. This allows
///                  sacrificing durability for some performance. It is also
///                  important to note that if 'X' is 0 or 1, it is same as
///                  `AfterEachWrite`.
///
/// `AfterXBytes` - If 'X' bytes are written then a flush will happen.
///
/// `AfterXTime` - This allows for configuring around intervals of time
///                for flushing. This does not mean that every 'X' a
///                flush is called regardless of data incoming. This is
///                for the difference between data is incoming. So, if
///                5 inserts occur and after 3 the 'X' elapse a
///                flush will happen. If only 2 inserts occur and 'X'
///                ms is not reached then no flush occurs.
///
/// `Off` - No explicit flush will be called. This does not guarantee that no
///         flush will occur or that data will not be pushed to 'disk'. This is
///         the most performant option, but also the least durable.
///
/// It is important to note that after the threshold is reached for `AfterX`...
/// a new window for tracking begins.
#[derive(Clone, Copy, Debug, PartialEq, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum FlushOptions {
    AfterEachWrite,
    AfterXWrites(u64),
    AfterXBytes(u64),
    AfterXTime(Duration),
    Off,
}

/// A stateful object for tracking progress of any of the `AfterX`... options
/// from `FlushOptions`.
#[derive(Debug, Default)]
pub struct FlushState {
    pub writes: u64,
    pub bytes_written: u64,
    pub elapsed: Duration,
}

impl FlushState {
    pub(crate) fn reset(&mut self, flush_option: &FlushOptions) {
        match flush_option {
            FlushOptions::AfterEachWrite | FlushOptions::Off => {}
            FlushOptions::AfterXWrites(_) => {
                self.writes = u64::default();
            }
            FlushOptions::AfterXBytes(_) => {
                self.bytes_written = u64::default();
            }
            FlushOptions::AfterXTime(_) => {
                self.elapsed = Duration::default();
            }
        }
    }

    pub(crate) fn update(&mut self, writes: u64, bytes_written: u64, elapsed: Duration) {
        self.bytes_written += bytes_written;
        self.elapsed += elapsed;
        self.writes += writes;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn it_updates_flush_state_on_update() {
        let mut fs = FlushState::default();
        fs.update(1, 0, Duration::default());
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.elapsed, Duration::default());
        fs.update(0, 1, Duration::default());
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::default());
        fs.update(0, 0, Duration::from_millis(1));
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.update(1, 1, Duration::from_millis(1));
        assert_eq!(fs.writes, 2);
        assert_eq!(fs.bytes_written, 2);
        assert_eq!(fs.elapsed, Duration::from_millis(2));
    }

    #[test]
    fn it_resets_flush_state_on_reset() {
        let mut fs = FlushState::default();
        fs.update(1, 1, Duration::from_millis(1));
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.reset(&FlushOptions::AfterEachWrite);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.reset(&FlushOptions::Off);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.reset(&FlushOptions::AfterXWrites(0));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.reset(&FlushOptions::AfterXBytes(0));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.elapsed, Duration::from_millis(1));
        fs.reset(&FlushOptions::AfterXTime(Duration::default()));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.elapsed, Duration::default());
    }
}
