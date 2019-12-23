// Copyright (c) Microsoft. All rights reserved.

//! Utility macros
//!
//! This module contains helper macros for implementing argument validation in
//! functions. In order to be able to use the macros in this crate, you _must_
//! have an implementation of `std::convert::From` that converts from the `Error`
//! type defined in `edgelet-utils` to the error type being returned from the
//! function where the macro is being used.
//!
//! All the input validation macros can be prefixed with the letter `f` when
//! used inside a function that returns a `Box<Future<T, E>>` instead of a
//! `Result<T, E>`.
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
//!     inner: Box<Fail>,
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
//!
//! <h3>Functions that return futures:</h3>
//!
//! ```
//! #[macro_use] extern crate edgelet_utils;
//! extern crate failure;
//! extern crate futures;
//!
//! use failure::Fail;
//! use futures::future;
//! use futures::prelude::*;
//!
//! use edgelet_utils::Error as UtilsError;
//!
//! struct BooError {
//!     inner: Box<Fail>,
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
//! fn do_the_thing(val: i32) -> Box<Future<Item = TheThing, Error = BooError>> {
//!     Box::new(
//!         future::ok(TheThing {
//!             val: fensure_range!(val, 10, 100)
//!         })
//!     )
//! }
//!
//! let _thing_future = do_the_thing(20);
//! ```

use std::fmt;

use failure::{Context, Fail};

use crate::error::ErrorKind;

/// Exits a function early with an `Error`.
///
/// The `bail!` macro provides an easy way to exit a function. It takes an error
/// as an argument and wraps that in an `edgelet_utils::Error` type instance and
/// invokes `From::from` to convert the error to the type that is being returned
/// from the function where it is being called.
///
/// ```
/// #[macro_use] extern crate edgelet_utils;
///
/// use edgelet_utils::{ErrorKind, Error};
///
/// fn do_the_thing(some_expected_value: bool) -> Result<(), Error> {
///     if !some_expected_value {
///         bail!(ErrorKind::Argument("boo".to_string()));
///     } else {
///         Ok(())
///     }
/// }
///
/// fn main() {
///     let result = do_the_thing(false);
///     println!("{:?}", result);
/// }
/// ```
///
/// `bail!(err)` expands to:
///
/// ```ignore
/// return Err(From::from(Error::from(err)))
/// ```
#[macro_export]
macro_rules! bail {
    ($err:expr) => {
        return Err(::std::convert::From::from($crate::Error::from($err)));
    };
}

/// Exits a function early with an `Error`.
///
/// Use this macro when your function
/// returns a `Box<Future<T, E>>` instead of a `Result<T, E>`. For usage
/// examples see documentation for `bail!`.
#[macro_export]
macro_rules! fbail {
    ($err:expr) => {
        return Box::new(::futures::future::err(::std::convert::From::from(
            $crate::Error::from($err),
        )));
    };
}

/// Internal macro used for implementing other validation macros.
///
/// Not to be directly invoked. Use one of the other `ensure*` macros.
#[macro_export]
macro_rules! ensure_impl {
    ($val:expr, $cond:expr, $err:expr, $bail:tt) => {{
        let cond = $cond;
        if cond {
            $val
        } else {
            $bail!($err);
        }
    }};
}

/// Check if a condition evaluates to `true` and call the `bail!` macro with an
/// error if it doesnt.
///
/// # Examples
///
/// ```
/// # #[macro_use] extern crate edgelet_utils;
/// # use edgelet_utils::{ErrorKind, Error};
/// fn do_thing() -> Result<(), Error> {
///    assert_eq!(10, ensure!(10, 10 > 0));
///    Ok(())
/// }
/// # fn main() {
/// #   do_thing().unwrap();
/// # }
/// ```
///
/// ```
/// # #[macro_use] extern crate edgelet_utils;
/// # use edgelet_utils::{ErrorKind, Error};
/// #[derive(Debug)]
/// struct Foo {
///     ival: i32,
///     fval: f32,
/// }
///
/// impl Foo {
///     fn new(ival: i32, fval: f32) -> Result<Foo, Error> {
///         Ok(Foo {
///             ival: ensure!(ival, ival > 0),
///             fval: ensure!(
///                     fval, fval > 10f32,
///                     ErrorKind::Argument("fval too small".to_string())
///                   ),
///         })
///     }
/// }
///
/// fn main() {
///     // prints argument error
///     println!("{:?}", Foo::new(0, 20f32));
///
///     // prints argument error with the message "fval too small"
///     println!("{:?}", Foo::new(5, 5f32));
/// }
/// ```
#[macro_export]
macro_rules! ensure {
    ($val:expr, $cond:expr, $err:expr) => {
        ensure_impl!($val, $cond, $err, bail)
    };
    ($val:expr, $cond:expr) => {
        ensure!($val, $cond, $crate::ErrorKind::Argument("".to_string()));
    };
    ($cond:expr) => {
        ensure!((), $cond);
    };
}

