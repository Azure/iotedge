use std::future::Future;

use anyhow::Result;
use futures_util::{
    future::{self, Either},
    pin_mut,
};
use mqtt_broker::{sidecar::Sidecar, BrokerHandle, BrokerSnapshot, Message, SystemEvent};

use tracing::{debug, error};

#[cfg(feature = "edgehub")]
mod edgehub;

#[cfg(feature = "edgehub")]
pub use edgehub::{add_sidecars, broker, config, start_server};

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
mod generic;

#[cfg(all(not(feature = "edgehub"), feature = "generic"))]
pub use generic::{add_sidecars, broker, config, start_server};

pub struct Bootstrap {
    sidecars: Vec<Box<dyn Sidecar>>,
}

impl Bootstrap {
    pub fn new() -> Self {
        Self {
            sidecars: Vec::new(),
        }
    }

    pub fn add_sidecar<S: Sidecar + 'static>(&mut self, sidecar: S) {
        self.sidecars.push(Box::new(sidecar));
    }

    pub async fn run(
        self,
        mut broker_handle: BrokerHandle,
        server: impl Future<Output = Result<BrokerSnapshot>>,
    ) -> Result<BrokerSnapshot> {
        let mut shutdowns = Vec::new();
        let mut sidecars = Vec::new();

        for sidecar in self.sidecars {
            shutdowns.push(sidecar.shutdown_handle()?);
            sidecars.push(tokio::spawn(sidecar.run()));
        }

        pin_mut!(server);

        let state = match future::select(server, future::select_all(sidecars)).await {
            // server exited first
            Either::Left((snapshot, sidecars)) => {
                // send shutdown event to each sidecar
                let shutdowns = shutdowns.into_iter().map(|handle| handle.shutdown());
                future::join_all(shutdowns).await;

                // awaits for at least one to finish
                let (_res, _stopped, sidecars) = sidecars.await;

                // wait for the rest to exit
                future::join_all(sidecars).await;

                snapshot?
            }
            // one of sidecars exited first
            Either::Right(((res, stopped, sidecars), server)) => {
                // signal server
                broker_handle.send(Message::System(SystemEvent::Shutdown))?;
                let snapshot = server.await;

                debug!("a sidecar has stopped. shutting down all sidecars...");
                if let Err(e) = res {
                    error!(message = "failed waiting for sidecar shutdown", error = %e);
                }

                // send shutdown event to each of the rest sidecars
                shutdowns.remove(stopped);
                let shutdowns = shutdowns.into_iter().map(|handle| handle.shutdown());
                future::join_all(shutdowns).await;

                // wait for the rest to exit
                future::join_all(sidecars).await;

                snapshot?
            }
        };

        Ok(state)
    }
}
