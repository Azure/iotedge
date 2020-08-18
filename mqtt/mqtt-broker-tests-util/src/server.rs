use std::{
    error::Error as StdError,
    sync::atomic::{AtomicU32, Ordering},
};

use async_trait::async_trait;
use futures_util::FutureExt;
use lazy_static::lazy_static;
use tokio::{
    sync::oneshot::{self, Sender},
    task::JoinHandle,
};

use mqtt_broker::{
    auth::{AuthenticationContext, Authenticator, Authorizer},
    AuthId, Broker, BrokerSnapshot, Error, Server,
};

/// Used to control server lifetime during tests. Implements
/// Drop to cleanup resources after every test.
pub struct ServerHandle {
    pub address: String,
    pub shutdown: Option<Sender<()>>,
    pub task: Option<JoinHandle<Result<BrokerSnapshot, Error>>>,
}

#[allow(dead_code)]
impl ServerHandle {
    pub fn address(&self) -> String {
        self.address.clone()
    }

    pub async fn shutdown(&mut self) -> BrokerSnapshot {
        self.shutdown
            .take()
            .unwrap()
            .send(())
            .expect("couldn't shutdown broker");

        self.task
            .take()
            .unwrap()
            .await
            .unwrap()
            .expect("can't wait for the broker")
    }
}

impl Drop for ServerHandle {
    fn drop(&mut self) {
        if let Some(sender) = self.shutdown.take() {
            sender.send(()).expect("couldn't shutdown broker");
        }
    }
}

/// Starts a test server with a provided broker and returns
/// shutdown handle, broker task and server binding.
pub fn start_server<N, Z>(broker: Broker<Z>, authenticator: N) -> ServerHandle
where
    N: Authenticator<Error = Box<dyn StdError>> + Send + Sync + 'static,
    Z: Authorizer + Send + Sync + 'static,
{
    lazy_static! {
        static ref PORT: AtomicU32 = AtomicU32::new(5555);
    }

    let port = PORT.fetch_add(1, Ordering::SeqCst);
    let address = format!("localhost:{}", port);

    let mut server = Server::from_broker(broker);
    server.tcp(&address, authenticator);

    let (shutdown, rx) = oneshot::channel::<()>();
    let task = tokio::spawn(server.serve(rx.map(drop)));

    ServerHandle {
        address,
        shutdown: Some(shutdown),
        task: Some(task),
    }
}

pub struct DummyAuthenticator(AuthId);

impl DummyAuthenticator {
    pub fn anonymous() -> Self {
        Self(AuthId::Anonymous)
    }
}

#[async_trait]
impl Authenticator for DummyAuthenticator {
    type Error = Box<dyn StdError>;

    async fn authenticate(&self, _: AuthenticationContext) -> Result<Option<AuthId>, Self::Error> {
        Ok(Some(self.0.clone()))
    }
}
