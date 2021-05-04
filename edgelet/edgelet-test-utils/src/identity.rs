// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;

use edgelet_core::{AuthType, Identity, IdentityManager, IdentitySpec};

#[derive(Clone, Copy, Debug, Fail)]
pub enum Error {
    #[fail(display = "General error")]
    General,

    #[fail(display = "Module not found")]
    ModuleNotFound,

    #[fail(display = "Module generation ID was not provided")]
    MissingGenerationId,
}

#[derive(Clone, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct TestIdentity {
    #[serde(rename = "moduleId")]
    module_id: String,
    #[serde(rename = "managedBy")]
    managed_by: String,
    #[serde(rename = "generationId")]
    generation_id: String,
    #[serde(rename = "authType")]
    auth_type: AuthType,
}

impl TestIdentity {
    pub fn new(
        module_id: &str,
        managed_by: &str,
        generation_id: &str,
        auth_type: AuthType,
    ) -> Self {
        TestIdentity {
            module_id: module_id.to_string(),
            managed_by: managed_by.to_string(),
            generation_id: generation_id.to_string(),
            auth_type,
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

    fn auth_type(&self) -> AuthType {
        self.auth_type
    }
}

#[derive(Clone)]
pub struct TestIdentityManager {
    identities: Vec<TestIdentity>,
    gen_id_sentinel: u32,
    fail_list: bool,
    fail_get: bool,
    fail_create: bool,
}

impl TestIdentityManager {
    pub fn new(identities: Vec<TestIdentity>) -> Self {
        TestIdentityManager {
            identities,
            gen_id_sentinel: 0,
            fail_list: false,
            fail_get: true,
            fail_create: false,
        }
    }

    pub fn with_fail_list(mut self, fail_list: bool) -> Self {
        self.fail_list = fail_list;
        self
    }

    pub fn with_fail_get(mut self, fail_get: bool) -> Self {
        self.fail_get = fail_get;
        self
    }

    pub fn with_fail_create(mut self, fail_create: bool) -> Self {
        self.fail_create = fail_create;
        self
    }
}

#[async_trait::async_trait]
impl IdentityManager for TestIdentityManager {
    type Identity = TestIdentity;
    type Error = Error;

    async fn create(&mut self, id: IdentitySpec) -> Result<Self::Identity, Self::Error> {
        if self.fail_create {
            Err(Error::General)
        } else {
            self.gen_id_sentinel += 1;
            let id = TestIdentity::new(
                id.module_id(),
                id.managed_by().unwrap_or(&"".to_string()),
                &format!("{}", self.gen_id_sentinel),
                AuthType::Sas,
            );
            self.identities.push(id.clone());

            Ok(id)
        }
    }

    async fn update(&mut self, id: IdentitySpec) -> Result<Self::Identity, Self::Error> {
        if let Some(generation_id) = id.generation_id() {
            // find the existing module
            let index = self
                .identities
                .iter()
                .position(|m| m.module_id() == id.module_id())
                .unwrap();

            let mut module = self.identities[index].clone();

            // verify if genid matches
            assert_eq!(&module.generation_id, generation_id);

            // set the sas type
            module.auth_type = AuthType::Sas;

            // Update managed by
            module.managed_by = id.managed_by().unwrap_or(&"".to_string()).to_string();

            // delete/insert updated module
            self.identities.remove(index);
            self.identities.push(module.clone());

            Ok(module)
        } else {
            Err(Error::MissingGenerationId)
        }
    }

    async fn list(&self) -> Result<Vec<Self::Identity>, Self::Error> {
        if self.fail_list {
            Err(Error::General)
        } else {
            Ok(self.identities.clone())
        }
    }

    async fn get(&self, id: IdentitySpec) -> Result<Option<Self::Identity>, Self::Error> {
        if self.fail_get {
            Err(Error::General)
        } else {
            match self
                .identities
                .iter()
                .find(|m| m.module_id() == id.module_id())
            {
                Some(module) => Ok(Some(module.clone())),
                None => Err(Error::ModuleNotFound),
            }
        }
    }

    async fn delete(&mut self, id: IdentitySpec) -> Result<(), Self::Error> {
        self.identities
            .iter()
            .position(|ref mid| mid.module_id() == id.module_id())
            .map(|index| {
                self.identities.remove(index);
            })
            .ok_or(Error::ModuleNotFound)
    }
}
