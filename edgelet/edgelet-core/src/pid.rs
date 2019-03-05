// Copyright (c) Microsoft. All rights reserved.

use std::cmp;
use std::fmt;

use serde_derive::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Deserialize, Serialize)]
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
            Pid::Any => match *other {
                Pid::None => false,
                _ => true,
            },
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
    use super::*;

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
