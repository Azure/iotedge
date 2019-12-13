// Copyright (c) Microsoft. All rights reserved.

use std::fmt::Display;
use std::io::Write;
use std::sync::{Arc, Mutex};

use chrono::{Duration, Utc};
use chrono_humanize::{Accuracy, HumanTime, Tense};
use failure::{Fail, ResultExt};
use futures::{Future, Stream};
use tabwriter::TabWriter;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState, ModuleStatus};

use crate::error::{Error, ErrorKind};
use crate::Command;

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

impl<M, W> Command for List<M, W>
where
    M: 'static + ModuleRuntime + Clone,
    M::Module: Clone,
    M::Config: Display,
    W: 'static + Write + Send,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let write = self.output.clone();
        let result = self
            .runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .collect()
            .and_then(move |mut result| {
                result.sort_by(|(mod1, _), (mod2, _)| mod1.name().cmp(mod2.name()));

                let mut w = write.lock().unwrap();
                writeln!(w, "NAME\tSTATUS\tDESCRIPTION\tCONFIG")
                    .context(ErrorKind::WriteToStdout)?;
                for (module, state) in result {
                    writeln!(
                        w,
                        "{}\t{}\t{}\t{}",
                        module.name(),
                        state.status(),
                        humanize_state(&state),
                        module.config(),
                    )
                    .context(ErrorKind::WriteToStdout)?;
                }
                w.flush().context(ErrorKind::WriteToStdout)?;
                Ok(())
            });
        Box::new(result)
    }
}

fn humanize_state(state: &ModuleRuntimeState) -> String {
    match *state.status() {
        ModuleStatus::Unknown => "Unknown".to_string(),
        ModuleStatus::Stopped => state.finished_at().map_or_else(
            || "Stopped".to_string(),
            |time| {
                format!(
                    "Stopped {}",
                    time_string(&HumanTime::from(Utc::now() - *time), Tense::Past)
                )
            },
        ),
        ModuleStatus::Failed => state
            .finished_at()
            .and_then(|time| {
                state.exit_code().map(|code| {
                    format!(
                        "Failed ({}) {}",
                        code,
                        time_string(&HumanTime::from(Utc::now() - *time), Tense::Past)
                    )
                })
            })
            .unwrap_or_else(|| "Failed".to_string()),
        ModuleStatus::Running => state.started_at().map_or_else(
            || "Up".to_string(),
            |time| {
                format!(
                    "Up {}",
                    time_string(&HumanTime::from(Utc::now() - *time), Tense::Present)
                )
            },
        ),
    }
}

fn time_string(ht: &HumanTime, tense: Tense) -> String {
    if *ht <= HumanTime::from(Duration::seconds(20)) {
        ht.to_text_en(Accuracy::Precise, tense)
    } else {
        ht.to_text_en(Accuracy::Rough, tense)
    }
}
