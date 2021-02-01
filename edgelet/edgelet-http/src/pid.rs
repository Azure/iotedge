// Copyright (c) Microsoft. All rights reserved.

use std::{cmp, fmt, io};

use futures::prelude::*;
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Request};
#[cfg(unix)]
use tokio_uds::UnixStream;

#[derive(Clone, Copy, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub enum Pid {
    None,
    Any,
    Value(i32),
}

impl fmt::Display for Pid {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match *self {
            Pid::None => write!(f, "none"),
            Pid::Any => write!(f, "any"),
            Pid::Value(pid) => write!(f, "{}", pid),
        }
    }
}

/// Pids are considered not equal when compared against
/// None, or equal when compared against Any. None takes
/// precedence, so Any is not equal to None.
impl cmp::PartialEq for Pid {
    fn eq(&self, other: &Pid) -> bool {
        match *self {
            Pid::None => false,
            Pid::Any => !matches!(*other, Pid::None),
            Pid::Value(pid1) => match *other {
                Pid::None => false,
                Pid::Any => true,
                Pid::Value(pid2) => pid1 == pid2,
            },
        }
    }
}

#[cfg(test)]
mod tests {
    use super::Pid;

    #[test]
    fn test_eq() {
        assert_ne!(Pid::None, Pid::None);
        assert_ne!(Pid::None, Pid::Any);
        assert_ne!(Pid::None, Pid::Value(42));
        assert_ne!(Pid::Any, Pid::None);
        assert_eq!(Pid::Any, Pid::Any);
        assert_eq!(Pid::Any, Pid::Value(42));
        assert_ne!(Pid::Value(42), Pid::None);
        assert_eq!(Pid::Value(42), Pid::Any);
        assert_eq!(Pid::Value(42), Pid::Value(42));
        assert_ne!(Pid::Value(0), Pid::Value(42));
    }
}

#[derive(Clone)]
pub struct PidService<T> {
    pid: Pid,
    inner: T,
}

impl<T> PidService<T> {
    pub fn new(pid: Pid, inner: T) -> Self {
        PidService { pid, inner }
    }
}

impl<T> Service for PidService<T>
where
    T: Service<ReqBody = Body>,
    <T as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<T as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
{
    type ReqBody = T::ReqBody;
    type ResBody = T::ResBody;
    type Error = T::Error;
    type Future = T::Future;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let mut req = req;
        req.extensions_mut().insert(self.pid);
        self.inner.call(req)
    }
}

pub trait UnixStreamExt {
    fn pid(&self) -> io::Result<Pid>;
}

impl UnixStreamExt for UnixStream {
    fn pid(&self) -> io::Result<Pid> {
        get_pid(self)
    }
}

#[cfg(target_os = "linux")]
use self::impl_linux::get_pid;

#[cfg(target_os = "linux")]
mod impl_linux {
    use std::os::unix::io::AsRawFd;
    use std::{io, mem};

    use libc::{c_void, getsockopt, ucred, SOL_SOCKET, SO_PEERCRED};
    use tokio_uds::UnixStream;

    use super::Pid;

    pub fn get_pid(sock: &UnixStream) -> io::Result<Pid> {
        let raw_fd = sock.as_raw_fd();
        let mut ucred = ucred {
            pid: 0,
            uid: 0,
            gid: 0,
        };
        let ucred_size = mem::size_of::<ucred>();

        // This paranoid check should be optimized-out
        assert_eq!(
            <usize as std::convert::TryFrom<_>>::try_from(u32::max_value())
                .map(|max_u32| ucred_size <= max_u32),
            Ok(true)
        );

        #[allow(clippy::cast_possible_truncation)]
        let mut ucred_size = ucred_size as u32;

        let ret = unsafe {
            getsockopt(
                raw_fd,
                SOL_SOCKET,
                SO_PEERCRED,
                &mut ucred as *mut ucred as *mut c_void,
                &mut ucred_size,
            )
        };
        if ret == 0 && ucred_size as usize == mem::size_of::<ucred>() {
            Ok(Pid::Value(ucred.pid))
        } else {
            Err(io::Error::last_os_error())
        }
    }
}

#[cfg(target_os = "macos")]
pub use self::impl_macos::get_pid;

#[cfg(target_os = "macos")]
pub mod impl_macos {
    use std::io;
    use std::os::unix::io::AsRawFd;

    use libc::getpeereid;
    use tokio_uds::UnixStream;

    use super::*;

    pub fn get_pid(sock: &UnixStream) -> io::Result<Pid> {
        unsafe {
            let raw_fd = sock.as_raw_fd();

            let mut uid = 0;
            let mut gid = 0;

            let ret = getpeereid(raw_fd, &mut uid, &mut gid);

            if ret == 0 {
                #[allow(clippy::cast_possible_truncation, clippy::cast_possible_wrap)]
                Ok(Pid::Value(uid as _))
            } else {
                Err(io::Error::last_os_error())
            }
        }
    }
}
