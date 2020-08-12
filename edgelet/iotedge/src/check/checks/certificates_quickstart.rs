use std::ffi::OsStr;
use std::path::PathBuf;

use failure::{self, Context, ResultExt};

use edgelet_core::RuntimeSettings;

use super::identity_certificate_expiry::CertificateValidity;
use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct CertificatesQuickstart {
    device_ca_cert_path: Option<PathBuf>,
    certificate_info: Option<CertificateValidity>,
}

impl Checker for CertificatesQuickstart {
    fn id(&self) -> &'static str {
        "certificates-quickstart"
    }
    fn description(&self) -> &'static str {
        "production readiness: certificates"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl CertificatesQuickstart {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        check.device_ca_cert_path = Some({
            if let Some(certificates) = settings.certificates().device_cert() {
                certificates.device_ca_cert()?
            } else {
                let certs_dir = settings.homedir().join("hsm").join("certs");

                let mut device_ca_cert_path = None;

                let entries = std::fs::read_dir(&certs_dir).with_context(|_| {
                    format!("Could not enumerate files under {}", certs_dir.display())
                })?;
                for entry in entries {
                    let entry = entry.with_context(|_| {
                        format!("Could not enumerate files under {}", certs_dir.display())
                    })?;
                    let path = entry.path();
                    if let Some(file_name) = path.file_name().and_then(OsStr::to_str) {
                        if file_name.starts_with("device_ca_alias")
                            && file_name.ends_with(".cert.pem")
                        {
                            device_ca_cert_path = Some(path);
                            break;
                        }
                    }
                }

                device_ca_cert_path.ok_or_else(|| {
                    Context::new(format!(
                        "Could not find device CA certificate under {}",
                        certs_dir.display(),
                    ))
                })?
            }
        });
        self.device_ca_cert_path = check.device_ca_cert_path.clone();

        if settings.certificates().device_cert().is_none() {
            let certificate_info = CertificateValidity::parse(
                "Device CA certificate".to_owned(),
                check.device_ca_cert_path.clone().unwrap(),
            )?;
            let not_after = certificate_info.not_after;
            self.certificate_info = Some(certificate_info);

            let now = chrono::Utc::now();

            if not_after < now {
                return Ok(CheckResult::Warning(
                Context::new(format!(
                    "The Edge device is using self-signed automatically-generated development certificates.\n\
                     The certs expired at {}. Restart the IoT Edge daemon to generate new development certs.\n\
                     Please consider using production certificates instead. See https://aka.ms/iotedge-prod-checklist-certs for best practices.",
                    not_after,
                ))
                .into(),
            ));
            } else {
                return Ok(CheckResult::Warning(
                Context::new(format!(
                    "The Edge device is using self-signed automatically-generated development certificates.\n\
                     They will expire in {} days (at {}) causing module-to-module and downstream device communication to fail on an active deployment.\n\
                     After the certs have expired, restarting the IoT Edge daemon will trigger it to generate new development certs.\n\
                     Please consider using production certificates instead. See https://aka.ms/iotedge-prod-checklist-certs for best practices.",
                    (not_after - now).num_days(), not_after,
                ))
                .into(),
            ));
            }
        }

        Ok(CheckResult::Ok)
    }
}
