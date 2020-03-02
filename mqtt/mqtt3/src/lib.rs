/*!
 * This crate contains an implementation of an MQTT client.
 */

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
	clippy::cognitive_complexity,
	clippy::default_trait_access,
	clippy::large_enum_variant,
	clippy::let_unit_value,
	clippy::missing_errors_doc,
	clippy::module_name_repetitions,
	clippy::must_use_candidate,
	clippy::pub_enum_variant_names,
	clippy::similar_names,
	clippy::single_match_else,
	clippy::too_many_arguments,
	clippy::too_many_lines,
	clippy::use_self,
)]

mod client;
pub use client::{
	Client,
	Error,
	Event,
	IoSource,
	PublishError,
	PublishHandle,
	ReceivedPublication,
	ShutdownError,
	ShutdownHandle,
	SubscriptionUpdateEvent,
	UpdateSubscriptionError,
	UpdateSubscriptionHandle,
};

mod logging_framed;

pub mod proto;
