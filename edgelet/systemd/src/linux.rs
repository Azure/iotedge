// Copyright (c) Microsoft. All rights reserved.

//! Implements the daemon interface for socket activation.
//! Provides two methods to get the resulting socket by name.
//! Based off of [`systemd_socket`](https://github.com/viraptor/systemd_socket)
//! and [`systemd-daemon`](https://github.com/systemd/systemd/tree/master/src/libsystemd/sd-daemon)

use std::collections::{hash_map, HashMap};
use std::env;
use std::net::SocketAddr;

use failure::ResultExt;
use nix::fcntl;
use nix::sys::socket::{self, AddressFamily, SockType};
use nix::sys::stat;
use nix::unistd::Pid;

use error::{Error, ErrorKind, SocketLookupType};
use {Fd, Socket};

pub const LISTEN_FDS_START: Fd = 3;

const ENV_PID: &str = "LISTEN_PID";
const ENV_FDS: &str = "LISTEN_FDS";
const ENV_NAMES: &str = "LISTEN_FDNAMES";

/// Returns the first listener for a file descriptor number.
///
/// Note that this value is biased by `LISTEN_FDS_START`. For example, an input of 0 corresponds to fd 3.
pub fn listener(num: usize) -> Result<Socket, Error> {
    debug!("Finding socket for number: {}", num);
    let sockets = listen_fds(false, LISTEN_FDS_START)?;
    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_possible_wrap,
        clippy::cast_sign_loss
    )]
    let socket = *sockets.get(num).ok_or_else(|| {
        Error::from(ErrorKind::SocketNotFound(SocketLookupType::Fd(
            (num + (LISTEN_FDS_START as usize)) as Fd,
        )))
    })?;
    Ok(socket)
}

/// Returns the first listener for a file descriptor name.
pub fn listener_name(name: &str) -> Result<Socket, Error> {
    debug!("Finding socket for name: {}", name);
    let sockets = listeners_name(name)?;
    let socket = sockets.into_iter().next().ok_or_else(|| {
        Error::from(ErrorKind::SocketNotFound(SocketLookupType::Name(
            name.to_string(),
        )))
    })?;
    Ok(socket)
}

/// Returns all of the listeners for a file descriptor name.
pub fn listeners_name(name: &str) -> Result<Vec<Socket>, Error> {
    debug!("Finding sockets for name: {}", name);
    let sockets = listen_fds_with_names(false, LISTEN_FDS_START)?;
    let sockets = sockets.get(name).ok_or_else(|| {
        Error::from(ErrorKind::SocketNotFound(SocketLookupType::Name(
            name.to_string(),
        )))
    })?;
    Ok(sockets.clone())
}

fn unsetenv_all() {
    env::remove_var(ENV_PID);
    env::remove_var(ENV_FDS);
    env::remove_var(ENV_NAMES);
}

fn get_env(key: &str) -> Result<String, Error> {
    Ok(env::var(key).with_context(|_| ErrorKind::InvalidVar(key.to_string()))?)
}

