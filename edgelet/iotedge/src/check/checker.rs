use crate::check::{Check, CheckResult};
use serde_json::json;

pub(crate) trait Checker {
    fn id(&self) -> &'static str;
    fn description(&self) -> &'static str;
    fn result(&mut self, check: &mut Check) -> CheckResult;
    fn get_json(&self) -> serde_json::Value;
}
