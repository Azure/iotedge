// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::sync::{Arc, Mutex};

use chrono::{Duration, Utc};
use chrono_humanize::{Accuracy, HumanTime, Tense};
use failure::{Fail, ResultExt};
use tabwriter::TabWriter;

use edgelet_core::{Module, ModuleRuntime, ModuleRuntimeState, ModuleStatus};

use crate::error::{Error, ErrorKind};
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
    pub async fn execute(self) -> Result<(), Error> {
        let write = self.output.clone();
        let mut result = self
            .runtime
            .list_with_details()
            .await
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))?;

        result.sort_by(|(mod1, _), (mod2, _)| mod1.name().cmp(mod2.name()));

        let mut w = write.lock().unwrap();
        writeln!(w, "NAME\tSTATUS\tDESCRIPTION\tImage").context(ErrorKind::WriteToStdout)?;
        for (module, _state) in result {
            writeln!(
                w,
                "{}\t{}\t{}\n{}",
                module.details.name,
                module.details.status.runtime_status.status,
                module
                    .details
                    .status
                    .runtime_status
                    .description
                    .unwrap_or_default(),
                module.image
            )
            .context(ErrorKind::WriteToStdout)?;
        }
        w.flush().context(ErrorKind::WriteToStdout)?;

        Ok(())
    }
}

fn time_string(ht: &HumanTime, tense: Tense) -> String {
    if *ht <= HumanTime::from(Duration::seconds(20)) {
        ht.to_text_en(Accuracy::Precise, tense)
    } else {
        ht.to_text_en(Accuracy::Rough, tense)
    }
}
