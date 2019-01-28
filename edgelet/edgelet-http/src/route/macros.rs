// Copyright (c) Microsoft. All rights reserved.

/// Create and populate a router. The following code,
///
/// ```ignore
/// let router = router!(
///     get "2018-06-28", "/" => index_handler,
///     get "2018-06-28", "/hello" => hello_handler,
/// );
/// ```
///
/// is equivalent to:
///
/// ```ignore
/// let router = Router::from(
///     RegexRoutesBuilder::default()
///         .get("2018-06-28", "/", index_handler)
///         .get("2018-06-28", "/hello", hello_handler)
///         .finish()
/// );
/// ```
///
/// The method names must be lowercase and must be one of:
///
/// `get`, `post`, `put` and `delete`
#[macro_export]
macro_rules! router {
    ($($method:ident $ver:ident, $glob:expr => $handler:expr),+ $(,)*) => ({
        Router::from(
            $crate::route::RegexRoutesBuilder::default()
            $(.$method(Version::$ver, $glob, $handler))*
            .finish()
        )
    });
}
