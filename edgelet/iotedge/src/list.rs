// Copyright (c) Microsoft. All rights reserved.

use std::fmt::Display;
use std::io::Write;
use std::sync::{Arc, Mutex};

use chrono::{Duration, Utc};
use chrono_humanize::{Accuracy, HumanTime, Tense};
use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState, ModuleStatus};
use futures::{future, Future};
use tabwriter::TabWriter;

use error::Error;
use Command;

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
    M::Error: Into<Error>,
    <M::Module as Module>::Error: Into<Error>,
    W: 'static + Write + Send,
{
    type Future = Box<Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
        let write = self.output.clone();
        let result = self
            .runtime
            .list()
            .map_err(|e| e.into())
            .and_then(move |list| {
                let modules = list.clone();
                let futures = list.into_iter().map(|m| m.runtime_state());
                future::join_all(futures)
                    .map_err(|e| e.into())
                    .and_then(move |states| {
                        let mut w = write.lock().unwrap();
                        writeln!(w, "NAME\tSTATUS\tDESCRIPTION\tCONFIG")?;
                        for (module, state) in modules.iter().zip(states) {
                            writeln!(
                                w,
                                "{}\t{}\t{}\t{}",
                                module.name(),
                                state.status(),
                                humanize_state(&state),
                                module.config(),
                            )?;
                        }
                        w.flush()?;
                        Ok(())
                    })
            });
        Box::new(result)
    }
}

fn humanize_state(state: &ModuleRuntimeState) -> String {
    match *state.status() {
        ModuleStatus::Unknown => "Unknown".to_string(),
        ModuleStatus::Stopped => state
            .finished_at()
            .map(|time| {
                format!(
                    "Stopped {}",
                    time_string(&HumanTime::from(Utc::now() - *time), Tense::Past)
                )
            }).unwrap_or_else(|| "Stopped".to_string()),
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
            }).unwrap_or_else(|| "Failed".to_string()),
        ModuleStatus::Running => state
            .started_at()
            .map(|time| {
                format!(
                    "Up {}",
                    time_string(&HumanTime::from(Utc::now() - *time), Tense::Present)
                )
            }).unwrap_or_else(|| "Up".to_string()),
    }
}

fn time_string(ht: &HumanTime, tense: Tense) -> String {
    if *ht <= HumanTime::from(Duration::seconds(20)) {
        ht.to_text_en(Accuracy::Precise, tense)
    } else {
        ht.to_text_en(Accuracy::Rough, tense)
    }
}
