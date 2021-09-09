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
                &Vec::new(),
                &edgelet_test_utils::route::extensions(),
            );

        route
    }};

    ($path:expr, $(($key:expr, $value:expr)),+) => {{
        let query = vec![
            $(
                (std::borrow::Cow::from($key), std::borrow::Cow::from($value)),
            )+
        ];

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

#[macro_export]
macro_rules! test_auth_agent {
    ($path:expr, $fn:expr) => {
        edgelet_test_utils::test_auth_caller!($path, "edgeAgent", $fn)
    };
}

#[macro_export]
macro_rules! test_auth_caller {
    ($path:expr, $caller:expr, $fn:expr) => {{
        // Caller not in authorized modules: fail.
        let route = edgelet_test_utils::test_route_ok!($path);

        {
            let mut runtime = route.runtime.lock().await;
            runtime.module_auth = std::collections::BTreeMap::new();
        }

        let response = $fn(route).await.unwrap_err();
        assert_eq!(hyper::StatusCode::FORBIDDEN, response.status_code);

        // Process doesn't match caller PID: fail.
        let route = edgelet_test_utils::test_route_ok!($path);

        {
            // PID that doesn't match this process.
            let pid = nix::unistd::getpid().as_raw() + 1;

            let mut runtime = route.runtime.lock().await;
            runtime.module_auth = std::collections::BTreeMap::new();
            runtime.module_auth.insert($caller.to_string(), vec![pid]);
        }

        let response = $fn(route).await.unwrap_err();
        assert_eq!(hyper::StatusCode::FORBIDDEN, response.status_code);

        // Process doesn't match caller name: fail.
        let route = edgelet_test_utils::test_route_ok!($path);

        {
            // PID that matches this process.
            let pid = nix::unistd::getpid().as_raw();

            let mut runtime = route.runtime.lock().await;
            runtime.module_auth = std::collections::BTreeMap::new();
            runtime
                .module_auth
                .insert("otherModule".to_string(), vec![pid]);
        }

        let response = $fn(route).await.unwrap_err();
        assert_eq!(hyper::StatusCode::FORBIDDEN, response.status_code);

        // Process matches caller: succeed.
        let route = edgelet_test_utils::test_route_ok!($path);

        {
            // PID that matches this process.
            let pid = nix::unistd::getpid().as_raw();

            let mut runtime = route.runtime.lock().await;
            runtime.module_auth = std::collections::BTreeMap::new();
            runtime.module_auth.insert($caller.to_string(), vec![pid]);
        }

        let response = $fn(route).await;

        // We don't know the details of the request, so don't know if it will succeed.
        // If the request fails, just check that auth succeeded.
        if let Err(err) = response {
            assert_ne!(hyper::StatusCode::FORBIDDEN, err.status_code);
        }
    }};
}

// Constructs the http::Extensions containing this process ID, similar to what
// http-common does.
pub fn extensions() -> http::Extensions {
    let pid = nix::unistd::getpid();

    let mut extensions = http::Extensions::new();
    assert!(extensions.insert(Some(pid.as_raw())).is_none());

    extensions
}
