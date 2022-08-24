// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

// mod client;
mod error;
mod image_prune_data;
mod module;
mod runtime;

pub use error::Error;
pub use image_prune_data::ImagePruneData;
pub use module::{DockerModule, MODULE_TYPE};
pub use runtime::{init_client, DockerModuleRuntime};

use tokio::sync::mpsc::UnboundedSender;

use edgelet_core::{ModuleAction, ModuleRuntime};
use edgelet_settings::RuntimeSettings;

#[async_trait::async_trait]
pub trait MakeModuleRuntime {
    type Config: Clone + Send;
    type Settings: RuntimeSettings<ModuleConfig = Self::Config>;
    type ModuleRuntime: ModuleRuntime<Config = Self::Config>;

    async fn make_runtime(
        settings: &Self::Settings,
        create_socket_channel: UnboundedSender<ModuleAction>,
        image_use_data: ImagePruneData,
    ) -> anyhow::Result<Self::ModuleRuntime>;
}
