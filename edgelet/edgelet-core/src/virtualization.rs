// Copyright (c) Microsoft. All rights reserved.

use std::process::Command;

use anyhow::Context;

use crate::error::Error;

pub fn is_virtualized_env() -> anyhow::Result<Option<bool>> {
    if cfg!(target_os = "linux") {
        let status = Command::new("systemd-detect-virt")
            .status()
            .context(Error::GetVirtualizationStatus)?;

        match status.code() {
            Some(0) => Ok(Some(true)),
            _ => Ok(Some(false)),
        }
    } else {
        Ok(None)
    }
}
