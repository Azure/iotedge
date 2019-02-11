// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::stutter, clippy::use_self, clippy::too_many_arguments)]

mod constants;
mod convert;
mod error;
mod module;
mod runtime;

pub use self::module::KubeModule;
pub use self::runtime::KubeModuleRuntime;
