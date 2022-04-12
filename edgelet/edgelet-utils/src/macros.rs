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

pub fn ensure_not_empty(value: &str) -> Result<(), Error> {
    if value.trim().is_empty() {
        return Err(Error::ArgumentEmpty(String::new()));
    }

    Ok(())
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use crate::error::Error;

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
                Error::$expected(..) => (),
                _ => panic!("Unexpected error encountered {:#?}", err),
            }
        };

        ($expected:ident, $f:tt) => {
            let result: Result<_, Error> = $f();
            let err = result.expect_err("expected error but found value");

            match err.kind() {
                Error::$expected(..) => (),
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
