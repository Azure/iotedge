// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::too_many_arguments,
    clippy::use_self
)]

mod constants;
mod convert;
mod error;
mod module;
mod runtime;

pub use self::module::KubeModule;
pub use self::runtime::KubeModuleRuntime;
