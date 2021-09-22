use failure::{self, Context};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ProxySettings {}

#[async_trait::async_trait]
impl Checker for ProxySettings {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "proxy-settings",
            description: "proxy settings are consistent in iotedged, moby daemon and config.toml",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ProxySettings{
    async fn inner_execute(&mut self, _check: &mut Check) -> Result<CheckResult, failure::Error> {    
        return Err(Context::new("Not implemented").into());
    }
}