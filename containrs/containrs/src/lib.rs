mod auth;
mod client;
pub mod reference;
mod util;

// TODO: convert this to something cleaner
pub type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;

pub use auth::Credentials;
pub use client::*;
