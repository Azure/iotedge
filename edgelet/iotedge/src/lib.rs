// Copyright (c) Microsoft. All rights reserved.

use serde::Deserialize;

mod check;
mod client;
pub mod config;
mod error;
mod list;
mod logs;
mod restart;
mod support_bundle;
mod system;
mod version;

pub use crate::check::{Check, OutputFormat};
pub use crate::client::{MgmtClient, MgmtModule};
pub use crate::error::{Error, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::support_bundle::SupportBundleCommand;
pub use crate::system::System;
pub use crate::version::Version;

#[derive(Debug, Deserialize)]
struct LatestVersions {
    channels: Vec<Channel>,
}

#[derive(Debug, Deserialize)]
struct Channel {
    products: Vec<Product>,
}

#[derive(Debug, Deserialize)]
struct Product {
    id: String,
    components: Vec<Component>,
}

#[derive(Debug, Deserialize)]
struct Component {
    name: String,
    version: String,
}
