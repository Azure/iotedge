#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::{broker, config, start_server};

#[cfg(not(feature = "edgehub"))]
mod generic;

#[cfg(not(feature = "edgehub"))]
pub use generic::{broker, config, start_server};
