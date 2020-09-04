use scopeguard::defer;
use libc::{mode_t, umask};

pub fn with_umask<F: FnOnce() -> T, T>(mask: mode_t, task: F) -> T {
    let tmp = unsafe { umask(mask) };
    defer!(unsafe { umask(tmp); });
    task()
}