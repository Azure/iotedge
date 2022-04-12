use std::{net::IpAddr, str::FromStr};

use anyhow::anyhow;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ParentHostname {
    config_parent_hostname: Option<String>,
}

#[async_trait::async_trait]
impl Checker for ParentHostname {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "parent_hostname",
            description: "configuration has correct parent_hostname",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ParentHostname {
    #[allow(clippy::unnecessary_wraps)]
    fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let config_parent_hostname =
            if let Some(config_parent_hostname) = check.parent_hostname.as_ref() {
                config_parent_hostname
            } else {
                // No parent hostname is a valid config.
                return Ok(CheckResult::Ignored);
            };

        self.config_parent_hostname = Some(config_parent_hostname.clone());

        if IpAddr::from_str(config_parent_hostname).is_ok() {
            //We can only check that it is a valid IP
            return Ok(CheckResult::Ok);
        }

        // Some software like the IoT Hub SDKs for downstream clients require the device hostname to follow RFC 1035.
        // For example, the IoT Hub C# SDK cannot connect to a hostname that contains an `_`.
        if !aziotctl_common::is_rfc_1035_valid(config_parent_hostname) {
            return Ok(CheckResult::Warning(anyhow!(
            "configuration has parent_hostname {} which does not comply with RFC 1035.\n\
             \n\
             - Hostname must be between 1 and 255 octets inclusive.\n\
             - Each label in the hostname (component separated by \".\") must be between 1 and 63 octets inclusive.\n\
             - Each label must start with an ASCII alphabet character (a-z, A-Z), end with an ASCII alphanumeric character (a-z, A-Z, 0-9), \
               and must contain only ASCII alphanumeric characters or hyphens (a-z, A-Z, 0-9, \"-\").\n\
             \n\
             Not complying with RFC 1035 may cause errors during the TLS handshake with modules and downstream devices.",
            config_parent_hostname,
        )));
        }

        if !aziotctl_common::check_length_for_local_issuer(config_parent_hostname) {
            return Ok(CheckResult::Failed(anyhow!(
                "configuration parent_hostname {} is too long to be used as a certificate issuer",
                config_parent_hostname,
            )));
        }

        Ok(CheckResult::Ok)
    }
}
