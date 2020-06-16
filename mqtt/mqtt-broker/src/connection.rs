use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    sync::Arc,
    time::Duration,
};

use futures_util::{
    future::{select, Either},
    pin_mut,
    sink::{Sink, SinkExt},
    stream::{Stream, StreamExt},
};
use lazy_static::lazy_static;
use tokio::io::{AsyncRead, AsyncWrite};
use tokio::sync::{
    mpsc::{self, UnboundedReceiver, UnboundedSender},
    Semaphore,
};
use tokio_io_timeout::TimeoutStream;
use tokio_util::codec::Framed;
use tracing::{debug, info, span, trace, warn, Level};
use tracing_futures::Instrument;
use uuid::Uuid;

use mqtt3::proto::{self, DecodeError, EncodeError, Packet, PacketCodec};
use mqtt_broker_core::{
    auth::{AuthenticationContext, Authenticator, Certificate},
    ClientId,
};

#[cfg(feature = "edgehub")]
use mqtt_edgehub::topic::translation::{
    translate_incoming_publish, translate_incoming_subscribe, translate_incoming_unsubscribe,
    translate_outgoing_publish,
};

use crate::broker::BrokerHandle;
use crate::transport::GetPeerInfo;
use crate::{Auth, ClientEvent, ConnReq, Error, Message, Publish};

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
pub async fn process<I, N>(
    io: I,
    remote_addr: SocketAddr,
    mut broker_handle: BrokerHandle,
    authenticator: &N,
) -> Result<(), Error>
where
    I: AsyncRead + AsyncWrite + GetPeerInfo<Certificate = Certificate> + Unpin,
    N: Authenticator + ?Sized,
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
            let span = span!(Level::INFO, "connection", client_id=%client_id, remote_addr=%remote_addr, connection=%connection_handle);

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
                        warn!(message = "error authenticating client: {}", error =% *e);
                        Auth::Failure
                    }
                };

                let req = ConnReq::new(client_id.clone(), peer_addr, connect, auth, connection_handle);
                let event = ClientEvent::ConnReq(req);
                let message = Message::Client(client_id.clone(), event);
                broker_handle.send(message).await?;

                // Start up the processing tasks
                let (outgoing, incoming) = codec.split();
                let incoming_task =
                    incoming_task(client_id.clone(), incoming, broker_handle.clone());
                let outgoing_task = outgoing_task(client_id.clone(), events, outgoing, broker_handle.clone());
                pin_mut!(incoming_task);
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
                        broker_handle.send(msg).await?;

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
                        broker_handle.send(msg).await?;

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

async fn incoming_task<S>(
    client_id: ClientId,
    mut incoming: S,
    mut broker: BrokerHandle,
) -> Result<(), Error>
where
    S: Stream<Item = Result<Packet, DecodeError>> + Unpin,
{
    // We limit the number of incoming publications (PublishFrom) per client
    // in order to avoid (a single) publisher to occupy whole BrokerHandle queue.
    // This helps with QoS 0 messages throughput, due to the fact that outgoing_task
    // also uses sends PubAck0 for QoS 0 messages to BrokerHandle queue.
    let incoming_pub_limit = Arc::new(Semaphore::new(10));

    debug!("incoming_task start");
    while let Some(maybe_packet) = incoming.next().await {
        match maybe_packet {
            Ok(packet) => {
                let event = match packet {
                    Packet::Connect(_) => {
                        // [MQTT-3.1.0-2] - The Server MUST process a second CONNECT Packet
                        // sent from a Client as a protocol violation and disconnect the Client.

                        warn!("CONNECT packet received on an already established connection, dropping connection due to protocol violation");
                        return Err(Error::ProtocolViolation);
                    }
                    Packet::ConnAck(connack) => ClientEvent::ConnAck(connack),
                    Packet::Disconnect(disconnect) => {
                        let event = ClientEvent::Disconnect(disconnect);
                        let message = Message::Client(client_id.clone(), event);
                        broker.send(message).await?;
                        debug!("disconnect received. shutting down receive side of connection");
                        return Ok(());
                    }
                    Packet::PingReq(ping) => ClientEvent::PingReq(ping),
                    Packet::PingResp(pingresp) => ClientEvent::PingResp(pingresp),
                    Packet::PubAck(puback) => ClientEvent::PubAck(puback),
                    Packet::PubComp(pubcomp) => ClientEvent::PubComp(pubcomp),
                    Packet::Publish(publish) => {
                        #[cfg(feature = "edgehub")]
                        let publish = translate_incoming_publish(&client_id, publish);
                        let perm = incoming_pub_limit.clone().acquire_owned().await;
                        ClientEvent::PublishFrom(publish, Some(perm))
                    }
                    Packet::PubRec(pubrec) => ClientEvent::PubRec(pubrec),
                    Packet::PubRel(pubrel) => ClientEvent::PubRel(pubrel),
                    Packet::Subscribe(subscribe) => {
                        #[cfg(feature = "edgehub")]
                        let subscribe = translate_incoming_subscribe(&client_id, subscribe);
                        ClientEvent::Subscribe(subscribe)
                    }
                    Packet::SubAck(suback) => ClientEvent::SubAck(suback),
                    Packet::Unsubscribe(unsubscribe) => {
                        #[cfg(feature = "edgehub")]
                        let unsubscribe = translate_incoming_unsubscribe(&client_id, unsubscribe);
                        ClientEvent::Unsubscribe(unsubscribe)
                    }
                    Packet::UnsubAck(unsuback) => ClientEvent::UnsubAck(unsuback),
                };

                let message = Message::Client(client_id.clone(), event);
                broker.send(message).await?;
            }
            Err(e) => {
                warn!(message="error occurred while reading from connection", error=%e);
                return Err(e.into());
            }
        }
    }

    debug!("no more packets. sending DropConnection to broker.");
    let message = Message::Client(client_id.clone(), ClientEvent::DropConnection);
    broker.send(message).await?;
    debug!("incoming_task completing...");
    Ok(())
}

