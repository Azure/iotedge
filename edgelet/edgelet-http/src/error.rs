// Copyright (c) Microsoft. All rights reserved.

pub fn bad_request(
    message: impl std::convert::Into<std::borrow::Cow<'static, str>>,
) -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::BAD_REQUEST,
        message: message.into(),
    }
}

// This function is only used by auth, so it doesn't need to be externally callable.
pub(crate) fn forbidden() -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::FORBIDDEN,
        message: "forbidden".into(),
    }
}

pub fn not_found(
    message: impl std::convert::Into<std::borrow::Cow<'static, str>>,
) -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::NOT_FOUND,
        message: message.into(),
    }
}

#[allow(clippy::module_name_repetitions)]
pub fn server_error(
    message: impl std::convert::Into<std::borrow::Cow<'static, str>>,
) -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
        message: message.into(),
    }
}
