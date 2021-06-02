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

mod client_io;

pub use client_io::{
    AuthenticationSettings, ClientIoSource, CredentialProviderSettings, Credentials,
    SasTokenSource, TcpConnection, TrustBundleSource,
};