fn listen_fds(unset_environment: bool, start_fd: Fd) -> Result<Vec<Socket>, Error> {
    let pid_str = get_env(ENV_PID)?;
    debug!("{} {}", ENV_PID, pid_str);
    let pid = Pid::from_raw(
        pid_str
            .parse::<i32>()
            .context(ErrorKind::ParsePid(ENV_PID.to_string()))?,
    );

    if pid != Pid::this() {
        return Err(ErrorKind::WrongProcess(ENV_PID.to_string(), pid).into());
    }

    let fds_str = get_env(ENV_FDS)?;
    debug!("{} {}", ENV_FDS, fds_str);
    let num_fds = fds_str
        .parse::<Fd>()
        .with_context(|_| ErrorKind::InvalidVar(ENV_FDS.to_string()))?;
    if num_fds < 0 {
        return Err(ErrorKind::InvalidNumFds(ENV_FDS.to_string(), num_fds).into());
    }

    // Set CLOEXEC on each FD so that they aren't inherited by child processes
    for fd in start_fd..(start_fd + num_fds) {
        fcntl::fcntl(fd, fcntl::FcntlArg::F_SETFD(fcntl::FdFlag::FD_CLOEXEC))
            .context(ErrorKind::Syscall("fcntl"))?;
    }

    if unset_environment {
        unsetenv_all();
    }

    let sockets = (start_fd..(start_fd + num_fds))
        .map(|fd| {
            if let Some(addr) = is_socket_inet(fd, None, None, None, None).unwrap_or(None) {
                Socket::Inet(fd, addr)
            } else if is_socket_unix(fd, None, None).unwrap_or(false) {
                Socket::Unix(fd)
            } else {
                Socket::Unknown
            }
        })
        .collect();

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
        return Err(Error::from(ErrorKind::NumFdsDoesNotMatchNumFdNames(
            fds.len(),
            names.len(),
        )));
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
        return Err(Error::from(ErrorKind::InvalidFd(fd)));
    }

    let fs = stat::fstat(fd).context(ErrorKind::Syscall("fstat"))?;
    let mode = stat::SFlag::from_bits_truncate(fs.st_mode);
    if !mode.contains(stat::SFlag::S_IFSOCK) {
        return Ok(false);
    }

    if let Some(val) = socktype {
        let type_ = socket::getsockopt(fd, socket::sockopt::SockType)
            .context(ErrorKind::Syscall("getsockopt"))?;
        if type_ != val {
            return Ok(false);
        }
    }

    if let Some(val) = listening {
        let acc = socket::getsockopt(fd, socket::sockopt::AcceptConn)
            .context(ErrorKind::Syscall("getsockopt"))?;
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

    let sock_addr = socket::getsockname(fd).context(ErrorKind::Syscall("getsockname"))?;
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

    let sock_addr = socket::getsockname(fd).context(ErrorKind::Syscall("getsockname"))?;
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
        let pid = Pid::this();
        env::set_var(ENV_PID, format!("{}", pid));
    }

    fn create_fd(family: AddressFamily, type_: SockType) -> Fd {
        socket::socket(family, type_, socket::SockFlag::empty(), None).unwrap()
    }

    fn close_fds<I: IntoIterator<Item = Socket>>(sockets: I) {
        for socket in sockets {
            match socket {
                Socket::Inet(n, _) => unistd::close(n).unwrap(),
                Socket::Unix(u) => unistd::close(u).unwrap(),
                _ => (),
            }
        }
    }

    #[test]
    fn test_listen_fds() {
        let _l = lock_env();
        set_current_pid();
        env::set_var(ENV_FDS, "1");
        let listen_fds_start = create_fd(AddressFamily::Unix, SockType::Stream);
        let fds = listen_fds(true, listen_fds_start).unwrap();
        assert_eq!(1, fds.len());
        assert_eq!(vec![Socket::Unix(listen_fds_start)], fds);
        close_fds(fds);
    }

    #[test]
    fn test_listen_fds_with_names() {
        let _l = lock_env();
        set_current_pid();
        env::set_var(ENV_FDS, "2");
        env::set_var(ENV_NAMES, "a:b");
        let listen_fds_start = create_fd(AddressFamily::Inet, SockType::Stream);
        assert_eq!(
            create_fd(AddressFamily::Unix, SockType::Stream),
            listen_fds_start + 1
        );
        let fds = listen_fds_with_names(true, listen_fds_start).unwrap();
        assert_eq!(2, fds.len());
        if let Socket::Inet(fd, _) = fds["a"][0] {
            assert_eq!(fd, listen_fds_start);
        } else {
            panic!("Didn't parse Inet socket");
        }
        assert_eq!(vec![Socket::Unix(listen_fds_start + 1)], fds["b"]);

        for (_, socks) in fds {
            close_fds(socks);
        }
    }

    #[test]
    fn test_listen_fds_with_missing_env() {
        let _l = lock_env();

        match listen_fds_with_names(true, LISTEN_FDS_START) {
            Ok(_) => panic!("expected listen_fds_with_names to panic"),
            Err(err) => match err.kind() {
                ErrorKind::InvalidVar(s) if s == ENV_NAMES => (),
                _ => panic!(
                    "expected listen_fds_with_names to raise ErrorKind::InvalidVar({}) but it raised {:?}",
                    ENV_NAMES, err
                ),
            },
        }
    }
}
