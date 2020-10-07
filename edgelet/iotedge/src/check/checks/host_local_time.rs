use crate::check::{checker::Checker, Check, CheckResult};
use chrono::{DateTime, Utc};
use edgelet_core::RuntimeSettings;
use failure::{self, Context, Fail, ResultExt};
use hyper::Client;
use hyper_tls::HttpsConnector;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct HostLocalTime {
    offset: Option<i64>,
}

impl Checker for HostLocalTime {
    fn id(&self) -> &'static str {
        "host-local-time"
    }
    fn description(&self) -> &'static str {
        "host time is close to reference time"
    }
    fn execute(&mut self, check: &mut Check, runtime: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check, runtime)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl HostLocalTime {
    fn inner_execute(
        &mut self,
        check: &mut Check,
        runtime: &mut tokio::runtime::Runtime,
    ) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        if let Some(parent_hostname) = settings.parent_hostname() {
            self.check_vs_parent_time(runtime, parent_hostname)
        } else {
            // This test is not run if parent hostname is not defined
            self.check_vs_ntp_time(check)
        }
    }

    fn check_vs_ntp_time(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
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

    fn check_vs_parent_time(
        &mut self,
        runtime: &mut tokio::runtime::Runtime,
        parent_hostname: &str,
    ) -> Result<CheckResult, failure::Error> {
        let address = format!("https://{}", parent_hostname);
        let parent_address = address
            .parse::<hyper::Uri>()
            .with_context(|_| format!("unable to parse address{}", address))?;

        let https = HttpsConnector::new(1).with_context(|_| "Could not create https connector")?;

        let response = runtime.block_on(
            Client::builder()
                .build::<_, hyper::Body>(https)
                .get(parent_address),
        )?;

        let date = response
            .headers()
            .get("Date")
            .ok_or_else(|| Context::new("Could not get date"))?
            .to_str()
            .with_context(|_| "Could not convert date to string")?;

        let offset = DateTime::parse_from_rfc2822(date)
            .with_context(|_| format!("Could not parse date string {}", date))?
            .signed_duration_since(Utc::now())
            .num_seconds()
            .abs();

        self.offset = Some(offset);
        if offset >= 10 {
            return Ok(CheckResult::Warning(Context::new(format!(
            "Time on the device is out of sync its parent with a delay of {} seconds. This may cause problems connecting to the parent.\n\
             Please ensure time on device and on the parent is accurate.",
             offset,
        )).into()));
        }

        Ok(CheckResult::Ok)
    }
}
