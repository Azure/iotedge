#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

mod bridge;
pub mod client;
mod config_update;
pub mod controller;
mod messages;
mod persist;
pub mod pump;
pub mod settings;
pub mod upstream;

pub use crate::controller::{BridgeController, BridgeControllerHandle, Error};

pub use crate::config_update::BridgeControllerUpdate;
