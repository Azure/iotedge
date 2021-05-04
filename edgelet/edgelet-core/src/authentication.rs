// Copyright (c) Microsoft. All rights reserved.

use crate::AuthId;

#[async_trait::async_trait]
pub trait Authenticator {
    type Error;
    type Request;

    async fn authenticate(&self, req: &Self::Request) -> Result<AuthId, Self::Error>;
}