/// Check if a condition evaluates to `true` and call the `fbail!` macro with an
/// error if it doesnt.
///
/// Use this macro when your function returns a `Box<Future<T, E>>` instead of
/// a `Result<T, E>`. For usage examples see documentation for `ensure!`.
#[macro_export]
macro_rules! fensure {
    ($val:expr, $cond:expr, $err:expr) => {
        ensure_impl!($val, $cond, $err, fbail)
    };
    ($val:expr, $cond:expr) => {
        fensure!($val, $cond, $crate::ErrorKind::Argument("".to_string()));
    };
    ($cond:expr) => {
        fensure!((), $cond);
    };
}

/// Internal macro used for implementing other validation macros.
///
/// Not to be directly invoked. Use one of the other `ensure*` macros.
#[macro_export]
macro_rules! ensure_range_impl {
    ($val:expr, $low:expr, $high:expr, $ensure:tt) => {
        match (&$val, &$low, &$high) {
            (val_val, low_val, high_val) => $ensure!(
                *val_val,
                *val_val > *low_val && *val_val <= *high_val,
                $crate::ErrorKind::ArgumentOutOfRange(
                    format!("{}", val_val),
                    format!("{}", low_val),
                    format!("{}", high_val),
                )
            ),
        }
    };
}

/// Check if a value falls within the range (low, high].
///
/// # Examples
///
/// ```
/// # #[macro_use] extern crate edgelet_utils;
/// # use edgelet_utils::Error;
/// struct Foo {
///     ival: i32
/// }
///
/// impl Foo {
///     fn new(ival: i32) -> Result<Foo, Error> {
///         Ok(Foo {
///             ival: ensure_range!(ival, 10, 25),
///         })
///     }
/// }
///
/// # fn main() {}
/// ```
#[macro_export]
macro_rules! ensure_range {
    ($val:expr, $low:expr, $high:expr) => {
        ensure_range_impl!($val, $low, $high, ensure)
    };
}

/// Check if a value falls within the range (low, high].
///
/// Use this macro when your function returns a `Box<Future<T, E>>` instead of
/// a `Result<T, E>`. For usage examples see documentation for `ensure_range!`.
#[macro_export]
macro_rules! fensure_range {
    ($val:expr, $low:expr, $high:expr) => {
        ensure_range_impl!($val, $low, $high, fensure)
    };
}

/// Internal macro used for implementing other validation macros.
///
/// Not to be directly invoked. Use one of the other `ensure*` macros.
#[macro_export]
macro_rules! ensure_greater_impl {
    ($val:expr, $low:expr, $ensure:tt) => {
        match (&$val, &$low) {
            (val_val, low_val) => $ensure!(
                *val_val,
                *val_val > *low_val,
                $crate::ErrorKind::ArgumentTooLow(format!("{}", val_val), format!("{}", low_val))
            ),
        }
    };
}

/// Check if a value is greater than a minimum and bail with an error if it is not.
///
/// # Examples
///
/// ```
/// # #[macro_use] extern crate edgelet_utils;
/// # use edgelet_utils::Error;
/// #[derive(Debug)]
/// struct Foo {
///     val: i32,
/// }
///
/// impl Foo {
///     fn new(val: i32) -> Result<Foo, Error> {
///         Ok(Foo {
///             val: ensure_greater!(val, 10),
///         })
///     }
/// }
///
/// fn main() {
///     // prints ArgumentTooLow error
///     let foo = Foo::new(10);
///     println!("{:?}", foo);
/// }
/// ```
#[macro_export]
macro_rules! ensure_greater {
    ($val:expr, $low:expr) => {
        ensure_greater_impl!($val, $low, ensure)
    };
}

