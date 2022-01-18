use std::collections::HashMap;

use chrono::{DateTime, Duration, Utc};

pub struct ModuleStartPlanner {
    tracked_modules: HashMap<String, WatchdogModule>,
}

impl ModuleStartPlanner {
    pub fn new() -> Self {
        Self {
            tracked_modules: HashMap::new(),
        }
    }

    pub fn get_modules_to_start(
        &mut self,
        modules_to_start: &[String],
        removed_modules: &[String],
    ) -> Vec<String> {
        let current_time = self.current_time();
        let untrack_after: Duration = Duration::minutes(5);

        // stop tracking removed modules
        for module in removed_modules {
            self.tracked_modules.remove(module);
        }

        // remove modules that have been healthy for long enough
        self.tracked_modules
            .retain(|_name, module| module.last_tracked + untrack_after <= current_time);

        // determine which modules to start
        let mut actual_modules_to_start = Vec::<String>::new();
        for module in modules_to_start {
            let tracker = self
                .tracked_modules
                .entry(module.clone())
                .or_insert(WatchdogModule {
                    last_tracked: current_time.clone(),
                    backoff: Backoff::new(current_time.clone()),
                });

            // check if module has reached the restart time
            if tracker.backoff.should_start(&current_time) {
                actual_modules_to_start.push(module.to_owned());
            }
            tracker.backoff.increment_backoff(current_time.clone());

            // update tracked time. This is used above to stop tracking modules after 5 minutes
            tracker.last_tracked = current_time;
        }

        actual_modules_to_start
    }

    fn current_time(&self) -> DateTime<Utc> {
        Utc::now()
    }
}

#[derive(Debug, Clone)]
struct WatchdogModule {
    last_tracked: DateTime<Utc>,
    backoff: Backoff,
}

#[derive(Debug, Clone)]
struct Backoff {
    start_next: DateTime<Utc>,
    increment: Duration,
    maximum: Duration,
}

impl Backoff {
    pub fn new(current_time: DateTime<Utc>) -> Self {
        let increment = Duration::seconds(10);
        let maximum = Duration::minutes(5);

        Self {
            start_next: current_time,
            increment,
            maximum,
        }
    }

    pub fn should_start(&self, current_time: &DateTime<Utc>) -> bool {
        &self.start_next <= current_time
    }

    pub fn increment_backoff(&mut self, current_time: DateTime<Utc>) {
        self.increment = std::cmp::max(self.increment * 2, self.maximum);
        self.start_next = current_time + self.increment;
    }
}
