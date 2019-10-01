#![deny(future_incompatible, rust_2018_idioms)]

mod auth;
mod blob;
mod client;
mod error;
mod paginate;

pub mod flows;

pub use auth::Credentials;
pub use blob::Blob;
pub use client::Client;
pub use error::{Error, ErrorKind, Result};
pub use paginate::Paginate;
