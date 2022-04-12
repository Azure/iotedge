// Copyright (c) Microsoft. All rights reserved.

//! Utility macros
//!
//! This module contains helper macros for implementing argument validation in
//! functions. In order to be able to use the macros in this crate, you _must_
//! have an implementation of `std::convert::From` that converts from the `Error`
//! type defined in `edgelet-utils` to the error type being returned from the
//! function where the macro is being used.
//!
//! # Examples
//!
//! <h3>Custom error types with implicit conversion:</h3>
//!
//! ```
//! #[macro_use] extern crate edgelet_utils;
//! extern crate failure;
//!
//! use failure::Fail;
//!
//! use edgelet_utils::Error as UtilsError;
//!
//! struct BooError {
//!     inner: Box<dyn Fail>,
//! }
//!
//! impl From<UtilsError> for BooError {
//!     fn from(err: UtilsError) -> Self {
//!         BooError { inner: Box::new(err) }
//!     }
//! }
//!
//! struct TheThing {
//!     val: i32,
//! }
//!
//! impl TheThing {
//!     fn new(val: i32) -> Result<TheThing, BooError> {
//!         Ok(TheThing {
//!             val: ensure_range!(val, 10, 100),
//!         })
//!     }
//! }
//!
//! let _thing = TheThing::new(5);
//! ```

use crate::error::Error;

#[inline]
pub fn ensure_not_empty(value: &str) -> Result<(), Error> {
    if value.trim().is_empty() {
        return Err(Error::ArgumentEmpty(String::new()));
    }

    Ok(())
}
