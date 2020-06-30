mod packet;
pub use packet::*;

use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    time::Duration,
};

use futures_util::{
    future::{select, Either},
    pin_mut,
    sink::{Sink, SinkExt},
    stream::{Stream, StreamExt},
};
use lazy_static::lazy_static;
use tokio::{
    io::{AsyncRead, AsyncWrite},
    sync::mpsc::{self, UnboundedReceiver, UnboundedSender},
};
use tokio_io_timeout::TimeoutStream;
use tokio_util::codec::Framed;
use tracing::{debug, info, info_span, trace, warn};
use tracing_futures::Instrument;
use uuid::Uuid;

use mqtt3::proto::{self, DecodeError, Packet, PacketCodec};

use crate::{
    auth::{AuthenticationContext, Authenticator, Certificate},
    broker::BrokerHandle,
    transport::GetPeerInfo,
    Auth, ClientEvent, ClientId, ConnReq, Error, Message,
};

lazy_static! {
    static ref DEFAULT_TIMEOUT: Duration = Duration::from_secs(5);
}

const KEEPALIVE_MULT: f32 = 1.5;

/// Allows sending events to a connection.
///
/// It is important that this struct doesn't implement Clone,
/// as the lifecycle management depends on there only being
/// one sender.
#[derive(Debug)]
pub struct ConnectionHandle {
    id: Uuid,
    sender: UnboundedSender<Message>,
}

impl ConnectionHandle {
    pub(crate) fn new(id: Uuid, sender: UnboundedSender<Message>) -> Self {
        Self { id, sender }
    }

    pub fn from_sender(sender: UnboundedSender<Message>) -> Self {
        Self::new(Uuid::new_v4(), sender)
    }

    pub fn send(&mut self, message: Message) -> Result<(), Error> {
        self.sender
            .send(message)
            .map_err(Error::SendConnectionMessage)
    }
}

impl Display for ConnectionHandle {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.id)
    }
}

impl PartialEq for ConnectionHandle {
    fn eq(&self, other: &Self) -> bool {
        self.id == other.id
    }
}

/// Handles packet processing for a single connection.
///
/// Receives a source of packets and a handle to the Broker.
/// Starts two tasks (sending and receiving)
#[allow(clippy::too_many_lines)]
pub async fn process<I, N, P>(
    io: I,
    remote_addr: SocketAddr,
    mut broker_handle: BrokerHandle,
    authenticator: &N,
    make_processor: P,
) -> Result<(), Error>
where
    I: AsyncRead + AsyncWrite + GetPeerInfo<Certificate = Certificate> + Unpin,
    N: Authenticator + ?Sized,
    P: MakeIncomingPacketProcessor + MakeOutgoingPacketProcessor,
{
    let certificate = io.peer_certificate()?;
    let peer_addr = io.peer_addr()?;

    let mut timeout = TimeoutStream::new(io);
    timeout.set_read_timeout(Some(*DEFAULT_TIMEOUT));
    timeout.set_write_timeout(Some(*DEFAULT_TIMEOUT));

    let mut codec = Framed::new(timeout, PacketCodec::default());

    // [MQTT-3.1.0-1] - After a Network Connection is established by a Client to a Server,
    // the first Packet sent from the Client to the Server MUST be a CONNECT Packet.
    //
    // We need to handle the first CONNECT packet here (instead of in the broker state machine)
    // so that we can get and cache the client_id for use with other packets.
    // The broker state machine will also have to handle not receiving a connect packet first
    // to keep the state machine correct.

    match codec.next().await {
        Some(Ok(Packet::Connect(connect))) => {
            let client_id = client_id(&connect.client_id);
            let (sender, events) = mpsc::unbounded_channel();
            let connection_handle = ConnectionHandle::from_sender(sender);
            let span = info_span!("connection", client_id=%client_id, remote_addr=%remote_addr, connection=%connection_handle);

            // async block to attach instrumentation context
            async {
                info!("new client connection");
                debug!("received CONNECT: {:?}", connect);

                // [MQTT-3.1.2-24] - If the Keep Alive value is non-zero and
                // the Server does not receive a Control Packet from the
                // Client within one and a half times the Keep Alive time
                // period, it MUST disconnect the Network Connection to the
                // Client as if the network had failed.
                let keep_alive = connect.keep_alive.mul_f32(KEEPALIVE_MULT);
                if keep_alive == Duration::from_secs(0) {
                    debug!("received 0 length keepalive from client. disabling keepalive timeout");
                    codec.get_mut().set_read_timeout(None);
                } else {
                    debug!("using keepalive timeout of {:?}", keep_alive);
                    codec.get_mut().set_read_timeout(Some(keep_alive));
                }

                // [MQTT-3.1.4-3] - The Server MAY check that the contents of the CONNECT
                // Packet meet any further restrictions and MAY perform authentication
                // and authorization checks. If any of these checks fail, it SHOULD send an
                // appropriate CONNACK response with a non-zero return code as described in
                // section 3.2 and it MUST close the Network Connection.
                let mut context = AuthenticationContext::new(client_id.clone(), peer_addr);
                if let Some(username) = &connect.username {
                    context.with_username(username);
                }

                if let Some(certificate) = certificate {
                    context.with_certificate(certificate);
                } else if let Some(password) = &connect.password {
                    context.with_password(password);
                }

                let auth = match authenticator.authenticate(context).await {
                    Ok(Some(auth_id)) => Auth::Identity(auth_id),
                    Ok(None) => Auth::Unknown,
                    Err(e) => {
                        warn!(message = "error authenticating client", error =% &*e);
                        Auth::Failure
                    }
                };

                let req = ConnReq::new(client_id.clone(), peer_addr, connect, auth, connection_handle);
                let event = ClientEvent::ConnReq(req);
                let message = Message::Client(client_id.clone(), event);
                broker_handle.send(message)?;

                let (outgoing, incoming) = codec.split();

                // prepare processing incoming packets
                let incoming_processor = make_processor.make_incoming(&client_id);
                let incoming_task =
                    incoming_task(client_id.clone(), incoming, broker_handle.clone(), incoming_processor);
                pin_mut!(incoming_task);

                // prepare processing outgoing packets
                let outgoing_processor = make_processor.make_outgoing(&client_id);
                let outgoing_task = outgoing_task(events, outgoing, broker_handle.clone(), outgoing_processor);
                pin_mut!(outgoing_task);

                match select(incoming_task, outgoing_task).await {
                    Either::Left((Ok(()), out)) => {
                        debug!("incoming_task finished with ok. waiting for outgoing_task to complete...");

                        if let Err((mut recv, e)) = out.await {
                            debug!(message = "outgoing_task finished with an error. draining message receiver for connection...", %e);
                            while let Some(message) = recv.recv().await {
                                trace!("dropping {:?}", message);
                            }
                            debug!("message receiver draining completed.");
                        }
                        debug!("outgoing_task completed.");
                    }
                    Either::Left((Err(e), out)) => {
                        // incoming packet stream completed with an error
                        // send a DropConnection request to the broker and wait for the outgoing
                        // task to drain
                        debug!(message = "incoming_task finished with an error. sending drop connection request to broker", error=%e);
                        let msg = Message::Client(client_id.clone(), ClientEvent::DropConnection);
                        broker_handle.send(msg)?;

                        debug!("waiting for outgoing_task to complete...");
                        if let Err((mut recv, e)) = out.await {
                            debug!(message = "outgoing_task finished with an error. draining message receiver for connection...", %e);
                            while let Some(message) = recv.recv().await {
                                trace!("dropping {:?}", message);
                            }
                            debug!("message receiver draining completed.");
                        }
                        debug!("outgoing_task completed.");
                    }
                    Either::Right((Ok(()), inc)) => {
                        drop(inc);
                        debug!("outgoing finished with ok")
                    }
                    Either::Right((Err((mut recv, e)), inc)) => {
                        // outgoing task failed with an error.
                        // drop the incoming packet processing
                        // Notify the broker that the connection is gone, drain the receiver, and
                        // close the connection

                        drop(inc);

                        debug!(message = "outgoing_task finished with an error. notifying the broker to remove the connection", %e);
                        let msg = Message::Client(client_id.clone(), ClientEvent::CloseSession);
                        broker_handle.send(msg)?;

                        debug!("draining message receiver for connection...");
                        while let Some(message) = recv.recv().await {
                            trace!("dropping {:?}", message);
                        }
                        debug!("message receiver draining completed.");
                    }
                }

                info!("closing connection");
                Ok(())
            }
                .instrument(span)
                .await
        }
        Some(Ok(packet)) => Err(Error::NoConnect(packet)),
        Some(Err(e)) => Err(e.into()),
        None => Err(Error::NoPackets),
    }
}

