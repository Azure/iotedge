// Copyright (c) Microsoft. All rights reserved.

use crate::AuthId;
use futures::Future;

pub trait Authenticator {
    type Error;
    type Request;
    type AuthenticateFuture: Future<Item = AuthId, Error = Self::Error> + Send;

    fn authenticate(&self, req: &Self::Request) -> Self::AuthenticateFuture;
}
