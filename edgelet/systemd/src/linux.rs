// Copyright (c) Microsoft. All rights reserved.

//! Implements the daemon interface for socket activation.
//! Provides two methods to get the resulting socket by name.
//! Based off of [systemd_socket](https://github.com/viraptor/systemd_socket)
//! and [systemd-daemon](https://github.com/systemd/systemd/tree/master/src/libsystemd/sd-daemon)

use std::collections::{hash_map, HashMap};
use std::env;
use std::net::SocketAddr;

use nix::fcntl;
use nix::sys::socket::{self, AddressFamily, SockType};
use nix::sys::stat;
use nix::unistd::{getpid, Pid};

use error::{Error, ErrorKind};
use {Fd, Socket};

const LISTEN_FDS_START: Fd = 3;
const ENV_PID: &str = "LISTEN_PID";
const ENV_FDS: &str = "LISTEN_FDS";
const ENV_NAMES: &str = "LISTEN_FDNAMES";

/// Returns the first listener for a file descriptor number.
pub fn listener(num: usize) -> Result<Socket, Error> {
    debug!("Finding socket for number: {}", num);
    listen_fds(false, LISTEN_FDS_START).and_then(|s| {
        s.get(num)
            .cloned()
            .ok_or_else(|| Error::from(ErrorKind::NotFound))
    })
}

/// Returns the first listener for a file descriptor name.
pub fn listener_name(name: &str) -> Result<Socket, Error> {
    debug!("Finding socket for name: {}", name);
    listeners_name(name).and_then(|s| {
        s.into_iter()
            .next()
            .ok_or_else(|| Error::from(ErrorKind::NotFound))
    })
}

/// Returns all of the listeners for a file descriptor name.
pub fn listeners_name(name: &str) -> Result<Vec<Socket>, Error> {
    debug!("Finding sockets for name: {}", name);
    listen_fds_with_names(false, LISTEN_FDS_START).and_then(|s| {
        s.get(name)
            .cloned()
            .ok_or_else(|| Error::from(ErrorKind::NotFound))
    })
}

fn unsetenv_all() {
    env::remove_var(ENV_PID);
    env::remove_var(ENV_FDS);
    env::remove_var(ENV_NAMES);
}

fn get_env(key: &str) -> Result<String, Error> {
    env::var(key).map_err(|_| Error::from(ErrorKind::Var(key.to_string())))
}

fn listen_fds(unset_environment: bool, start_fd: Fd) -> Result<Vec<Socket>, Error> {
    let pid_str = get_env(ENV_PID)?;
    debug!("{} {}", ENV_PID, pid_str);
    let pid: Pid = Pid::from_raw(pid_str.parse()?);

    if pid != getpid() {
        return Err(Error::from(ErrorKind::WrongProcess));
    }

    let fds_str = get_env(ENV_FDS)?;
    debug!("{} {}", ENV_FDS, fds_str);
    let fds: Fd = fds_str.parse()?;
    if fds < 0 {
        return Err(Error::from(ErrorKind::InvalidVar));
    }

    // Set CLOEXEC on each FD so that they aren't inherited by child processes
    for fd in start_fd..(start_fd + fds) {
        fcntl::fcntl(fd, fcntl::FcntlArg::F_SETFD(fcntl::FdFlag::FD_CLOEXEC))?;
    }

    if unset_environment {
        unsetenv_all();
    }

    let sockets = (start_fd..(start_fd + fds))
        .map(|fd| {
            if let Some(addr) = is_socket_inet(fd, None, None, None, None).unwrap_or(None) {
                Socket::Inet(fd, addr)
            } else if is_socket_unix(fd, None, None).unwrap_or(false) {
                Socket::Unix(fd)
            } else {
                Socket::Unknown
            }
        }).collect();

    Ok(sockets)
}

fn listen_fds_with_names(
    unset_environment: bool,
    start_fd: Fd,
) -> Result<HashMap<String, Vec<Socket>>, Error> {
    let names_str = get_env(ENV_NAMES)?;
    debug!("{} {}", ENV_NAMES, names_str);
    let names: Vec<&str> = names_str.split(':').collect();

    let fds = listen_fds(unset_environment, start_fd)?;
    if fds.len() != names.len() {
        return Err(Error::from(ErrorKind::InvalidVar));
    }

    let mut map: HashMap<String, Vec<Socket>> = HashMap::new();
    for (name, fd) in names.into_iter().zip(fds) {
        match map.entry(name.to_string()) {
            hash_map::Entry::Occupied(mut o) => o.get_mut().push(fd),
            hash_map::Entry::Vacant(v) => {
                v.insert(vec![fd]);
            }
        }
    }
    Ok(map)
}

