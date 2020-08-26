use failure::{self, Context, ResultExt};

use edgelet_core::{self, ManualAuthMethod, ProvisioningType, RuntimeSettings};

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
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
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

        if let ProvisioningType::Manual(manual) = settings.provisioning().provisioning_type() {
            let hub = match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    let (_, device, hub) = cs.parse_device_connection_string().context(
                                "Invalid connection string format detected.\n\
                                Please check the value of the provisioning.device_connection_string parameter.",
                    )?;
                    self.device_id = Some(device);
                    hub
                }
                ManualAuthMethod::X509(x509) => x509.iothub_hostname().to_owned(),
            };
            check.iothub_hostname = Some(hub);
            self.iothub_hostname = check.iothub_hostname.clone();
        } else if check.iothub_hostname.is_none() {
            let warning = "Device not configured with manual provisioning, in this configuration 'iotedge check' is not able to discover the device's backing IoT Hub.\n\
                            To run connectivity checks in this configuration please specify the backing IoT Hub name using --iothub-hostname switch if you have that information.\n\
                            If no hostname is provided, all hub connectivity tests will be skipped.";
            return Ok(CheckResult::Warning(Context::new(warning).into()));
        }

        Ok(CheckResult::Ok)
    }
}
