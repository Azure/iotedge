// Copyright (c) Microsoft. All rights reserved.

mod create;
mod delete;
mod list;
mod update;

pub use self::create::CreateIdentity;
pub use self::delete::DeleteIdentity;
pub use self::list::ListIdentities;
pub use self::update::UpdateIdentity;

#[cfg(test)]
mod tests {
    use hyper::{Body, Response, StatusCode};
    use serde_json;

    use edgelet_test_utils::identity::Error;
    use management::models::ErrorResponse;

    use crate::IntoResponse;

    impl IntoResponse for Error {
        fn into_response(self) -> Response<Body> {
            let body = serde_json::to_string(&ErrorResponse::new(self.to_string()))
                .expect("serialization of ErrorResponse failed.");
            Response::builder()
                .status(StatusCode::INTERNAL_SERVER_ERROR)
                .body(body.into())
                .unwrap()
        }
    }
}
