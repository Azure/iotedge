#![allow(dead_code)] // TODO @dmolokanov remove when implemented
use std::fmt;

use crate::error::Error;

#[derive(Clone, Debug, PartialEq)]
pub enum AuthId {
    Any,
    Value(Identity),
}

pub type Identity = String;

#[derive(Clone, Debug)]
pub struct Certificate(Vec<u8>);

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

impl fmt::Display for AuthId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AuthId::Any => write!(f, "any"),
            AuthId::Value(auth_id) => write!(f, "{}", auth_id),
        }
    }
}
