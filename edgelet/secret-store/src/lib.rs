#![allow(missing_docs, trivial_casts, unused_variables, unused_mut, unused_imports, unused_extern_crates, non_camel_case_types)]

use async_trait::async_trait;
use futures::Stream;
use std::error::Error;
use std::task::{Poll, Context};
use swagger::{ApiError, ContextWrapper};

type ServiceError = Box<dyn Error + Send + Sync + 'static>;

pub const BASE_PATH: &'static str = "";
pub const API_VERSION: &'static str = "2020-07-22";

#[derive(Debug, PartialEq)]
#[must_use]
pub enum DeleteSecretResponse {
    /// OK
    OK
    ,
    /// NotFound
    NotFound
    (models::ErrorResponse)
    ,
    /// Error
    Error
    (models::ErrorResponse)
}

#[derive(Debug, PartialEq)]
#[must_use]
pub enum GetSecretResponse {
    /// OK
    OK
    (String)
    ,
    /// NotFound
    NotFound
    (models::ErrorResponse)
    ,
    /// Error
    Error
    (models::ErrorResponse)
}

#[derive(Debug, PartialEq)]
#[must_use]
pub enum PullSecretResponse {
    /// OK
    OK
    ,
    /// Error
    Error
    (models::ErrorResponse)
}

#[derive(Debug, PartialEq)]
#[must_use]
pub enum RefreshSecretResponse {
    /// OK
    OK
    ,
    /// NoSource
    NoSource
    (models::ErrorResponse)
    ,
    /// NotFound
    NotFound
    (models::ErrorResponse)
    ,
    /// Error
    Error
    (models::ErrorResponse)
}

#[derive(Debug, PartialEq)]
#[must_use]
pub enum SetSecretResponse {
    /// OK
    OK
    ,
    /// Error
    Error
    (models::ErrorResponse)
}

/// API
#[async_trait]
pub trait Api<C: Send + Sync> {
    fn poll_ready(&self, _cx: &mut Context) -> Poll<Result<(), Box<dyn Error + Send + Sync + 'static>>> {
        Poll::Ready(Ok(()))
    }

    async fn delete_secret(
        &self,
        api_version: String,
        id: String,
        context: &C) -> Result<DeleteSecretResponse, ApiError>;

    async fn get_secret(
        &self,
        api_version: String,
        id: String,
        context: &C) -> Result<GetSecretResponse, ApiError>;

    async fn pull_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        context: &C) -> Result<PullSecretResponse, ApiError>;

    async fn refresh_secret(
        &self,
        api_version: String,
        id: String,
        context: &C) -> Result<RefreshSecretResponse, ApiError>;

    async fn set_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        context: &C) -> Result<SetSecretResponse, ApiError>;

}

/// API where `Context` isn't passed on every API call
#[async_trait]
pub trait ApiNoContext<C: Send + Sync> {

    fn poll_ready(&self, _cx: &mut Context) -> Poll<Result<(), Box<dyn Error + Send + Sync + 'static>>>;

    fn context(&self) -> &C;

    async fn delete_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<DeleteSecretResponse, ApiError>;

    async fn get_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<GetSecretResponse, ApiError>;

    async fn pull_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        ) -> Result<PullSecretResponse, ApiError>;

    async fn refresh_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<RefreshSecretResponse, ApiError>;

    async fn set_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        ) -> Result<SetSecretResponse, ApiError>;

}

/// Trait to extend an API to make it easy to bind it to a context.
pub trait ContextWrapperExt<C: Send + Sync> where Self: Sized
{
    /// Binds this API to a context.
    fn with_context(self: Self, context: C) -> ContextWrapper<Self, C>;
}

impl<T: Api<C> + Send + Sync, C: Clone + Send + Sync> ContextWrapperExt<C> for T {
    fn with_context(self: T, context: C) -> ContextWrapper<T, C> {
         ContextWrapper::<T, C>::new(self, context)
    }
}

#[async_trait]
impl<T: Api<C> + Send + Sync, C: Clone + Send + Sync> ApiNoContext<C> for ContextWrapper<T, C> {
    fn poll_ready(&self, cx: &mut Context) -> Poll<Result<(), ServiceError>> {
        self.api().poll_ready(cx)
    }

    fn context(&self) -> &C {
        ContextWrapper::context(self)
    }

    async fn delete_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<DeleteSecretResponse, ApiError>
    {
        let context = self.context().clone();
        self.api().delete_secret(api_version, id, &context).await
    }

    async fn get_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<GetSecretResponse, ApiError>
    {
        let context = self.context().clone();
        self.api().get_secret(api_version, id, &context).await
    }

    async fn pull_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        ) -> Result<PullSecretResponse, ApiError>
    {
        let context = self.context().clone();
        self.api().pull_secret(api_version, id, body, &context).await
    }

    async fn refresh_secret(
        &self,
        api_version: String,
        id: String,
        ) -> Result<RefreshSecretResponse, ApiError>
    {
        let context = self.context().clone();
        self.api().refresh_secret(api_version, id, &context).await
    }

    async fn set_secret(
        &self,
        api_version: String,
        id: String,
        body: String,
        ) -> Result<SetSecretResponse, ApiError>
    {
        let context = self.context().clone();
        self.api().set_secret(api_version, id, body, &context).await
    }

}


#[cfg(feature = "client")]
pub mod client;

// Re-export Client as a top-level name
#[cfg(feature = "client")]
pub use client::Client;

#[cfg(feature = "server")]
pub mod server;

// Re-export router() as a top-level name
#[cfg(feature = "server")]
pub use self::server::Service;

#[cfg(feature = "server")]
pub mod context;

pub mod models;

#[cfg(any(feature = "client", feature = "server"))]
pub(crate) mod header;
