use std::collections::HashMap;

use chrono::{DateTime, Duration, Utc};

#[cfg(test)]
lazy_static::lazy_static! {
    static ref  CURRENT_TIME: std::sync::Mutex<DateTime<Utc>> = {
        let now = Utc::now();
        std::sync::Mutex::new(now)
    };
}

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
            .retain(|_name, module| module.last_tracked + untrack_after >= current_time);

        // determine which modules to start
        let mut actual_modules_to_start = Vec::<String>::new();
        for module in modules_to_start {
            if let Some(mut tracker) = self.tracked_modules.get_mut(module) {
                // check if module has reached the restart time
                if tracker.backoff.should_start(&current_time) {
                    actual_modules_to_start.push(module.to_owned());
                    tracker.backoff.increment_backoff(current_time.clone());
                }

                // update tracked time. This is used above to stop tracking modules after 5 minutes
                tracker.last_tracked = current_time;
            } else {
                actual_modules_to_start.push(module.to_owned());

                self.tracked_modules.insert(
                    module.to_owned(),
                    WatchdogModule {
                        last_tracked: current_time.clone(),
                        backoff: Backoff::new(current_time.clone()),
                    },
                );
            }
        }

        actual_modules_to_start
    }

    fn current_time(&self) -> DateTime<Utc> {
        #[cfg(not(test))]
        {
            Utc::now()
        }
        #[cfg(test)]
        {
            CURRENT_TIME.lock().unwrap().clone()
        }
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
            start_next: current_time + increment,
            increment,
            maximum,
        }
    }

    pub fn should_start(&self, current_time: &DateTime<Utc>) -> bool {
        &self.start_next <= current_time
    }

    pub fn increment_backoff(&mut self, current_time: DateTime<Utc>) {
        self.increment = std::cmp::min(self.increment * 2, self.maximum);
        self.start_next = current_time + self.increment;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn basic_backoff() {
        let mut planner = ModuleStartPlanner::new();

        // All modules should be started
        let modules_to_start: &[String] = &["foo".to_string(), "bar".to_string()];
        let planned = planner.get_modules_to_start(modules_to_start, &[]);
        assert_eq!(modules_to_start, &planned);

        // Foo should not be started, since only 6 seconds have passed
        increment_time(Duration::seconds(6));
        let modules_to_start: &[String] = &["foo".to_string(), "baz".to_string()];
        let expected: &[String] = &["baz".to_string()];
        let planned = planner.get_modules_to_start(modules_to_start, &[]);
        assert_eq!(expected, &planned);

        // Foo should be started, since 12 seconds have passed. Baz should not, since only 6 have
        increment_time(Duration::seconds(6));
        let modules_to_start: &[String] = &["foo".to_string(), "baz".to_string()];
        let expected: &[String] = &["foo".to_string()];
        let planned = planner.get_modules_to_start(modules_to_start, &[]);
        assert_eq!(expected, &planned);

        // Foo should not be started, since 12 seconds have passed, and it should now wait 20 instead of 10. Baz should start, since 18 seconds have passed since it was started
        increment_time(Duration::seconds(12));
        let modules_to_start: &[String] = &["foo".to_string(), "baz".to_string()];
        let expected: &[String] = &["baz".to_string()];
        let planned = planner.get_modules_to_start(modules_to_start, &[]);
        assert_eq!(expected, &planned);

        // simulate foo failing for 10 minutes. This should set the foo'sbackoff to 5 minutes. All other modules should be considered healthy
        let modules_to_start: &[String] = &["foo".to_string()];
        for _ in 0..(60 * 10) {
            increment_time(Duration::seconds(1));
            planner.get_modules_to_start(modules_to_start, &[]);
        }
        let expected: &[String] = &["foo".to_string()];
        let tracked: Vec<String> = planner.tracked_modules.keys().map(Clone::clone).collect();
        assert_eq!(expected, &tracked);

        // Simulate 20 minutes passing. foo should be started exactly 4 times
        let mut foo_started = 0;
        let modules_to_start: &[String] = &["foo".to_string()];
        for _ in 0..(60 * 20) {
            increment_time(Duration::seconds(1));
            let modules = planner.get_modules_to_start(modules_to_start, &[]);
            if &modules == modules_to_start {
                foo_started += 1;
            }
        }
        assert_eq!(4, foo_started);
    }

    fn increment_time(duration: Duration) {
        let mut current = CURRENT_TIME.lock().unwrap();
        *current = *current + duration;
    }
}
