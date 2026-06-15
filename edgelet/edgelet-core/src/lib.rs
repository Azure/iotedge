// Copyright (c) Microsoft. All rights reserved.

pub mod error;
pub mod module;

mod parse_since;
mod virtualization;

use std::sync::LazyLock;

pub use error::Error;
pub use module::{
    DiskInfo, LogOptions, LogTail, Module, ModuleAction, ModuleOperation, ModuleRegistry,
    ModuleRuntime, ModuleRuntimeErrorReason, ModuleRuntimeState, ModuleStatus, ProvisioningInfo,
    RegistryOperation, RuntimeOperation, SystemInfo, SystemResources,
};
pub use parse_since::parse_since;

use std::path::{Path, PathBuf};

use url::Url;

static VERSION: LazyLock<&'static str> = LazyLock::new(|| {
    option_env!("VERSION").unwrap_or_else(|| include_str!("../../version.txt").trim())
});
static VERSION_WITH_SOURCE_VERSION: LazyLock<String> = LazyLock::new(|| {
    option_env!("VERSION")
        .map(|version| {
            option_env!("BUILD_SOURCEVERSION")
                .map(|sha| format!("{version} ({sha})"))
                .unwrap_or_else(|| version.to_string())
        })
        .unwrap_or_else(|| include_str!("../../version.txt").trim().to_string())
});

pub fn version() -> &'static str {
    &VERSION
}

pub fn version_with_source_version() -> String {
    VERSION_WITH_SOURCE_VERSION.to_string()
}

pub trait UrlExt {
    fn to_uds_file_path(&self) -> Result<PathBuf, Error>;
    fn to_base_path(&self) -> Result<PathBuf, Error>;
}

impl UrlExt for Url {
    fn to_uds_file_path(&self) -> Result<PathBuf, Error> {
        debug_assert_eq!(self.scheme(), UNIX_SCHEME);

        Ok(Path::new(self.path()).to_path_buf())
    }

    fn to_base_path(&self) -> Result<PathBuf, Error> {
        match self.scheme() {
            "unix" => Ok(self.to_uds_file_path()?),
            _ => Ok(self.as_str().into()),
        }
    }
}

pub const UNIX_SCHEME: &str = "unix";

#[derive(Debug, Eq, PartialEq)]
pub enum WatchdogAction {
    EdgeCaRenewal,
    Reprovision,
    Signal,
}

impl std::fmt::Display for WatchdogAction {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            WatchdogAction::EdgeCaRenewal => f.write_str("Edge CA was renewed; restarting modules"),
            WatchdogAction::Reprovision => f.write_str("Edge daemon will reprovision and restart"),
            WatchdogAction::Signal => f.write_str("Received signal; shutting down"),
        }
    }
}