async fn outgoing_task<S>(
    client_id: ClientId,
    mut messages: UnboundedReceiver<Message>,
    mut outgoing: S,
    mut broker: BrokerHandle,
) -> Result<(), (UnboundedReceiver<Message>, Error)>
where
    S: Sink<Packet, Error = EncodeError> + Unpin,
{
    debug!("outgoing_task start");
    while let Some(message) = messages.recv().await {
        debug!("outgoing: {:?}", message);
        let maybe_packet = match message {
            Message::Client(_client_id, event) => match event {
                ClientEvent::ConnReq(_) => None,
                ClientEvent::ConnAck(connack) => Some(Packet::ConnAck(connack)),
                ClientEvent::Disconnect(_) => {
                    debug!("asked to disconnect. outgoing_task completing...");
                    return Ok(());
                }
                ClientEvent::DropConnection => {
                    debug!("asked to drop connection. outgoing_task completing...");
                    return Ok(());
                }
                ClientEvent::PingReq(req) => Some(Packet::PingReq(req)),
                ClientEvent::PingResp(response) => Some(Packet::PingResp(response)),
                ClientEvent::Subscribe(sub) => Some(Packet::Subscribe(sub)),
                ClientEvent::SubAck(suback) => Some(Packet::SubAck(suback)),
                ClientEvent::Unsubscribe(unsub) => Some(Packet::Unsubscribe(unsub)),
                ClientEvent::UnsubAck(unsuback) => Some(Packet::UnsubAck(unsuback)),
                ClientEvent::PublishTo(Publish::QoS12(_id, publish)) => {
                    #[cfg(feature = "edgehub")]
                    let publish = translate_outgoing_publish(publish);
                    Some(Packet::Publish(publish))
                }
                ClientEvent::PublishTo(Publish::QoS0(id, publish)) => {
                    #[cfg(feature = "edgehub")]
                    let publish = translate_outgoing_publish(publish);
                    let result = outgoing.send(Packet::Publish(publish)).await;

                    if let Err(e) = result {
                        warn!(message = "error occurred while writing to connection", error=%e);
                        return Err((messages, e.into()));
                    } else {
                        let message = Message::Client(client_id.clone(), ClientEvent::PubAck0(id));
                        if let Err(e) = broker.send(message).await {
                            warn!(message = "error occurred while sending QoS ack to broker", error=%e);
                            return Err((messages, e));
                        }
                    }
                    None
                }
                ClientEvent::PubAck(puback) => Some(Packet::PubAck(puback)),
                ClientEvent::PubRec(pubrec) => Some(Packet::PubRec(pubrec)),
                ClientEvent::PubRel(pubrel) => Some(Packet::PubRel(pubrel)),
                ClientEvent::PubComp(pubcomp) => Some(Packet::PubComp(pubcomp)),
                event => {
                    warn!("ignoring event for outgoing_task: {:?}", event);
                    None
                }
            },
            Message::System(_event) => None,
        };

        if let Some(packet) = maybe_packet {
            let result = outgoing.send(packet).await;

            if let Err(e) = result {
                warn!(message = "error occurred while writing to connection", error=%e);
                return Err((messages, e.into()));
            }
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
