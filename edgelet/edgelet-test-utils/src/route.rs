// Copyright (c) Microsoft. All rights reserved.

// Macros to test from_uri that allow a variable number of arguments.
// Unspecified arguments use a default value.
#[macro_export]
macro_rules! test_route_ok {
    ($path:expr) => {
        edgelet_test_utils::test_route!($path)
            .expect("valid route wasn't parsed")
    };

    ($path:expr, $(($key:expr, $value:expr)),+) => {
        edgelet_test_utils::test_route!($path, $(($key, $value)),+)
            .expect("valid route wasn't parsed")
    }
}

#[macro_export]
macro_rules! test_route_err {
    ($path:expr) => {
        assert!(edgelet_test_utils::test_route!($path).is_none())
    };
}

#[macro_export]
macro_rules! test_route {
    ($path:expr) => {{
        let route: Option<super::Route<edgelet_test_utils::runtime::Runtime>> =
            http_common::server::Route::from_uri(
                &crate::Service::new(edgelet_test_utils::runtime::Runtime::default()),
                $path,
                &vec![],
                &edgelet_test_utils::route::extensions(),
            );

        route
    }};

    ($path:expr, $(($key:expr, $value:expr)),+) => {{
        let mut query = vec![];

        $(
            query.push((std::borrow::Cow::from($key), std::borrow::Cow::from($value)));
        )+

        let route: Option<super::Route<edgelet_test_utils::runtime::Runtime>> =
            http_common::server::Route::from_uri(
                &crate::Service::new(edgelet_test_utils::runtime::Runtime::default()),
                $path,
                query.as_slice(),
                &edgelet_test_utils::route::extensions(),
            );

        route
    }}
}

// Constructs the http::Extensions containing this process ID, similar to what
// http-common does.
pub fn extensions() -> http::Extensions {
    let pid = nix::unistd::getpid();

    let mut extensions = http::Extensions::new();
    assert!(extensions.insert(Some(pid.as_raw())).is_none());

    extensions
}
