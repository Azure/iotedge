use failure::{self, Context};

use aziotctl_common::check_last_modified::check_last_modified;
use aziotctl_common::check_last_modified::CheckResult as InternalCheckResult;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct UpToDateConfig {}

impl Checker for UpToDateConfig {
    fn id(&self) -> &'static str {
        "config-up-to-date"
    }
    fn description(&self) -> &'static str {
        "configuration up-to-date with config.toml"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl UpToDateConfig {
    fn inner_execute(_check: &mut Check) -> Result<CheckResult, failure::Error> {
        let check_result = match check_last_modified(&["edged"]) {
            InternalCheckResult::Ok => CheckResult::Ok,
            InternalCheckResult::Ignored => CheckResult::Ignored,
            InternalCheckResult::Warning(message) => {
                CheckResult::Warning(Context::new(message).into())
            }
            InternalCheckResult::Failed(error) => CheckResult::Failed(error.into()),
        };

        Ok(check_result)
    }
}
