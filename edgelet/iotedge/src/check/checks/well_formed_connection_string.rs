use failure::{self, Context, ResultExt};

use edgelet_core::{self, ManualAuthMethod, Provisioning, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub struct WellFormedConnectionString {
    iothub_hostname: Option<String>,
}
impl Checker for WellFormedConnectionString {
    fn id(&self) -> &'static str {
        "connection-string"
    }
    fn description(&self) -> &'static str {
        "config.yaml has well-formed connection string"
    }
    fn result(&mut self, check: &mut Check) -> CheckResult {
        self.execute(check).unwrap_or_else(CheckResult::Failed)
    }
}
impl WellFormedConnectionString {
    fn execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
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
            check.iothub_hostname = Some(hub.to_owned());
        } else if check.iothub_hostname.is_none() {
            return Err(Context::new("Device is not using manual provisioning, so Azure IoT Hub hostname needs to be specified with --iothub-hostname").into());
        };

        self.iothub_hostname = check.iothub_hostname.clone();

        Ok(CheckResult::Ok)
    }
}
