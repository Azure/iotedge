#[allow(dead_code)]
#[derive(Clone, Copy, Debug)]
pub enum FlushOptions {
    AfterEachWrite,
    AfterXWrites(usize),
    AfterXBytes(usize),
    AfterXMilliseconds(usize),
    Off,
}

#[allow(dead_code)]
pub struct FlushState {
    pub writes: usize,
    pub bytes_written: usize,
    pub millis_elapsed: usize,
}

#[allow(dead_code)]
impl FlushState {
    pub(crate) fn new() -> Self {
        Self {
            writes: usize::default(),
            bytes_written: usize::default(),
            millis_elapsed: usize::default(),
        }
    }

    pub(crate) fn reset(&mut self, flush_option: &FlushOptions) {
        match flush_option {
            FlushOptions::AfterEachWrite => {}
            FlushOptions::AfterXWrites(_) => {
                self.writes = 0;
            }
            FlushOptions::AfterXBytes(_) => {
                self.bytes_written = 0;
            }
            FlushOptions::AfterXMilliseconds(_) => {
                self.millis_elapsed = 0;
            }
            FlushOptions::Off => {}
        }
    }

    pub(crate) fn update(&mut self, writes: usize, bytes_written: usize, millis_elapsed: usize) {
        self.bytes_written += bytes_written;
        self.millis_elapsed += millis_elapsed;
        self.writes += writes;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn it_updates_flush_state_on_update() {
        let mut fs = FlushState::new();
        fs.update(1, 0, 0);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.millis_elapsed, 0);
        fs.update(0, 1, 0);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 0);
        fs.update(0, 0, 1);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 1);
        fs.update(1, 1, 1);
        assert_eq!(fs.writes, 2);
        assert_eq!(fs.bytes_written, 2);
        assert_eq!(fs.millis_elapsed, 2);
    }

    #[test]
    fn it_resets_flush_state_on_reset() {
        let mut fs = FlushState::new();
        fs.update(1, 1, 1);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 1);
        fs.reset(&FlushOptions::AfterEachWrite);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 1);
        fs.reset(&FlushOptions::Off);
        assert_eq!(fs.writes, 1);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 1);
        fs.reset(&FlushOptions::AfterXWrites(0));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 1);
        assert_eq!(fs.millis_elapsed, 1);
        fs.reset(&FlushOptions::AfterXBytes(0));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.millis_elapsed, 1);
        fs.reset(&FlushOptions::AfterXMilliseconds(0));
        assert_eq!(fs.writes, 0);
        assert_eq!(fs.bytes_written, 0);
        assert_eq!(fs.millis_elapsed, 0);
    }
}
