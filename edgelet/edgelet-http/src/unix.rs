// Copyright (c) Microsoft. All rights reserved.

use std::fs;
#[cfg(unix)]
use std::os::unix::prelude::PermissionsExt;
use std::path::Path;

use failure::ResultExt;
use log::{debug, error};
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use crate::error::{Error, ErrorKind};
use crate::util::{incoming::Incoming, socket_file_exists};

pub fn listener<P: AsRef<Path>>(path: P, unix_socket_permission: u32) -> Result<Incoming, Error> {
    let path = path.as_ref();
    let path_display = path.display();

    if socket_file_exists(path) {
        debug!("unlinking {}...", path_display);

        let err1 = fs::remove_file(path).err();
        let err2 = fs::remove_dir_all(path).err();
        if let Some((err1, err2)) = err1.zip(err2) {
            error!("Could not unlink existing socket: [{}] [{}]", err1, err2);
            return Err(ErrorKind::Path(path_display.to_string()).into());
        }
        debug!("unlinked {}", path_display);
    }

    // If parent doesn't exist, create it and socket will be created inside.
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).with_context(|err| {
            error!("Cannot create directory, error: {}", err);
            ErrorKind::Path(path_display.to_string())
        })?;
    }

    let listener =
        UnixListener::bind(&path).with_context(|_| ErrorKind::Path(path_display.to_string()))?;
    debug!("bound {}", path_display);

    set_permissions(path, unix_socket_permission)?;

    Ok(Incoming::Unix(listener))
}

#[cfg(unix)]
fn set_permissions(path: &Path, unix_socket_permission: u32) -> Result<(), Error> {
    fs::set_permissions(path, fs::Permissions::from_mode(unix_socket_permission)).map_err(
        |err| {
            error!("Cannot set directory permissions: {}", err);
            ErrorKind::Path(path.display().to_string())
        },
    )?;

    Ok(())
}

#[cfg(windows)]
fn set_permissions(_path: &Path, _unix_socket_permission: u32) -> Result<(), Error> {
    Ok(())
}

#[cfg(test)]
#[cfg(unix)]
mod tests {
    use super::listener;
    use std::fs::OpenOptions;
    use std::os::unix::fs::MetadataExt;
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

        let listener = listener(&path, 0o666).unwrap();
        let _srv = listener.for_each(move |(_socket, _addr)| Ok(()));

        let file_stat = stat(&path).unwrap();
        assert_eq!(0o666, file_stat.st_mode & 0o777);

        dir.close().unwrap();
    }

    #[test]
    fn test_socket_not_created() {
        let dir = tempdir().unwrap();
        let path = dir.path().join("dummy_socket.sock");

        let listener = listener(&path, 0o666).unwrap();
        let _srv = listener.for_each(move |(_socket, _addr)| Ok(()));

        let file_stat = stat(&path).unwrap();
        assert_eq!(0o666, file_stat.st_mode & 0o777);

        dir.close().unwrap();
    }
}
