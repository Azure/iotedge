// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::Future;

pub trait Identity {
    fn module_id(&self) -> &str;
    fn managed_by(&self) -> &str;
    fn generation_id(&self) -> &str;
}

pub struct IdentitySpec {
    module_id: String,
}

impl IdentitySpec {
    pub fn new(module_id: &str) -> IdentitySpec {
        IdentitySpec {
            module_id: module_id.to_string(),
        }
    }

    pub fn module_id(&self) -> &str {
        &self.module_id
    }
}

pub trait IdentityManager {
    type Identity: Identity;
    type Error: Fail;
    type CreateFuture: Future<Item = Self::Identity, Error = Self::Error>;
    type GetFuture: Future<Item = Vec<Self::Identity>, Error = Self::Error>;
    type DeleteFuture: Future<Item = (), Error = Self::Error>;

    fn create(&mut self, id: IdentitySpec) -> Self::CreateFuture;
    fn get(&self) -> Self::GetFuture;
    fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture;
}
