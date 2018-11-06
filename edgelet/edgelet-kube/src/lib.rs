// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

extern crate edgelet_core;
extern crate edgelet_docker;
extern crate edgelet_utils;
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate kube_client;

mod error;
mod module;
mod runtime;

pub use module::KubeModule;
pub use runtime::KubeModuleRuntime;