/// Check if a value is greater than a minimum and bail with an error if it is not.
///
/// Use this macro when your function returns a `Box<Future<T, E>>` instead of
/// a `Result<T, E>`. For usage examples see documentation for `ensure_greater!`.
#[macro_export]
macro_rules! fensure_greater {
    ($val:expr, $low:expr) => {
        ensure_greater_impl!($val, $low, fensure)
    };
}

/// Internal macro used for implementing other validation macros.
///
/// Not to be directly invoked. Use one of the other `ensure*` macros.
#[macro_export]
macro_rules! ensure_not_empty_impl {
    ($val:expr, $msg:expr, $ensure:tt) => {
        $ensure!(
            $val,
            !($val.trim().is_empty()),
            $crate::ErrorKind::ArgumentEmpty($msg.to_string())
        )
    };
}

/// Check if a string is empty and bail with an error if it is.
///
/// # Examples
///
/// ```
/// # #[macro_use] extern crate edgelet_utils;
/// # use edgelet_utils::Error;
/// #[derive(Debug)]
/// struct Foo {
///     sval1: String,
///     sval2: String,
/// }
///
/// impl Foo {
///     fn new(sval1: &str, sval2: String) -> Result<Foo, Error> {
///         Ok(Foo {
///             sval1: ensure_not_empty!(sval1.to_string()),
///             sval2: ensure_not_empty!(sval2, "sval2 cannot be empty"),
///         })
///     }
/// }
///
/// # fn main() {
/// # }
/// ```
#[macro_export]
macro_rules! ensure_not_empty {
    ($val:expr, $msg:expr) => {
        ensure_not_empty_impl!($val, $msg, ensure)
    };
    ($val:expr) => {
        ensure_not_empty!($val, "".to_string())
    };
}

/// Check if a string is empty and bail with an error if it is.
///
/// Use this macro when your function returns a `Box<Future<T, E>>` instead of
/// a `Result<T, E>`. For usage examples see documentation for `ensure_not_empty!`.
#[macro_export]
macro_rules! fensure_not_empty {
    ($val:expr, $msg:expr) => {
        ensure_not_empty_impl!($val, $msg, fensure)
    };
    ($val:expr) => {
        fensure_not_empty!($val, "".to_string())
    };
}

