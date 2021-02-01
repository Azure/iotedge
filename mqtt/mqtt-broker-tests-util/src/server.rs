use std::{
    convert::Infallible,
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
    AuthId, Broker, BrokerSnapshot, Error, MakePacketProcessor, Server, ServerCertificate,
};

/// Used to control server lifetime during tests. Implements
/// Drop to cleanup resources after every test.
pub struct ServerHandle {
    pub address: String,
    pub tls_address: Option<String>,
    pub shutdown: Option<Sender<()>>,
    pub task: Option<JoinHandle<Result<BrokerSnapshot, Error>>>,
}

#[allow(dead_code)]
impl ServerHandle {
    pub fn address(&self) -> String {
        self.address.clone()
    }

    pub fn tls_address(&self) -> Option<String> {
        self.tls_address.clone()
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

/// Starts a test server with tcp and tls endpoints and returns
/// shutdown handle, broker task and server binding.
pub fn start_server_with_tls<N, E, Z>(
    identity: ServerCertificate,
    broker: Broker<Z>,
    authenticator: N,
    tcp_addr: Option<String>,
    tls_addr: Option<String>,
) -> ServerHandle
where
    N: Authenticator<Error = E> + Clone + Send + Sync + 'static,
    Z: Authorizer + Send + 'static,
    E: StdError + Send + Sync + 'static,
{
    run_with_tls(
        |addr, tls_addr| {
            let mut server = Server::from_broker(broker);
            server.with_tcp(addr, authenticator.clone(), None).unwrap();

            if let Some(tls) = tls_addr {
                server.with_tls(tls, identity, authenticator, None).unwrap();
            }

            server
        },
        tcp_addr,
        tls_addr,
    )
}

/// Starts a test server with a provided broker and returns
/// shutdown handle, broker task and server binding.
pub fn start_server<N, E, Z>(broker: Broker<Z>, authenticator: N) -> ServerHandle
where
    N: Authenticator<Error = E> + Send + Sync + 'static,
    Z: Authorizer + Send + 'static,
    E: StdError + Send + Sync + 'static,
{
    run(|addr| {
        let mut server = Server::from_broker(broker);
        server.with_tcp(addr, authenticator, None).unwrap();
        server
    })
}

lazy_static! {
    static ref PORT: AtomicU32 = AtomicU32::new(8889);
}

pub fn run<F, Z, P>(make_server: F) -> ServerHandle
where
    F: FnOnce(String) -> Server<Z, P>,
    Z: Authorizer + Send + 'static,
    P: MakePacketProcessor + Clone + Send + Sync + 'static,
{
    let port = PORT.fetch_add(1, Ordering::SeqCst);
    let addr = format!("localhost:{}", port);

    let server = make_server(addr.clone());

    let (shutdown, rx) = oneshot::channel::<()>();
    let task = tokio::spawn(server.serve(rx.map(drop)));

    ServerHandle {
        address: addr,
        tls_address: None,
        shutdown: Some(shutdown),
        task: Some(task),
    }
}

pub fn run_with_tls<F, Z, P>(
    make_server: F,
    tcp_addr: Option<String>,
    tls_addr: Option<String>,
) -> ServerHandle
where
    F: FnOnce(String, Option<String>) -> Server<Z, P>,
    Z: Authorizer + Send + 'static,
    P: MakePacketProcessor + Clone + Send + Sync + 'static,
{
    let addr = if let Some(addr) = tcp_addr {
        addr
    } else {
        let port = PORT.fetch_add(1, Ordering::SeqCst);
        format!("localhost:{}", port)
    };

    let tls_addr = if tls_addr.is_some() {
        tls_addr
    } else {
        let port = PORT.fetch_add(1, Ordering::SeqCst);
        Some(format!("localhost:{}", port))
    };

    let server = make_server(addr.clone(), tls_addr.clone());

    let (shutdown, rx) = oneshot::channel::<()>();
    let task = tokio::spawn(server.serve(rx.map(drop)));

    ServerHandle {
        address: addr,
        tls_address: tls_addr,
        shutdown: Some(shutdown),
        task: Some(task),
    }
}

#[derive(Clone)]
pub struct DummyAuthenticator(AuthId);

impl DummyAuthenticator {
    pub fn anonymous() -> Self {
        Self(AuthId::Anonymous)
    }

    pub fn with_id(id: impl Into<AuthId>) -> Self {
        Self(id.into())
    }
}

#[async_trait]
impl Authenticator for DummyAuthenticator {
    type Error = Infallible;

    async fn authenticate(&self, _: AuthenticationContext) -> Result<Option<AuthId>, Self::Error> {
        Ok(Some(self.0.clone()))
    }
}
