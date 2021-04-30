#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::init;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
mod generic;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
pub use generic::init;

#[cfg(feature = "edgehub")]
mod format;

#[cfg(feature = "edgehub")]
use format::Format;
