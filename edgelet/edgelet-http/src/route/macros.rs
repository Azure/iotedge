// Copyright (c) Microsoft. All rights reserved.

/// Create and populate a router. The following code,
///
/// ```ignore
/// let router = router!(
///     get "/" => index_handler,
///     get "/hello" => hello_handler,
/// );
/// ```
///
/// is equivalent to:
///
/// ```ignore
/// let router = Router::from(
///     RegexRoutesBuilder::default()
///         .get("/", index_handler)
///         .get("/hello", hello_handler)
///         .finish()
/// );
/// ```
///
/// The method names must be lowercase and must be one of:
///
/// `get`, `post`, `put` and `delete`
#[macro_export]
macro_rules! router {
    ($($method:ident $glob:expr => $handler:expr),+ $(,)*) => ({
        Router::from(
            $crate::route::RegexRoutesBuilder::default()
            $(.$method($glob, $handler))*
            .finish()
        )
    });
}
