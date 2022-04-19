use std::path::PathBuf;

use failure::Context;
use tokio::process::Command;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct CheckCompatibility {}

#[async_trait::async_trait]
impl Checker for CheckCompatibility {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-compatibility",
            description: "Checks whether device is still compatible to run iotedge",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckCompatibility {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let script_path = PathBuf::from("/etc/aziot/edged/aziot-compatibility.sh");
        let (is_success, result_output) = get_compatibility_script_output(script_path).await?;
        if !is_success {
            return Ok(CheckResult::Failed(
                Context::new(format!("{}", result_output)).into(),
            ));
        }
        Ok(CheckResult::Ok)
    }
}

async fn get_compatibility_script_output(
    script_path: PathBuf,
) -> Result<(bool, String), failure::Error> {
    let mut inspect = Command::new(script_path);
    inspect.args(&["-a", "aziotedge"]);
    let inspect = inspect.output().await?;

    Ok((
        inspect.status.success(),
        String::from_utf8_lossy(&inspect.stdout).to_string(),
    ))
}
