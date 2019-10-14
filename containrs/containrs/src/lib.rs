#![deny(future_incompatible, rust_2018_idioms)]

mod auth;
mod blob;
mod client;
mod error;
mod paginate;

pub use auth::Credentials;
pub use blob::Blob;
pub use client::Client;
pub use error::{Error, Result};
pub use paginate::Paginate;
