// Copyright (c) Microsoft. All rights reserved.

// Macros to test from_uri that allow a variable number of arguments.
// Unspecified arguments use a default value.
#[macro_export]
macro_rules! test_route_ok {
    ($path:literal) => {
        edgelet_test_utils::test_route!($path).expect("valid route wasn't parsed")
    };
}

#[macro_export]
macro_rules! test_route_err {
    ($path:literal) => {
        assert!(edgelet_test_utils::test_route!($path).is_none())
    };
}

#[macro_export]
macro_rules! test_route {
    ($path:literal) => {{
        let route: Option<super::Route<edgelet_test_utils::runtime::Runtime>> =
            http_common::server::Route::from_uri(
                &crate::Service::new(edgelet_test_utils::runtime::Runtime {}),
                $path,
                &vec![],
                &http::Extensions::new(),
            );

        route
    }};
}
