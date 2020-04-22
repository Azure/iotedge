use futures_util::StreamExt;
use mqtt3::proto::{Publication, SubscribeTo};
use mqtt3::{
    Client, Event, PublishError, PublishHandle, ShutdownError, ShutdownHandle,
    UpdateSubscriptionError, UpdateSubscriptionHandle,
};
use tokio::sync::mpsc::{self, Receiver};
use tokio::task::JoinError;

/// A wrapper on the mqtt3::Client to help simplify client event loop management.
#[derive(Debug)]
pub struct TestClient {
    publish_handle: PublishHandle,
    subscription_handle: UpdateSubscriptionHandle,
    shutdown_handle: ShutdownHandle,
    pub events_receiver: Receiver<Event>,
    task: tokio::task::JoinHandle<()>,
}

impl TestClient {
    pub async fn publish(&mut self, publication: Publication) -> Result<(), PublishError> {
        self.publish_handle.publish(publication).await
    }

    pub async fn subscribe(
        &mut self,
        subscribe_to: SubscribeTo,
    ) -> Result<(), UpdateSubscriptionError> {
        self.subscription_handle.subscribe(subscribe_to).await
    }

    pub async fn shutdown(&mut self) -> Result<(), ShutdownError> {
        self.shutdown_handle.shutdown().await
    }

    pub async fn join(self) -> Result<(), JoinError> {
        self.task.await
    }
}

#[derive(Default)]
pub struct TestClientBuilder {
    server: Option<String>,
    client_id: Option<String>,
    username: Option<String>,
    password: Option<String>,
    will: Option<Publication>,
}

#[allow(dead_code)]
impl TestClientBuilder {
    pub fn server(mut self, server: &str) -> Self {
        self.server = Some(server.into());
        self
    }

    pub fn client_id(mut self, client_id: &str) -> Self {
        self.client_id = Some(client_id.into());
        self
    }

    pub fn username(mut self, username: &str) -> Self {
        self.username = Some(username.into());
        self
    }

    pub fn password(mut self, password: &str) -> Self {
        self.password = Some(password.into());
        self
    }

    pub fn will(mut self, will: Publication) -> Self {
        self.will = Some(will);
        self
    }

    pub fn build(self) -> TestClient {
        let server = self.server.expect("server address is missing");
        let password = self.password.clone();

        let mut client = Client::new(
            self.client_id.clone(),
            self.username.clone(),
            self.will.clone(),
            move || {
                let server = server.clone();
                let password = password.clone();
                Box::pin(async move {
                    let io = tokio::net::TcpStream::connect(&server).await;
                    io.map(|io| (io, password))
                })
            },
            std::time::Duration::from_secs(1),
            std::time::Duration::from_secs(60),
        );

        let publish_handle = client
            .publish_handle()
            .expect("couldn't get publish handle");

        let subscription_handle = client
            .update_subscription_handle()
            .expect("couldn't get subscribe handle");

        let shutdown_handle = client
            .shutdown_handle()
            .expect("couldn't get shutdown handle");

        let (mut events_sender, events_receiver) = mpsc::channel(128);

        let task = tokio::spawn(async move {
            while let Some(event) = client.next().await {
                let e = event.unwrap();
                events_sender
                    .send(e)
                    .await
                    .expect("can't send an event to a channel");
            }
        });

        TestClient {
            publish_handle,
            subscription_handle,
            shutdown_handle,
            events_receiver,
            task,
        }
    }
}
