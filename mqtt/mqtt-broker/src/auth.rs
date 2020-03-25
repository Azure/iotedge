use async_trait::async_trait;
use derive_more::Display;
use failure::Fail;

use crate::Error;

#[derive(Clone, Debug, Display, PartialEq)]
pub enum AuthId {
    #[display(fmt = "*")]
    Anonymous,

    Value(Identity),
}

pub type Identity = String;

#[derive(Clone, Debug)]
pub struct Certificate(Vec<u8>);

pub enum Credentials {
    Basic(Option<String>, Option<String>),
    ClientCertificate(Certificate),
}

#[async_trait]
pub trait Authenticator {
    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Error>;
}

#[async_trait]
impl<F> Authenticator for F
where
    F: Fn(Credentials) -> Result<Option<AuthId>, Error> + Sync,
{
    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Error> {
        self(credentials)
    }
}

pub struct DefaultAuthenticator;

#[async_trait]
impl Authenticator for DefaultAuthenticator {
    async fn authenticate(&self, _: Credentials) -> Result<Option<AuthId>, Error> {
        Ok(Some(AuthId::Anonymous))
    }
}

#[async_trait]
pub trait Authorizer {
    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Error>;
}

#[async_trait]
impl<F> Authorizer for F
where
    F: Fn(AuthId) -> Result<bool, Error> + Sync,
{
    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Error> {
        self(auth_id)
    }
}

pub struct DefaultAuthorizer;

#[async_trait]
impl Authorizer for DefaultAuthorizer {
    async fn authorize(&self, _: AuthId) -> Result<bool, Error> {
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
