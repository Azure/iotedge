use std::{collections::HashSet, fmt::Display, hash::Hash};

use tokio::sync::broadcast::{self, error::RecvError, Receiver, Sender};
use tracing::{debug, error, warn};

/// `BrokerReady` component acts as a intermediate entity to determine whether
/// `Broker` is ready to serve external clients. It awaits on several events
/// from components Broker depends on.
///
/// Notes: Authenticator and Authorizer both needs some data which is not
/// available when process started. They receive it using internal
/// communication channels. Once data is loaded and components initialized
/// they send corresponding events to the `BrokerReady` which in turn unblocks
/// external transports.
pub struct BrokerReady<E> {
    event_send: Sender<E>,
}

impl<E: Clone> Default for BrokerReady<E> {
    fn default() -> Self {
        let (event_send, _) = broadcast::channel(5);
        Self { event_send }
    }
}

impl<E: Clone> BrokerReady<E> {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn handle(&self) -> BrokerReadyHandle<E> {
        BrokerReadyHandle(self.event_send.clone())
    }

    pub fn signal(&self) -> BrokerReadySignal<E> {
        BrokerReadySignal(self.event_send.subscribe())
    }
}

/// A handle to send readiness signals to `BrokerReady`.
pub struct BrokerReadyHandle<E>(Sender<E>);

impl<E> BrokerReadyHandle<E>
where
    E: Clone + Display,
{
    pub fn send(&mut self, event: E) {
        debug!("sending broker ready event: {}", event);
        if self.0.send(event).is_err() {
            error!("no active receiver found")
        }
    }
}

/// A handle to await all readiness events collected by the handle.
pub struct BrokerReadySignal<E>(Receiver<E>);

impl<E> BrokerReadySignal<E>
where
    E: Display + Clone + Eq + Hash + AwaitingEvents<Event = E>,
{
    /// Blocks execution and awaits when all required events are received, or
    /// error occurred.
    pub async fn wait(mut self) -> Result<(), BrokerReadyError> {
        let mut awaiting = E::awaiting();

        while !awaiting.is_empty() {
            match self.0.recv().await {
                Ok(event) => {
                    if !awaiting.remove(&event) {
                        warn!("received duplicating event {}", event);
                    }
                }
                Err(RecvError::Lagged(count)) => {
                    warn!("lagged to receive all events by {} events", count);
                }
                Err(RecvError::Closed) => {
                    return Err(BrokerReadyError::Closed);
                }
            }
        }

        Ok(())
    }
}

/// A trait to define a set of all events required by the `BrokerReady` to
/// receive in order to consider broker as ready to serve external clients.
pub trait AwaitingEvents {
    type Event;

    fn awaiting() -> HashSet<Self::Event>;
}

/// Broker readiness events.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum BrokerReadyEvent {
    /// Signals that `EdgeHubAuthorizer` is ready.
    AuthorizerReady,
    /// Signals that `PolicyAuthorizer` is ready.
    PolicyReady,
}

impl AwaitingEvents for BrokerReadyEvent {
    type Event = Self;

    fn awaiting() -> HashSet<Self::Event> {
        let mut awaiting = HashSet::new();
        awaiting.insert(Self::AuthorizerReady);
        awaiting.insert(Self::PolicyReady);

        awaiting
    }
}

impl Display for BrokerReadyEvent {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::AuthorizerReady => write!(f, "Authorizer Ready"),
            Self::PolicyReady => write!(f, "Policy Ready"),
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum BrokerReadyError {
    #[error("close channel")]
    Closed,
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use super::*;

    #[tokio::test]
    async fn it_unblocks_when_all_events_received() {
        let ready = BrokerReady::<TestEvent>::new();

        let mut sender1 = ready.handle();
        let mut sender2 = ready.handle();

        let join1 = tokio::spawn(ready.signal().wait());
        let join2 = tokio::spawn(ready.signal().wait());

        sender1.send(TestEvent::Event1);
        sender2.send(TestEvent::Event2);

        assert_matches!(join1.await, Ok(Ok(())));
        assert_matches!(join2.await, Ok(Ok(())));
    }

    #[derive(Debug, Clone, PartialEq, Eq, Hash)]
    enum TestEvent {
        Event1,
        Event2,
    }

    impl AwaitingEvents for TestEvent {
        type Event = Self;

        fn awaiting() -> HashSet<Self::Event> {
            let mut awaiting = HashSet::new();
            awaiting.insert(Self::Event1);
            awaiting.insert(Self::Event2);

            awaiting
        }
    }

    impl Display for TestEvent {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            match self {
                Self::Event1 => write!(f, "e1"),
                Self::Event2 => write!(f, "e2"),
            }
        }
    }
}
