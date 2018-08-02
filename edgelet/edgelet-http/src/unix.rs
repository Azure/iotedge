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
    use super::*;

    use std::fs::OpenOptions;
    use std::os::unix::fs::OpenOptionsExt;

    use futures::Stream;
    use nix::sys::stat::stat;
    use tempfile::tempdir;
    use tokio_core::reactor::Core;

    #[test]
    fn test_unlink() {
        let core = Core::new().unwrap();
        let dir = tempdir().unwrap();
        let path = dir.path().join("unlink.sock");
        let file = OpenOptions::new()
            .read(true)
            .write(true)
            .create_new(true)
            .mode(0o600)
            .open(path.clone())
            .unwrap();

        assert_eq!(0o600, file.metadata().unwrap().mode() & 0o7777);
        drop(file);

        let listener = listener(&path, &core.handle()).unwrap();
        let _srv = listener.for_each(move |(_socket, _addr)| {
            Ok(())
        });

        let file_stat = stat(&path).unwrap();
        assert_eq!(0o600, file_stat.st_mode  & 0o777);

        dir.close().unwrap();
    }
}
