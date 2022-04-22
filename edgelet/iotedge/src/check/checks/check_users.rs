use crate::{
    check::{Check, CheckResult, Checker, CheckerMeta},
    internal::common::get_system_user,
};

#[derive(Default, serde::Serialize)]
pub(crate) struct CheckUsers {}

#[async_trait::async_trait]
impl Checker for CheckUsers {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-users",
            description: "IoT Edge Users are valid and can be queried from the system",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckUsers {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        // Todo : Add Check for TPM User.
        for user in ["aziotcs", "aziotks", "aziotid", "iotedge"] {
            if let Err(e) = get_system_user(user) {
                return Ok(CheckResult::Failed(anyhow::anyhow!(format!("{}", e))));
            }
        }
        Ok(CheckResult::Ok)
    }
}
