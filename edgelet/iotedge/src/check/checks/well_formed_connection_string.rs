use failure::{self, Context, ResultExt};

use edgelet_core::{self, Provisioning, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WellFormedConnectionString {
    iothub_hostname: Option<String>,
    device_id: Option<String>,
}

impl Checker for WellFormedConnectionString {
    fn id(&self) -> &'static str {
        "connection-string"
    }
    fn description(&self) -> &'static str {
        "config.yaml has well-formed connection string"
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl WellFormedConnectionString {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        if let Provisioning::Manual(manual) = settings.provisioning() {
            let (_, _, hub) = manual.parse_device_connection_string().context(
                "Invalid connection string format detected.\n\
                 Please check the value of the provisioning.device_connection_string parameter.",
            )?;
            check.iothub_hostname = Some(hub);
            self.iothub_hostname = check.iothub_hostname.clone();
        } else if check.iothub_hostname.is_none() {
            return Err(Context::new("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname").into());
        };

        Ok(CheckResult::Ok)
    }
}
