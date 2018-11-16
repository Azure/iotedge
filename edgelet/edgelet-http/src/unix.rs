// Copyright (c) Microsoft. All rights reserved.

use std::fs;
#[cfg(unix)]
use std::os::unix::fs::MetadataExt;
use std::path::Path;

use failure::ResultExt;
#[cfg(unix)]
use nix::sys::stat::{umask, Mode};
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use error::{Error, ErrorKind};
use util::incoming::Incoming;

pub fn listener<P: AsRef<Path>>(path: P) -> Result<Incoming, Error> {
    let listener = if path.as_ref().exists() {
        // get the previous file's metadata
        let metadata = fs::metadata(&path)
            .with_context(|_| ErrorKind::Path(path.as_ref().display().to_string()))?;
        debug!(
            "read metadata {:?} for {}",
            metadata,
            path.as_ref().display()
        );

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
