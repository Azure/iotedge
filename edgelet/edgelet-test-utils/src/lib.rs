// Copyright (c) Microsoft. All rights reserved.

pub mod route;
pub mod runtime;

mod json_connector;
pub use json_connector::JsonConnector;

mod settings;
pub use settings::Settings;

/// Generic test error. Most users of ModuleRuntime don't act on the error other
/// than passing it up the call stack, so it's fine to return any error.
fn test_error() -> anyhow::Error {
    anyhow::Error::msg("test error")
}
