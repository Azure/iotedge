#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::init;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
mod generic;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
pub use generic::init;

// TODO fix this
// mod format;
// use format::Format;
