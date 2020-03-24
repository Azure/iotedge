use derive_more::Display;
use failure::Fail;

use crate::Error;

#[derive(Clone, Debug, Display, PartialEq)]
pub enum AuthId {
    #[display(fmt = "*")]
    Any,

    Value(Identity),
}

pub type Identity = String;

#[derive(Clone, Debug)]
pub struct Certificate(Vec<u8>);

#[allow(dead_code)]
pub struct Credentials {
    username: Option<String>,
    password: Option<String>,
    certificate: Option<Certificate>,
}

impl Credentials {
    pub fn new(
        username: Option<String>,
        password: Option<String>,
        certificate: Option<Certificate>,
    ) -> Self {
        Self {
            username,
            password,
            certificate,
        }
    }
}

pub trait Authenticator {
    fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Error>;
}

pub trait Authorizer {
    fn authorize(&self, auth_id: AuthId) -> Result<bool, Error>;
}

pub struct DefaultAuthenticator;

impl Authenticator for DefaultAuthenticator {
    fn authenticate(&self, _credentials: Credentials) -> Result<Option<AuthId>, Error> {
        Ok(Some(AuthId::Any))
    }
}

pub struct DefaultAuthorizer;

impl Authorizer for DefaultAuthorizer {
    fn authorize(&self, _auth_id: AuthId) -> Result<bool, Error> {
        Ok(true)
    }
}

#[derive(Debug, Display, Fail, PartialEq)]
pub enum ErrorReason {
    #[display(fmt = "Error occurred during authentication")]
    Authenticate,

    #[display(fmt = "Error occurred during authorization")]
    Authorize,
}
