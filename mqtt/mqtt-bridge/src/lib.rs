#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc,
    clippy::default_trait_access // TODO remove when mockall fix released for https://github.com/asomers/mockall/issues/221
)]

mod bridge;
pub mod client;
pub mod controller;
mod messages;
mod persist;
pub mod pump;
pub mod settings;
mod token_source;
pub mod upstream;

pub use crate::controller::{
    BridgeController, BridgeControllerHandle, BridgeControllerUpdate, Error,
};
