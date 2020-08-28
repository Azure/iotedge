use std::{fs::{self, File}, io::prelude::*, sync::Arc};
use anyhow::{Context, Result};
use tokio::sync::Notify;

pub fn write_binary_to_file(file: &[u8], path: &str) -> Result<()> {
    let mut f = File::create(path).context(format!("Cannot create file, {}", path))?;
    f.write_all(file)
        .context(format!("File: Cannot write to file {}", path))?;
    f.sync_data().context("File: cannot sync data")?;

    Ok(())
}

pub fn get_string_from_file(path: &str) -> Result<String, anyhow::Error> {
    let str = fs::read_to_string(path).context(format!("Unable to read {:?}", path))?;
    Ok(str)
}

#[derive(Debug)]
pub struct ShutdownHandle(pub Arc<Notify>);

impl ShutdownHandle {
    pub async fn shutdown(self) {
        self.0.notify();
    }
}
