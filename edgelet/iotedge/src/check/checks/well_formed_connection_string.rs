use std;
use std::fs::File;

use failure::{self, Fail};

use edgelet_docker::Settings;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WellFormedConnectionString {
    result: CheckResult,
    iothub_hostname: Option<String>,
}
impl Checker for WellFormedConnectionString {
    fn id(&self) -> &'static str {
        "connection-string"
    }
    fn description(&self) -> &'static str {
        "config.yaml has well-formed connection string"
    }
    fn result(&self) -> &CheckResult {
        &self.result
    }
}
impl WellFormedConnectionString {
    pub fn new(check: &Check, config: &WellFormedConfig) -> Self {
        let mut checker = Self::default();
        checker.result = checker
            .execute(check, config)
            .unwrap_or_else(CheckResult::Failed);
        checker
    }

    fn execute(
        &mut self,
        check: &Check,
        config: &WellFormedConfig,
    ) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &config.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        if let Provisioning::Manual(manual) = settings.provisioning() {
            let hub = match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    let (_, _, hub) = cs.parse_device_connection_string().context(
                                    "Invalid connection string format detected.\n\
                                    Please check the value of the provisioning.device_connection_string parameter.",
                    )?;
                    hub
                }
                ManualAuthMethod::X509(x509) => x509.iothub_hostname().to_owned(),
            };

            self.iothub_hostname = Some(hub.to_owned());
        } else {
            self.iothub_hostname = check.iothub_hostname.clone();
            if check.iothub_hostname.is_none() {
                return Err(Context::new("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname").into());
            }
        };

        Ok(CheckResult::Ok)
    }
}
