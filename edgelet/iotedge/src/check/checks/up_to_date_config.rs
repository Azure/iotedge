use aziotctl_common::check_last_modified::{check_last_modified, LastModifiedError};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct UpToDateConfig {}

#[async_trait::async_trait]
impl Checker for UpToDateConfig {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "config-up-to-date",
            description: "configuration up-to-date with config.toml",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
}

impl UpToDateConfig {
    #[allow(clippy::unnecessary_wraps)]
    fn inner_execute(_check: &mut Check) -> anyhow::Result<CheckResult> {
        let check_result = match check_last_modified(&["edged"]) {
            Ok(()) => CheckResult::Ok,
            Err(LastModifiedError::Ignored) => CheckResult::Ignored,
            Err(LastModifiedError::Warning(message)) => {
                CheckResult::Warning(anyhow::anyhow!(message))
            }
            Err(LastModifiedError::Failed(error)) => CheckResult::Failed(error.into()),
        };

        Ok(check_result)
    }
}
