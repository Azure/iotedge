// Copyright (c) Microsoft. All rights reserved.

#[cfg(feature = "runtime-docker")]
mod docker;

#[cfg(feature = "runtime-docker")]
pub use self::docker::Authorization;

#[cfg(feature = "runtime-kubernetes")]
mod kubernetes;

#[cfg(feature = "runtime-kubernetes")]
pub use self::kubernetes::Authorization;

pub enum Policy {
    Anonymous,
    Caller,
    Module(&'static str),
}
