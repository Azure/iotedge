// Copyright (c) Microsoft. All rights reserved.

use std::process::Command;
use failure::ResultExt;
use crate::error::{Error, ErrorKind};

pub fn is_virtualized_env() -> Result<Option<bool>, Error> {
    if cfg!(target_os = "linux") {    
        let status = Command::new("systemd-detect-virt")
            .status()
            .context(ErrorKind::GetVirtualizationStatus)?;
        
        match status.code() {
            Some(0) => Ok(Some(true)),
            _ => Ok(Some(false))
        }
    } else {
        Ok(None)
    }
} 