// Copyright (c) Microsoft. All rights reserved.

use crate::error::{Error, ErrorKind};
use failure::ResultExt;
use std::process::Command;

pub fn is_virtualized_env() -> Result<Option<bool>, Error> {
    if cfg!(target_os = "linux") {
        let status = Command::new("systemd-detect-virt")
            .status()
            .context(ErrorKind::GetVirtualizationStatus)?;

        match status.code() {
            Some(0) => Ok(Some(true)),
            _ => Ok(Some(false)),
        }
    } else {
        Ok(None)
    }
}
