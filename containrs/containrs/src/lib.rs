#![deny(future_incompatible, rust_2018_idioms)]

mod auth;
mod client;
mod error;
mod paginate;
mod util;

pub mod flows;

pub use auth::Credentials;
pub use client::Client;
pub use error::{Error, ErrorKind, Result};
pub use paginate::Paginate;
