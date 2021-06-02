use crate::check::{Check, CheckResult};

pub(crate) trait Checker {
    fn id(&self) -> &'static str;
    fn description(&self) -> &'static str;
    fn execute(&mut self, check: &mut Check, runtime: &mut tokio::runtime::Runtime) -> CheckResult;
    fn get_json(&self) -> serde_json::Value;
}
