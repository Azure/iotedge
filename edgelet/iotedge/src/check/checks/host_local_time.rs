use failure::{self, Context, Fail};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct HostLocalTime {
    offset: Option<i64>,
}

impl Checker for HostLocalTime {
    fn id(&self) -> &'static str {
        "host-local-time"
    }
    fn description(&self) -> &'static str {
        "host time is close to real time"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl HostLocalTime {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        fn is_server_unreachable_error(err: &mini_sntp::Error) -> bool {
            match err.kind() {
                mini_sntp::ErrorKind::ResolveNtpPoolHostname(_) => true,
                mini_sntp::ErrorKind::SendClientRequest(err)
                | mini_sntp::ErrorKind::ReceiveServerResponse(err) => {
                    err.kind() == std::io::ErrorKind::TimedOut || // Windows
                err.kind() == std::io::ErrorKind::WouldBlock // Unix
                }
                _ => false,
            }
        }

        let mini_sntp::SntpTimeQueryResult {
            local_clock_offset, ..
        } = match mini_sntp::query(&check.ntp_server) {
            Ok(result) => result,
            Err(err) => {
                if is_server_unreachable_error(&err) {
                    return Ok(CheckResult::Warning(
                        err.context("Could not query NTP server").into(),
                    ));
                } else {
                    return Err(err.context("Could not query NTP server").into());
                }
            }
        };

        let offset = local_clock_offset.num_seconds().abs();
        self.offset = Some(offset);
        if offset >= 10 {
            return Ok(CheckResult::Warning(Context::new(format!(
            "Time on the device is out of sync with the NTP server. This may cause problems connecting to IoT Hub.\n\
             Please ensure time on device is accurate, for example by {}.",
            if cfg!(windows) {
                "setting up the Windows Time service to automatically sync with a time server"
            } else {
                "installing an NTP daemon"
            },
        )).into()));
        }

        Ok(CheckResult::Ok)
    }
}
