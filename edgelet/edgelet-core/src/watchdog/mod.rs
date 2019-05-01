// Copyright (c) Microsoft. All rights reserved.

#[cfg(feature = "runtime-docker")]
mod docker;

#[cfg(feature = "runtime-docker")]
pub use self::docker::{start_watchdog, Watchdog};

#[cfg(feature = "runtime-kubernetes")]
mod kubernetes;

#[cfg(feature = "runtime-kubernetes")]
pub use self::kubernetes::{start_watchdog, Watchdog};
