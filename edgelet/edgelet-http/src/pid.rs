// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::pid::Pid;
use futures::prelude::*;
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Request};

#[derive(Clone)]
pub struct PidService<T> {
    pid: Pid,
    inner: T,
}

impl<T> PidService<T> {
    pub fn new(pid: Pid, inner: T) -> PidService<T> {
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
        req.extensions_mut().insert(self.pid.clone());
        self.inner.call(req)
    }
}

#[cfg(unix)]
pub use self::impl_unix::UnixStreamExt;

#[cfg(unix)]
mod impl_unix {
    use libc::{c_void, getsockopt, ucred, SOL_SOCKET, SO_PEERCRED};
    use std::os::unix::io::AsRawFd;
    use std::{io, mem};
    use tokio_uds::UnixStream;

    use super::*;

    pub trait UnixStreamExt {
        fn pid(&self) -> io::Result<Pid>;
    }

    impl UnixStreamExt for UnixStream {
        fn pid(&self) -> io::Result<Pid> {
            get_pid(self)
        }
    }

    pub fn get_pid(sock: &UnixStream) -> io::Result<Pid> {
        let raw_fd = sock.as_raw_fd();
        let mut ucred = ucred {
            pid: 0,
            uid: 0,
            gid: 0,
        };
        let ucred_size = mem::size_of::<ucred>();

        // These paranoid checks should be optimized-out
        assert!(mem::size_of::<u32>() <= mem::size_of::<usize>());
        assert!(ucred_size <= u32::max_value() as usize);

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
