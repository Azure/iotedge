use std::future::Future;
use std::io;
use std::marker::PhantomData;
use std::pin::Pin;
use std::task::{Context, Poll};

use hyper::Request;
use hyper::service::Service;
use tokio::net::UnixStream;
use tokio::net::unix::UCred;

pub struct InjectUnixCredentials<T, U>(T, PhantomData<U>);
pub struct AuthenticatedService<T>(T, UCred);

impl<T, U> InjectUnixCredentials<T, U> {
    pub fn new(inner: T) -> Self {
        Self(inner, PhantomData)
    }
}

impl<T> AuthenticatedService<T> {
    pub fn new(inner: T, credentials: UCred) -> Self {
        Self(inner, credentials)
    }
}

impl<T, U> Service<&UnixStream> for InjectUnixCredentials<T, U>
where
    T: Service<Request<U>> + Clone + Send + 'static
{
    type Response = AuthenticatedService<T>;
    type Error = io::Error;
    type Future = Pin<Box<dyn Future<Output = Result<Self::Response, Self::Error>> + Send>>;

    fn poll_ready(&mut self, _: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        Poll::Ready(Ok(()))
    }

    fn call(&mut self, conn: &UnixStream) -> Self::Future {
        let service = self.0.clone();
        let creds = conn.peer_cred();
        Box::pin(async move {
            let creds = creds?;
            Ok(AuthenticatedService::new(service, creds))
        })
    }
}

impl<T: Service<Request<U>>, U> Service<Request<U>> for AuthenticatedService<T> {
    type Response = T::Response;
    type Error = T::Error;
    type Future = T::Future;

    fn poll_ready(&mut self, ctx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        self.0.poll_ready(ctx)
    }

    fn call(&mut self, mut req: Request<U>) -> Self::Future {
        req.extensions_mut().insert(self.1);
        self.0.call(req)
    }
}
