// Copyright (c) Microsoft. All rights reserved.

use std::cmp;
use std::fmt;

#[derive(Clone, Debug)]
pub enum Pid {
    None,
    Value(i32),
}

impl fmt::Display for Pid {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            Pid::None => write!(f, "none"),
            Pid::Value(pid) => write!(f, "{}", pid),
        }
    }
}

/// Pids are considered equal when comparing against None.
/// This is the logic required for using the pid to perform
/// access control. By default, if a pid isn't present, then
/// it should be considered equal to all other pids.
impl cmp::PartialEq for Pid {
    fn eq(&self, other: &Pid) -> bool {
        match *self {
            Pid::None => true,
            Pid::Value(pid1) => match *other {
                Pid::None => true,
                Pid::Value(pid2) => pid1 == pid2,
            },
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_eq() {
        assert_eq!(Pid::None, Pid::Value(42));
        assert_eq!(Pid::None, Pid::None);
        assert_eq!(Pid::Value(42), Pid::Value(42));
        assert_ne!(Pid::Value(0), Pid::Value(42));
    }
}
