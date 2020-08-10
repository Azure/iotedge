use std::fs::File;
use std::io::Read;
use std::path::PathBuf;

use failure::{self, Context, ResultExt};

use edgelet_core::{self, AttestationMethod, ManualAuthMethod, ProvisioningType, RuntimeSettings};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct IdentityCertificateExpiry {
    provisioning_mode: Option<&'static str>,
    certificate_info: Option<CertificateValidity>,
}

impl Checker for IdentityCertificateExpiry {
    fn id(&self) -> &'static str {
        "identity-certificate-expiry"
    }
    fn description(&self) -> &'static str {
        "production readiness: identity certificates expiry"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl IdentityCertificateExpiry {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        match settings.provisioning().provisioning_type() {
            ProvisioningType::Dps(dps) => {
                if let AttestationMethod::X509(x509_info) = dps.attestation() {
                    self.provisioning_mode = Some("dps-x509");
                    let path = x509_info.identity_cert()?;

                    let result =
                        CertificateValidity::parse("DPS identity certificate".to_owned(), path)?;
                    self.certificate_info = Some(result.clone());
                    return result.to_check_result();
                }
                self.provisioning_mode = Some("dps-other");
            }
            ProvisioningType::Manual(manual) => {
                if let ManualAuthMethod::X509(x509) = manual.authentication_method() {
                    self.provisioning_mode = Some("manual-x509");
                    let path = x509.identity_cert()?;
                    let result = CertificateValidity::parse(
                        "Manual authentication identity certificate".to_owned(),
                        path,
                    )?;
                    self.certificate_info = Some(result.clone());
                    return result.to_check_result();
                }
                self.provisioning_mode = Some("manual-other");
            }
            ProvisioningType::External(_) => {
                self.provisioning_mode = Some("external");
            }
        }

        Ok(CheckResult::Ignored)
    }
}

#[derive(Debug, serde_derive::Serialize, Clone)]
pub(crate) struct CertificateValidity {
    cert_name: String,
    cert_path: PathBuf,
    pub(crate) not_after: chrono::DateTime<chrono::Utc>,
    not_before: chrono::DateTime<chrono::Utc>,
}

impl CertificateValidity {
    pub(crate) fn parse(cert_name: String, cert_path: PathBuf) -> Result<Self, failure::Error> {
        fn parse_openssl_time(
            time: &openssl::asn1::Asn1TimeRef,
        ) -> chrono::ParseResult<chrono::DateTime<chrono::Utc>> {
            // openssl::asn1::Asn1TimeRef does not expose any way to convert the ASN1_TIME to a Rust-friendly type
            //
            // Its Display impl uses ASN1_TIME_print, so we convert it into a String and parse it back
            // into a chrono::DateTime<chrono::Utc>
            let time = time.to_string();
            let time = chrono::NaiveDateTime::parse_from_str(&time, "%b %e %H:%M:%S %Y GMT")?;
            Ok(chrono::DateTime::<chrono::Utc>::from_utc(time, chrono::Utc))
        }

        let (not_after, not_before) = File::open(&cert_path)
            .map_err(failure::Error::from)
            .and_then(|mut device_ca_cert_file| {
                let mut device_ca_cert = vec![];
                device_ca_cert_file.read_to_end(&mut device_ca_cert)?;
                let device_ca_cert = openssl::x509::X509::stack_from_pem(&device_ca_cert)?;
                let device_ca_cert = &device_ca_cert[0];

                let not_after = parse_openssl_time(device_ca_cert.not_after())?;
                let not_before = parse_openssl_time(device_ca_cert.not_before())?;

                Ok((not_after, not_before))
            })
            .with_context(|_| {
                format!(
                    "Could not parse {} as a valid certificate file",
                    &cert_path.display(),
                )
            })?;

        Ok(CertificateValidity {
            cert_name,
            cert_path,
            not_after,
            not_before,
        })
    }

    fn to_check_result(&self) -> Result<CheckResult, failure::Error> {
        let cert_path_displayable = self.cert_path.display();

        let now = chrono::Utc::now();

        if self.not_before > now {
            Err(Context::new(format!(
                "{} at {} has not-before time {} which is in the future",
                self.cert_name, cert_path_displayable, self.not_before,
            ))
            .into())
        } else if self.not_after < now {
            Err(Context::new(format!(
                "{} at {} expired at {}",
                self.cert_name, cert_path_displayable, self.not_after,
            ))
            .into())
        } else if self.not_after < now + chrono::Duration::days(7) {
            Ok(CheckResult::Warning(
                Context::new(format!(
                    "{} at {} will expire soon ({}, in {} days)",
                    self.cert_name,
                    cert_path_displayable,
                    self.not_after,
                    (self.not_after - now).num_days(),
                ))
                .into(),
            ))
        } else {
            Ok(CheckResult::Ok)
        }
    }
}
