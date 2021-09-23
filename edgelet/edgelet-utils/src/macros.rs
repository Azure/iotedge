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
/// error if it doesn't.
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
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use crate::error::{Error, ErrorKind};

    macro_rules! check_ok {
        ($expected:expr, $f:block) => {
            let result: Result<_, Error> = async { $f }.await;
            assert_eq!($expected, result.unwrap());
        };

        ($expected:expr, $f:tt) => {
            let result: Result<_, Error> = $f();
            assert_eq!($expected, result.unwrap());
        };
    }

    macro_rules! check_err {
        ($expected:ident, $f:block) => {
            let result: Result<_, Error> = async { $f }.await;
            let err = result.expect_err("expected error but found value");

            match err.kind() {
                ErrorKind::$expected(..) => (),
                _ => panic!("Unexpected error encountered {:#?}", err),
            }
        };

        ($expected:ident, $f:tt) => {
            let result: Result<_, Error> = $f();
            let err = result.expect_err("expected error but found value");

            match err.kind() {
                ErrorKind::$expected(..) => (),
                _ => panic!("Unexpected error encountered {:#?}", err),
            }
        };
    }

    #[test]
    fn validate_ensure() {
        check_ok!(15, (|| Ok(ensure!(15, 15 > 10))));

        check_err!(Argument, (|| Ok(ensure!(10, 9 > 10))));
    }

    #[tokio::test]
    async fn validate_ensure_async() {
        check_ok!(15, { Ok(ensure!(15, 15 > 10)) });

        check_err!(Argument, { Ok(ensure!(10, 9 > 10)) });
    }

    #[test]
    fn validate_ensure_range() {
        check_ok!(10, (|| Ok(ensure_range!(10, 5, 15))));
        check_ok!(15, (|| Ok(ensure_range!(15, 5, 15))));

        check_err!(ArgumentOutOfRange, (|| Ok(ensure_range!(3, 5, 15))));
        check_err!(ArgumentOutOfRange, (|| Ok(ensure_range!(5, 5, 15))));
        check_err!(ArgumentOutOfRange, (|| Ok(ensure_range!(25, 5, 15))));
    }

    #[tokio::test]
    async fn validate_ensure_range_async() {
        check_ok!(10, { Ok(ensure_range!(10, 5, 15)) });
        check_ok!(15, { Ok(ensure_range!(15, 5, 15)) });

        check_err!(ArgumentOutOfRange, { Ok(ensure_range!(3, 5, 15)) });
        check_err!(ArgumentOutOfRange, { Ok(ensure_range!(5, 5, 15)) });
        check_err!(ArgumentOutOfRange, { Ok(ensure_range!(25, 5, 15)) });
    }

    #[test]
    fn validate_ensure_greater() {
        check_ok!(10, (|| Ok(ensure_greater!(10, 5))));

        check_err!(ArgumentTooLow, (|| Ok(ensure_greater!(10, 25))));
    }

    #[tokio::test]
    async fn validate_ensure_greater_async() {
        check_ok!(10, { Ok(ensure_greater!(10, 5)) });

        check_err!(ArgumentTooLow, { Ok(ensure_greater!(10, 25)) });
    }

    #[test]
    fn validate_ensure_not_empty() {
        check_err!(ArgumentEmpty, (|| Ok(ensure_not_empty!(""))));
        check_err!(ArgumentEmpty, (|| Ok(ensure_not_empty!("", "empty str"))));
        check_err!(
            ArgumentEmpty,
            (|| Ok(ensure_not_empty!("    ", "white space str")))
        );

        check_err!(ArgumentEmpty, (|| Ok(ensure_not_empty!("".to_string()))));
        check_err!(
            ArgumentEmpty,
            (|| Ok(ensure_not_empty!("".to_string(), "empty String")))
        );
        check_err!(
            ArgumentEmpty,
            (|| Ok(ensure_not_empty!("    ".to_string(), "white space String")))
        );

        check_ok!("  not empty  ", (|| Ok(ensure_not_empty!("  not empty  "))));
        check_ok!(
            "  not empty  ".to_string(),
            (|| Ok(ensure_not_empty!("  not empty  ".to_string())))
        );
    }

    #[tokio::test]
    async fn validate_ensure_not_empty_async() {
        check_err!(ArgumentEmpty, { Ok(ensure_not_empty!("")) });
        check_err!(ArgumentEmpty, { Ok(ensure_not_empty!("", "empty str")) });
        check_err!(ArgumentEmpty, {
            Ok(ensure_not_empty!("    ", "white space str"))
        });

        check_err!(ArgumentEmpty, { Ok(ensure_not_empty!("".to_string())) });
        check_err!(ArgumentEmpty, {
            Ok(ensure_not_empty!("".to_string(), "empty String"))
        });
        check_err!(ArgumentEmpty, {
            Ok(ensure_not_empty!("    ".to_string(), "white space String"))
        });

        check_ok!("  not empty  ", { Ok(ensure_not_empty!("  not empty  ")) });
        check_ok!("  not empty  ".to_string(), {
            Ok(ensure_not_empty!("  not empty  ".to_string()))
        });
    }
}
