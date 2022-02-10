// Copyright (c) Microsoft. All rights reserved.

pub mod route;
pub mod runtime;

mod settings;
pub use settings::Settings;

/// Generic test error. Most users of ModuleRuntime don't act on the error other
/// than passing it up the call stack, so it's fine to return any error.
fn test_error() -> std::io::Error {
    std::io::Error::new(std::io::ErrorKind::Other, "test error")
}
