use std::{
    fs::{self, File},
    io::prelude::*,
    path::Path,
};

use anyhow::{Context, Result};

pub fn write_binary_to_file<P: AsRef<Path>>(content: &[u8], path: P) -> Result<()> {
    let mut f = File::create(path).context("Cannot create file")?;
    f.write_all(content)
        .context("File: Cannot write to file ")?;
    f.sync_data().context("File: cannot sync data")?;

    Ok(())
}

pub fn get_string_from_file<P: AsRef<Path>>(path: P) -> Result<String, anyhow::Error> {
    let str = fs::read_to_string(path).context("Unable to read file")?;
    Ok(str)
}
