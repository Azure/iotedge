// Copyright (c) Microsoft. All rights reserved.

use futures::Future;

use crate::AuthId;

pub trait Authenticator {
    type Error;
    type Request;
    type AuthenticateFuture: Future<Item = AuthId, Error = Self::Error> + Send;

    fn authenticate(&self, req: &Self::Request) -> Self::AuthenticateFuture;
}
