#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::{broker, server};

#[cfg(not(feature = "edgehub"))]
mod generic;

#[cfg(not(feature = "edgehub"))]
pub use generic::{broker, server};
