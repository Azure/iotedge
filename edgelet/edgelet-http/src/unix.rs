// Copyright (c) Microsoft. All rights reserved.

#![cfg(unix)]

use std::fs;
use std::os::unix::fs::MetadataExt;
use std::path::Path;

use nix::sys::stat::{umask, Mode};
use tokio_core::reactor::Handle;
use tokio_uds::UnixListener;

use error::Error;
use util::incoming::Incoming;

pub fn listener<P: AsRef<Path>>(path: P, handle: &Handle) -> Result<Incoming, Error> {
    let listener = if path.as_ref().exists() {
        // get the previous file's metadata
        let metadata = fs::metadata(&path)?;
        debug!("read metadata {:?} for {}", metadata, path.as_ref().display());

        debug!("unlinking {}...", path.as_ref().display());
        fs::remove_file(&path)?;
        debug!("unlinked {}", path.as_ref().display());

        let mode = Mode::from_bits_truncate(metadata.mode());
        let mut mask = Mode::all();
        mask.toggle(mode);

        debug!("settings permissions {:#o} for {}...", mode, path.as_ref().display());
        let prev = umask(mask);
        defer! {{ umask(prev); }}

        debug!("binding {}...", path.as_ref().display());
        let listener = UnixListener::bind(&path, &handle)?;
        debug!("bound {}", path.as_ref().display());

        Incoming::Unix(listener)
    } else {
        let listener = UnixListener::bind(path, &handle)?;
        Incoming::Unix(listener)
    };

    Ok(listener)
}

#[cfg(test)]
mod tests {

}
