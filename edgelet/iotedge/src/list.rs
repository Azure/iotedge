// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::str::FromStr;
use std::sync::{Arc, Mutex};

use anyhow::Context;
use chrono::{DateTime, Duration, TimeZone, Utc};
use chrono_humanize::{Accuracy, HumanTime, Tense};
use tabwriter::TabWriter;

use edgelet_core::{Module, ModuleRuntime, ModuleStatus as ModuleStatusEnum};
use edgelet_http::ModuleStatus;

use crate::error::Error;
use crate::MgmtModule;

pub struct List<M, W> {
    runtime: M,
    output: Arc<Mutex<TabWriter<W>>>,
}

impl<M, W> List<M, W>
where
    W: Write,
{
    pub fn new(runtime: M, output: W) -> Self {
        let tab = TabWriter::new(output).minwidth(15);
        List {
            runtime,
            output: Arc::new(Mutex::new(tab)),
        }
    }
}

impl<M, W> List<M, W>
where
    M: ModuleRuntime<Module = MgmtModule>,
    W: Write,
{
    pub async fn execute(self) -> anyhow::Result<()> {
        let write = self.output.clone();
        let mut result = self
            .runtime
            .list_with_details()
            .await
            .context(Error::ModuleRuntime)?;

        result.sort_by(|(mod1, _), (mod2, _)| mod1.name().cmp(mod2.name()));

        let mut w = write.lock().unwrap();
        writeln!(w, "NAME\tSTATUS\tDESCRIPTION\tConfig").context(Error::WriteToStdout)?;
        for (module, _state) in result {
            writeln!(
                w,
                "{}\t{}\t{}\t{}",
                module.details.name,
                module.details.status.runtime_status.status,
                humanize_status(&module.details.status),
                module.image
            )
            .context(Error::WriteToStdout)?;
        }
        w.flush().context(Error::WriteToStdout)?;

        Ok(())
    }
}

fn humanize_status(status: &ModuleStatus) -> String {
    let status_enum = ModuleStatusEnum::from_str(&status.runtime_status.status)
        .unwrap_or(ModuleStatusEnum::Unknown);
    match status_enum {
        ModuleStatusEnum::Unknown => "Unknown".to_string(),
        ModuleStatusEnum::Stopped | ModuleStatusEnum::Dead => {
            if let Some(exit_status) = &status.exit_status {
                if let Ok(time) = DateTime::parse_from_rfc3339(&exit_status.exit_time) {
                    return format!("Stopped {}", format_time(time, Tense::Past));
                }
            }

            "Stopped".to_string()
        }
        ModuleStatusEnum::Failed => {
            if let Some(exit_status) = &status.exit_status {
                if let Ok(time) = DateTime::parse_from_rfc3339(&exit_status.exit_time) {
                    return format!(
                        "Failed ({}) {}",
                        exit_status.status_code,
                        format_time(time, Tense::Past)
                    );
                }
            }

            "Failed".to_string()
        }
        ModuleStatusEnum::Running => {
            if let Some(start_time) = &status.start_time {
                if let Ok(time) = DateTime::parse_from_rfc3339(start_time) {
                    return format!("Up {}", format_time(time, Tense::Present));
                }
            }

            "Up".to_string()
        }
    }
}

fn format_time<Tz>(time: DateTime<Tz>, tense: Tense) -> String
where
    Tz: TimeZone,
{
    let ht = HumanTime::from(Utc::now().signed_duration_since(time));
    if ht <= HumanTime::from(Duration::seconds(20)) {
        ht.to_text_en(Accuracy::Precise, tense)
    } else {
        ht.to_text_en(Accuracy::Rough, tense)
    }
}
