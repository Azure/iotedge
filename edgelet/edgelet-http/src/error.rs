// Copyright (c) Microsoft. All rights reserved.

pub fn bad_request(
    message: impl std::convert::Into<std::borrow::Cow<'static, str>>,
) -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::BAD_REQUEST,
        message: message.into(),
    }
}

pub fn server_error(
    message: impl std::convert::Into<std::borrow::Cow<'static, str>>,
) -> http_common::server::Error {
    http_common::server::Error {
        status_code: http::StatusCode::INTERNAL_SERVER_ERROR,
        message: message.into(),
    }
}
