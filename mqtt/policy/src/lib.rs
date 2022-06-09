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

mod core;
mod errors;
mod matcher;
mod substituter;
mod validator;

pub use crate::core::{Decision, Effect, Policy, Request};
pub use crate::core::{PolicyBuilder, PolicyDefinition, Statement};
pub use crate::errors::{Error, Result};
pub use crate::matcher::{DefaultResourceMatcher, ResourceMatcher};
pub use crate::substituter::{DefaultSubstituter, Substituter};
pub use crate::validator::{DefaultValidator, PolicyValidator};
