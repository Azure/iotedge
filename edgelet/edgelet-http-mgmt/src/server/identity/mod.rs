// Copyright (c) Microsoft. All rights reserved.

mod create;
mod delete;
mod list;

pub use self::create::CreateIdentity;
pub use self::delete::DeleteIdentity;
pub use self::list::ListIdentities;

#[cfg(test)]
mod tests {
    use futures::future::{self, FutureResult};
    use http::{Response, StatusCode};
    use hyper::Body;
    use serde_json;

    use edgelet_core::{Identity, IdentityManager, IdentitySpec};
    use IntoResponse;
    use management::models::ErrorResponse;

    #[derive(Clone, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,

        #[fail(display = "Module not found")]
        ModuleNotFound,
    }

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

    #[derive(Clone, Deserialize, Serialize)]
    pub struct TestIdentity {
        module_id: String,
        managed_by: String,
        generation_id: String,
    }

    impl TestIdentity {
        pub fn new(module_id: &str, managed_by: &str, generation_id: &str) -> TestIdentity {
            TestIdentity {
                module_id: module_id.to_string(),
                managed_by: managed_by.to_string(),
                generation_id: generation_id.to_string(),
            }
        }
    }

    impl Identity for TestIdentity {
        fn module_id(&self) -> &str {
            &self.module_id
        }

        fn managed_by(&self) -> &str {
            &self.managed_by
        }

        fn generation_id(&self) -> &str {
            &self.generation_id
        }
    }

    #[derive(Clone)]
    pub struct TestIdentityManager {
        identities: Vec<TestIdentity>,
        gen_id_sentinel: u32,
        fail_get: bool,
        fail_create: bool,
    }

    impl TestIdentityManager {
        pub fn new(identities: Vec<TestIdentity>) -> TestIdentityManager {
            TestIdentityManager {
                identities,
                gen_id_sentinel: 0,
                fail_get: false,
                fail_create: false,
            }
        }

        pub fn with_fail_get(mut self, fail_get: bool) -> TestIdentityManager {
            self.fail_get = fail_get;
            self
        }

        pub fn with_fail_create(mut self, fail_create: bool) -> TestIdentityManager {
            self.fail_create = fail_create;
            self
        }
    }

    impl IdentityManager for TestIdentityManager {
        type Identity = TestIdentity;
        type Error = Error;
        type CreateFuture = FutureResult<Self::Identity, Self::Error>;
        type UpdateFuture = FutureResult<Self::Identity, Self::Error>;
        type GetFuture = FutureResult<Vec<Self::Identity>, Self::Error>;
        type DeleteFuture = FutureResult<(), Self::Error>;

        fn create(&mut self, id: IdentitySpec) -> Self::CreateFuture {
            if self.fail_create {
                future::err(Error::General)
            } else {
                self.gen_id_sentinel = self.gen_id_sentinel + 1;
                let id = TestIdentity::new(
                    id.module_id(),
                    "iotedge",
                    &format!("{}", self.gen_id_sentinel),
                );
                self.identities.push(id.clone());

                future::ok(id)
            }
        }

        fn update(&mut self, id: IdentitySpec) -> Self::UpdateFuture {
            if self.fail_create {
                future::err(Error::General)
            } else {
                self.gen_id_sentinel = self.gen_id_sentinel + 1;
                let id = TestIdentity::new(
                    id.module_id(),
                    "iotedge",
                    &format!("{}", self.gen_id_sentinel),
                );
                self.identities.push(id.clone());

                future::ok(id)
            }
        }

        fn get(&self) -> Self::GetFuture {
            if self.fail_get {
                future::err(Error::General)
            } else {
                future::ok(self.identities.clone())
            }
        }

        fn delete(&mut self, id: IdentitySpec) -> Self::DeleteFuture {
            self.identities
                .iter()
                .position(|ref mid| mid.module_id() == id.module_id())
                .map(|index| self.identities.remove(index))
                .map(|_| future::ok(()))
                .unwrap_or_else(|| future::err(Error::ModuleNotFound))
        }
    }
}
