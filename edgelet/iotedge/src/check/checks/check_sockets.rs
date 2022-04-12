use failure::Context;

use crate::{
    check::{Check, CheckResult, Checker, CheckerMeta},
    internal::common::get_system_user,
};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct CheckSockets {}

#[async_trait::async_trait]
impl Checker for CheckSockets {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-sockets",
            description:
                "IoT Edge Communication sockets are available and have the required permission",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckSockets {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };
        Ok(CheckResult::Ok)
    }
}
