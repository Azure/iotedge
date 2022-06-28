// Copyright (c) Microsoft. All rights reserved.

use std::borrow::Cow;

use http_common::server::Error;

pub const FORBIDDEN: Error = Error {
    status_code: http::StatusCode::FORBIDDEN,
    message: Cow::Borrowed("forbidden"),
};

pub const fn bad_request(message: &'static str) -> Error {
    Error {
        status_code: http::StatusCode::BAD_REQUEST,
        message: Cow::Borrowed(message),
    }
}

/// Produce an HTTP error response provided a runtime-dependent error.
#[allow(clippy::module_name_repetitions)]
pub fn runtime_error<M>(_runtime: &M, error: &anyhow::Error) -> http_common::server::Error
where
    M: edgelet_core::ModuleRuntime,
{
    http_common::server::Error {
        status_code: <M as edgelet_core::ModuleRuntime>::error_code(error),
        message: Cow::Owned(error.to_string()),
    }
}

/// Produce a generic internal server error.
#[allow(clippy::module_name_repetitions, clippy::needless_pass_by_value)]
pub fn server_error(error: impl ToString) -> Error {
    Error {
        status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
        message: error.to_string().into(),
    }
}
