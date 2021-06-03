// Copyright (c) Microsoft. All rights reserved.

#[macro_export]
macro_rules! router {
    ($($method:ident $ver:ident $runtime:ident $policy:expr => $path:expr => $handler:expr),+ $(,)*) => ({
        Router::from(
            $crate::route::RegexRoutesBuilder::default()
            $(.$method(Version::$ver, $path, Authentication::new(Authorization::new($handler, $policy), $policy, $runtime.clone())))*
            .finish()
        )
    });
}