fn is_socket_internal(
    fd: Fd,
    socktype: Option<SockType>,
    listening: Option<bool>,
) -> Result<bool, Error> {
    if fd < 0 {
        return Err(Error::from(ErrorKind::InvalidFd));
    }

    let fs = stat::fstat(fd)?;
    let mode = stat::SFlag::from_bits_truncate(fs.st_mode);
    if !mode.contains(stat::SFlag::S_IFSOCK) {
        return Ok(false);
    }

    if let Some(val) = socktype {
        let type_ = socket::getsockopt(fd, socket::sockopt::SockType)?;
        if type_ != val {
            return Ok(false);
        }
    }

    if let Some(val) = listening {
        let acc = socket::getsockopt(fd, socket::sockopt::AcceptConn)?;
        if acc != val {
            return Ok(false);
        }
    }
    Ok(true)
}

fn is_socket_inet(
    fd: Fd,
    family: Option<AddressFamily>,
    socktype: Option<SockType>,
    listening: Option<bool>,
    port: Option<u16>,
) -> Result<Option<SocketAddr>, Error> {
    if !is_socket_internal(fd, socktype, listening)? {
        return Ok(None);
    }

    let sock_addr = socket::getsockname(fd)?;
    let sock_family = sock_addr.family();
    if sock_family != AddressFamily::Inet && sock_family != AddressFamily::Inet6 {
        return Ok(None);
    }

    if let Some(val) = family {
        if sock_family != val {
            return Ok(None);
        }
    }

    let addr = match sock_addr {
        socket::SockAddr::Inet(x) => x.to_std(),
        _ => unreachable!(),
    };

    if let Some(val) = port {
        if addr.port() != val {
            return Ok(None);
        }
    }

    Ok(Some(addr))
}

fn is_socket_unix(
    fd: Fd,
    socktype: Option<SockType>,
    listening: Option<bool>,
) -> Result<bool, Error> {
    if !is_socket_internal(fd, socktype, listening)? {
        return Ok(false);
    }

    let sock_addr = socket::getsockname(fd)?;
    let sock_family = sock_addr.family();
    if sock_family != AddressFamily::Unix {
        return Ok(false);
    }
    Ok(true)
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::panic;
    use std::sync::{Mutex, MutexGuard};

    use nix::unistd;

    lazy_static! {
        static ref LOCK: Mutex<()> = Mutex::new(());
    }

    fn lock_env<'a>() -> MutexGuard<'a, ()> {
        LOCK.lock().unwrap()
    }

    fn set_current_pid() {
        let pid = getpid();
        env::set_var(ENV_PID, format!("{}", pid));
    }

    fn create_fd(no: i32, family: AddressFamily, type_: SockType) -> Fd {
        let fd = socket::socket(family, type_, socket::SockFlag::empty(), None).unwrap();
        assert_eq!(fd, no);
        fd
    }

    fn close_fds<'a, I: Iterator<Item = &'a Socket>>(sockets: I) {
        for socket in sockets {
            match socket {
                Socket::Inet(n, _) => unistd::close(*n).unwrap(),
                Socket::Unix(u) => unistd::close(*u).unwrap(),
                _ => (),
            }
        }
    }

    #[test]
    fn test_listen_fds() {
        let _l = lock_env();
        set_current_pid();
        env::set_var(ENV_FDS, "1");
        create_fd(3, AddressFamily::Unix, SockType::Stream);
        let fds = listen_fds(true, 3).unwrap();
        assert_eq!(1, fds.len());
        assert_eq!(vec![Socket::Unix(3)], fds);
        close_fds(fds.iter());
    }

    #[test]
    fn test_listen_fds_with_names() {
        let _l = lock_env();
        set_current_pid();
        env::set_var(ENV_FDS, "2");
        env::set_var(ENV_NAMES, "a:b");
        create_fd(3, AddressFamily::Inet, SockType::Stream);
        create_fd(4, AddressFamily::Unix, SockType::Stream);
        let fds = listen_fds_with_names(true, 3).unwrap();
        assert_eq!(2, fds.len());
        if let Socket::Inet(3, _) = fds["a"][0] {
        } else {
            panic!("Didn't parse Inet socket");
        }
        assert_eq!(vec![Socket::Unix(4)], fds["b"]);

        for socks in fds.values() {
            close_fds(socks.iter());
        }
    }

    #[test]
    fn test_listen_fds_with_missing_env() {
        let r = {
            let _l = lock_env();
            panic::catch_unwind(|| listen_fds_with_names(true, 3).unwrap())
        };

        match r {
            Ok(_) => panic!("expected listen_fds_with_names to panic"),
            Err(err) => match err.downcast_ref::<String>().map(String::as_str) {
                Some(s) if s.contains(ENV_NAMES) => (),
                other => panic!(
                    "expected listen_fds_with_names to panic with {} but it panicked with {:?}",
                    ENV_NAMES, other
                ),
            },
        }
    }
}
