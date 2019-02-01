// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy::all, clippy::pedantic))]
#![cfg_attr(
    feature = "cargo-clippy",
    allow(clippy::stutter, clippy::use_self, clippy::new_ret_no_self)
)]

mod constants;
mod convert;
mod error;
mod module;
mod runtime;

pub use self::module::KubeModule;
pub use self::runtime::KubeModuleRuntime;
