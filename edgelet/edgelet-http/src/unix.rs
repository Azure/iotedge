// Copyright (c) Microsoft. All rights reserved.

use std::fs;
#[cfg(unix)]
use std::os::unix::fs::MetadataExt;
use std::path::Path;

use failure::ResultExt;
use log::debug;
#[cfg(unix)]
use nix::sys::stat::{umask, Mode};
#[cfg(unix)]
use scopeguard::defer;
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use crate::error::{Error, ErrorKind};
use crate::util::{incoming::Incoming, socket_file_exists};

pub fn listener<P: AsRef<Path>>(path: P) -> Result<Incoming, Error> {
    let listener = if socket_file_exists(path.as_ref()) {
        // get the previous file's metadata
        #[cfg(unix)]
        let metadata = get_metadata(path.as_ref())?;

        debug!("unlinking {}...", path.as_ref().display());
        fs::remove_file(&path)
            .with_context(|_| ErrorKind::Path(path.as_ref().display().to_string()))?;
        debug!("unlinked {}", path.as_ref().display());

        #[cfg(unix)]
        let prev = set_umask(&metadata, path.as_ref());
        #[cfg(unix)]
        defer! {{ umask(prev); }}

        debug!("binding {}...", path.as_ref().display());
        let listener = UnixListener::bind(&path)
            .with_context(|_| ErrorKind::Path(path.as_ref().display().to_string()))?;
        debug!("bound {}", path.as_ref().display());

        Incoming::Unix(listener)
    } else {
        let listener = UnixListener::bind(&path)
            .with_context(|_| ErrorKind::Path(path.as_ref().display().to_string()))?;
        Incoming::Unix(listener)
    };

    Ok(listener)
}

#[cfg(unix)]
fn get_metadata(path: &Path) -> Result<fs::Metadata, Error> {
    let metadata =
        fs::metadata(path).with_context(|_| ErrorKind::Path(path.display().to_string()))?;
    debug!("read metadata {:?} for {}", metadata, path.display());
    Ok(metadata)
}

#[cfg(unix)]
fn set_umask(metadata: &fs::Metadata, path: &Path) -> Mode {
    let mode = Mode::from_bits_truncate(metadata.mode());
    let mut mask = Mode::all();
    mask.toggle(mode);

    debug!("settings permissions {:#o} for {}...", mode, path.display());

    umask(mask)
}

#[cfg(test)]
#[cfg(unix)]
mod tests {
    use super::*;

    use std::fs::OpenOptions;
    use std::os::unix::fs::OpenOptionsExt;

    use futures::Stream;
    use nix::sys::stat::stat;
    use tempfile::tempdir;

    #[test]
    fn test_unlink() {
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

        let listener = listener(&path).unwrap();
        let _srv = listener.for_each(move |(_socket, _addr)| Ok(()));

        let file_stat = stat(&path).unwrap();
        assert_eq!(0o600, file_stat.st_mode & 0o777);

        dir.close().unwrap();
    }
}