async fn incoming_task<S, P>(
    client_id: ClientId,
    mut incoming: S,
    mut broker: BrokerHandle,
    mut processor: P,
) -> Result<(), Error>
where
    S: Stream<Item = Result<Packet, DecodeError>> + Unpin,
    P: IncomingPacketProcessor,
{
    debug!("incoming_task start");
    while let Some(maybe_packet) = incoming.next().await {
        match maybe_packet {
            Ok(packet) => match processor.process(packet).await? {
                PacketAction::Continue(message) => {
                    broker.send(message)?;
                }
                PacketAction::Stop(message) => {
                    broker.send(message)?;
                    return Ok(());
                }
            },
            Err(e) => {
                warn!(message="error occurred while reading from connection", error=%e);
                return Err(e.into());
            }
        }
    }

    debug!("no more packets. sending DropConnection to broker.");
    let message = Message::Client(client_id.clone(), ClientEvent::DropConnection);
    broker.send(message)?;
    debug!("incoming_task completing...");
    Ok(())
}

async fn outgoing_task<S, P>(
    mut messages: UnboundedReceiver<Message>,
    mut outgoing: S,
    mut broker: BrokerHandle,
    mut processor: P,
) -> Result<(), (UnboundedReceiver<Message>, Error)>
where
    S: Sink<Packet, Error = proto::EncodeError> + Unpin,
    P: OutgoingPacketProcessor,
{
    debug!("outgoing_task start");
    while let Some(message) = messages.recv().await {
        debug!("outgoing: {:?}", message);
        match processor.process(message).await {
            PacketAction::Continue(Some((packet, message))) => {
                // send a packet to a client
                if let Err(e) = outgoing.send(packet).await {
                    warn!(message = "error occurred while writing to connection", error=%e);
                    return Err((messages, e.into()));
                }

                // send a message back to broker
                if let Some(message) = message {
                    if let Err(e) = broker.send(message) {
                        warn!(message = "error occurred while sending QoS ack to broker", error=%e);
                        return Err((messages, e));
                    }
                }
            }
            PacketAction::Continue(None) => (),
            PacketAction::Stop(_) => return Ok(()),
        }
    }
    debug!("outgoing_task completing...");
    Ok(())
}

fn client_id(client_id: &proto::ClientId) -> ClientId {
    let id = match client_id {
        proto::ClientId::ServerGenerated => Uuid::new_v4().to_string(),
        proto::ClientId::IdWithCleanSession(ref id) => id.to_owned(),
        proto::ClientId::IdWithExistingSession(ref id) => id.to_owned(),
    };
    ClientId::from(id)
}