pub fn ensure_not_empty_with_context<D, F>(value: &str, context: F) -> Result<(), Context<D>>
where
    D: fmt::Display + Send + Sync,
    F: FnOnce() -> D,
{
    if value.trim().is_empty() {
        return Err(ErrorKind::ArgumentEmpty(String::new()).context(context()));
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use std::fmt::Debug;
    use std::mem;

    use futures::future;
    use futures::prelude::*;

    use crate::error::{Error, ErrorKind};

    fn check_value<T, F>(expected: &T, f: F)
    where
        F: Fn() -> Result<T, Error>,
        T: PartialEq + Debug,
    {
        match f() {
            Ok(actual) => assert_eq!(expected, &actual),
            Err(err) => panic!(format!("{:?}", err)),
        }
    }

    fn check_fvalue<T, F>(expected: &T, f: F)
    where
        F: Fn() -> Box<dyn Future<Item = T, Error = Error>>,
        T: PartialEq + Debug,
    {
        match f().wait() {
            Ok(actual) => assert_eq!(expected, &actual),
            Err(err) => panic!(format!("{:?}", err)),
        }
    }

    fn check_error<T, V, F>(validator: V, f: F)
    where
        F: Fn() -> Result<T, Error>,
        V: Fn(&Error) -> bool,
        T: PartialEq + Debug,
    {
        match f() {
            Ok(v) => panic!(format!("Expected error but found value {:?}", v)),
            Err(err) => {
                if !validator(&err) {
                    panic!(format!("Unexpected error encountered {:?}", err));
                }
            }
        }
    }

    fn check_ferror<T, V, F>(validator: V, f: F)
    where
        F: Fn() -> Box<dyn Future<Item = T, Error = Error>>,
        V: Fn(&Error) -> bool,
        T: PartialEq + Debug,
    {
        match f().wait() {
            Ok(v) => panic!(format!("Expected error but found value {:?}", v)),
            Err(err) => {
                if !validator(&err) {
                    panic!(format!("Unexpected error encountered {:?}", err));
                }
            }
        }
    }

    #[test]
    fn validate_ensure() {
        check_value(&15, || Ok(ensure!(15, 15 > 10)));

        #[allow(clippy::eq_op)]
        check_error(
            |err| {
                mem::discriminant(err.kind())
                    == mem::discriminant(&ErrorKind::Argument("".to_string()))
            },
            || Ok(ensure!(10, 10 > 10)),
        );
    }

    #[test]
    fn validate_fensure() {
        check_fvalue(&15, || Box::new(future::ok(fensure!(15, 15 > 10))));

        #[allow(clippy::eq_op)]
        check_ferror(
            |err| {
                mem::discriminant(err.kind())
                    == mem::discriminant(&ErrorKind::Argument("".to_string()))
            },
            || Box::new(future::ok(fensure!(10, 10 > 10))),
        );
    }

    #[test]
    fn validate_ensure_range() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentOutOfRange(
                    "".to_string(),
                    "".to_string(),
                    "".to_string(),
                ))
        });

        check_value(&10, || Ok(ensure_range!(10, 5, 15)));
        check_value(&15, || Ok(ensure_range!(15, 5, 15)));
        check_error(validator.as_ref(), || Ok(ensure_range!(3, 5, 15)));
        check_error(validator.as_ref(), || Ok(ensure_range!(5, 5, 15)));
        check_error(validator.as_ref(), || Ok(ensure_range!(25, 5, 15)));
    }

    #[test]
    fn validate_fensure_range() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentOutOfRange(
                    "".to_string(),
                    "".to_string(),
                    "".to_string(),
                ))
        });

        check_fvalue(&10, || Box::new(future::ok(fensure_range!(10, 5, 15))));
        check_fvalue(&15, || Box::new(future::ok(fensure_range!(15, 5, 15))));
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_range!(3, 5, 15)))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_range!(5, 5, 15)))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_range!(25, 5, 15)))
        });
    }

    #[test]
    fn validate_ensure_greater() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentTooLow("".to_string(), "".to_string()))
        });

        check_value(&10, || Ok(ensure_greater!(10, 5)));
        check_error(validator.as_ref(), || Ok(ensure_greater!(10, 25)));
    }

    #[test]
    fn validate_fensure_greater() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentTooLow("".to_string(), "".to_string()))
        });

        check_fvalue(&10, || Box::new(future::ok(fensure_greater!(10, 5))));
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_greater!(10, 25)))
        });
    }

    #[test]
    fn validate_ensure_not_empty() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentEmpty("".to_string()))
        });

        check_error(validator.as_ref(), || Ok(ensure_not_empty!("")));
        check_error(validator.as_ref(), || {
            Ok(ensure_not_empty!("", "empty str"))
        });
        check_error(validator.as_ref(), || {
            Ok(ensure_not_empty!("    ", "white space str"))
        });
        check_error(validator.as_ref(), || Ok(ensure_not_empty!("".to_string())));
        check_error(validator.as_ref(), || {
            Ok(ensure_not_empty!("".to_string(), "empty String"))
        });
        check_error(validator.as_ref(), || {
            Ok(ensure_not_empty!("    ".to_string(), "white space String"))
        });
        check_value(&"  not empty  ", || Ok(ensure_not_empty!("  not empty  ")));
        check_value(&"  not empty  ".to_string(), || {
            Ok(ensure_not_empty!("  not empty  ".to_string()))
        });
    }

    #[test]
    fn validate_fensure_not_empty() {
        let validator: Box<dyn Fn(&Error) -> bool> = Box::new(|err| {
            mem::discriminant(err.kind())
                == mem::discriminant(&ErrorKind::ArgumentEmpty("".to_string()))
        });

        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!("")))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!("", "empty str")))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!("    ", "white space str")))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!("".to_string())))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!(
                "".to_string(),
                "empty String"
            )))
        });
        check_ferror(validator.as_ref(), || {
            Box::new(future::ok(fensure_not_empty!(
                "    ".to_string(),
                "white space String"
            )))
        });
        check_fvalue(&"  not empty  ", || {
            Box::new(future::ok(fensure_not_empty!("  not empty  ")))
        });
        check_fvalue(&"  not empty  ".to_string(), || {
            Box::new(future::ok(fensure_not_empty!("  not empty  ".to_string())))
        });
    }
}
