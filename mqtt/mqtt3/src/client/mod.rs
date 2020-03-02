use std::future::Future;

mod connect;

mod ping;

mod publish;
pub use publish::{ PublishError, PublishHandle };

mod subscriptions;
pub use subscriptions::{ UpdateSubscriptionError, UpdateSubscriptionHandle };

/// An MQTT v3.1.1 client.
///
/// A `Client` is a [`Stream`] of [`Event`]s. It automatically reconnects if the connection to the server is broken,
/// and handles session state.
///
/// Publish messages to the server using the handle returned by [`Client::publish_handle`].
///
/// Subscribe to and unsubscribe from topics using the handle returned by [`Client::update_subscription_handle`].
///
/// The [`Stream`] only ends (returns `Ready(None)`) when the client is told to shut down gracefully using the handle
/// returned by [`Client::shutdown_handle`]. The `Client` becomes unusable after it has returned `None`
/// and should be dropped.
#[derive(Debug)]
pub struct Client<IoS>(ClientState<IoS>) where IoS: IoSource;

impl<IoS> Client<IoS> where IoS: IoSource {
	/// Create a new client with the given parameters
	///
	/// * `client_id`
	///
	///     If set, this ID will be used to start a new clean session with the server. On subsequent re-connects, the ID will be re-used.
	///     Otherwise, the client will use a server-generated ID for each new connection.
	///
	/// * `username`
	///
	///     Optional username credential for the server. Note that password is provided via `io_source`.
	///
	/// * `io_source`
	///
	///     The MQTT protocol is layered onto the I/O object returned by this source.
	///
	/// * `max_reconnect_back_off`
	///
	///     Every connection failure will double the back-off period, to a maximum of this value.
	///
	/// * `keep_alive`
	///
	///     The keep-alive time advertised to the server. The client will ping the server at half this interval.
	pub fn new(
		client_id: Option<String>,
		username: Option<String>,
		will: Option<crate::proto::Publication>,
		io_source: IoS,
		max_reconnect_back_off: std::time::Duration,
		keep_alive: std::time::Duration,
	) -> Self {
		let client_id = match client_id {
			Some(id) => crate::proto::ClientId::IdWithCleanSession(id),
			None => crate::proto::ClientId::ServerGenerated,
		};

		let (shutdown_send, shutdown_recv) = futures_channel::mpsc::channel(0);

		// TODO: username / password / will can be too large and prevent a CONNECT packet from being encoded.
		//       `Client::new()` should detect that and retrurn an error.
		//       But password is provided by the IoSource, so it can't be done here?

		Client(ClientState::Up {
			client_id,
			username,
			will,
			keep_alive,

			shutdown_send,
			shutdown_recv,

			packet_identifiers: Default::default(),

			connect: connect::Connect::new(io_source, max_reconnect_back_off),
			ping: ping::State::BeginWaitingForNextPing,
			publish: Default::default(),
			subscriptions: Default::default(),

			packets_waiting_to_be_sent: Default::default(),
		})
	}

	/// Queues a message to be published to the server
	pub fn publish(&mut self, publication: crate::proto::Publication) -> impl Future<Output = Result<(), PublishError>> {
		match &mut self.0 {
			ClientState::Up { publish, .. } => futures_util::future::Either::Left(publish.publish(publication)),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => futures_util::future::Either::Right(futures_util::future::err(PublishError::ClientDoesNotExist)),
		}
	}

	/// Returns a handle that can be used to publish messages to the server
	pub fn publish_handle(&self) -> Result<PublishHandle, PublishError> {
		match &self.0 {
			ClientState::Up { publish, .. } => Ok(publish.publish_handle()),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => Err(PublishError::ClientDoesNotExist),
		}
	}

	/// Subscribes to a topic with the given parameters
	pub fn subscribe(&mut self, subscribe_to: crate::proto::SubscribeTo) -> Result<(), UpdateSubscriptionError> {
		match &mut self.0 {
			ClientState::Up { subscriptions, .. } => subscriptions.subscribe(subscribe_to),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => Err(UpdateSubscriptionError::ClientDoesNotExist),
		}
	}

	/// Unsubscribes from the given topic
	pub fn unsubscribe(&mut self, unsubscribe_from: String) -> Result<(), UpdateSubscriptionError> {
		match &mut self.0 {
			ClientState::Up { subscriptions, .. } => subscriptions.unsubscribe(unsubscribe_from),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => Err(UpdateSubscriptionError::ClientDoesNotExist),
		}
	}

	/// Returns a handle that can be used to update subscriptions
	pub fn update_subscription_handle(&self) -> Result<UpdateSubscriptionHandle, UpdateSubscriptionError> {
		match &self.0 {
			ClientState::Up { subscriptions, .. } => Ok(subscriptions.update_subscription_handle()),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => Err(UpdateSubscriptionError::ClientDoesNotExist),
		}
	}

	/// Returns a handle that can be used to signal the client to shut down
	pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
		match &self.0 {
			ClientState::Up { shutdown_send, .. } => Ok(ShutdownHandle(shutdown_send.clone())),
			ClientState::ShuttingDown { .. } |
			ClientState::ShutDown { .. } => Err(ShutdownError::ClientDoesNotExist),
		}
	}
}

impl<IoS> futures_core::Stream for Client<IoS> where
	Self: Unpin,
	IoS: IoSource,
	<IoS as IoSource>::Io: Unpin,
	<IoS as IoSource>::Error: std::fmt::Display,
	<IoS as super::IoSource>::Future: Unpin,
{
	type Item = Result<Event, Error>;

	fn poll_next(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Option<Self::Item>> {
		use futures_sink::Sink;

		let reason = loop {
			match &mut self.0 {
				ClientState::Up {
					client_id,
					username,
					will,
					keep_alive,

					shutdown_recv,

					packet_identifiers,

					connect,
					ping,
					publish,
					subscriptions,

					packets_waiting_to_be_sent,

					..
				} => {
					match std::pin::Pin::new(shutdown_recv).poll_next(cx) {
						std::task::Poll::Ready(Some(())) => break None,

						std::task::Poll::Ready(None) |
						std::task::Poll::Pending => (),
					}

					let connect::Connected { framed, new_connection, reset_session } = match connect.poll(
						cx,
						username.as_ref().map(AsRef::as_ref),
						will.as_ref(),
						client_id,
						*keep_alive,
					) {
						std::task::Poll::Ready(framed) => framed,
						std::task::Poll::Pending => return std::task::Poll::Pending,
					};

					if new_connection {
						log::debug!("New connection established");

						*packets_waiting_to_be_sent = Default::default();

						ping.new_connection();

						packets_waiting_to_be_sent.extend(publish.new_connection(reset_session, packet_identifiers));

						packets_waiting_to_be_sent.extend(subscriptions.new_connection(reset_session, packet_identifiers));

						return std::task::Poll::Ready(Some(Ok(Event::NewConnection { reset_session })));
					}

					match client_poll(
						cx,
						framed,
						*keep_alive,
						packets_waiting_to_be_sent,
						packet_identifiers,
						ping,
						publish,
						subscriptions,
					) {
						std::task::Poll::Ready(Ok(event)) => return std::task::Poll::Ready(Some(Ok(event))),

						std::task::Poll::Ready(Err(err)) =>
							if err.is_user_error() {
								break Some(err);
							}
							else {
								log::warn!("client will reconnect because of error: {}", err);

								if !err.session_is_resumable() {
									// Ensure clean session if the error is such that the session is not resumable.
									//
									// DEVNOTE: subscriptions::State relies on the fact that the session is reset here.
									// Update that if this ever changes.
									*client_id = match std::mem::replace(client_id, crate::proto::ClientId::ServerGenerated) {
										id @ crate::proto::ClientId::ServerGenerated |
										id @ crate::proto::ClientId::IdWithCleanSession(_) => id,
										crate::proto::ClientId::IdWithExistingSession(id) => crate::proto::ClientId::IdWithCleanSession(id),
									};
								}

								connect.reconnect();
							},

						std::task::Poll::Pending => return std::task::Poll::Pending,
					}
				},

				ClientState::ShuttingDown {
					client_id,
					username,
					will,
					keep_alive,

					connect,

					sent_disconnect,

					reason,
				} => {
					let connect::Connected { mut framed, .. } = match connect.poll(
						cx,
						username.as_ref().map(AsRef::as_ref),
						will.as_ref(),
						client_id,
						*keep_alive,
					) {
						std::task::Poll::Ready(framed) => framed,
						std::task::Poll::Pending => {
							// Already disconnected
							self.0 = ClientState::ShutDown { reason: reason.take() };
							continue;
						},
					};

					loop {
						if *sent_disconnect {
							match std::pin::Pin::new(&mut framed).poll_flush(cx) {
								std::task::Poll::Ready(Ok(())) => {
									self.0 = ClientState::ShutDown { reason: reason.take() };
									break;
								},

								std::task::Poll::Ready(Err(err)) => {
									let err = Error::EncodePacket(err);
									log::warn!("couldn't send DISCONNECT: {}", err);
									self.0 = ClientState::ShutDown { reason: reason.take() };
									break;
								},

								std::task::Poll::Pending => return std::task::Poll::Pending,
							}
						}
						else {
							match std::pin::Pin::new(&mut framed).poll_ready(cx) {
								std::task::Poll::Ready(Ok(())) => {
									let packet = crate::proto::Packet::Disconnect(crate::proto::Disconnect);
									match std::pin::Pin::new(&mut framed).start_send(packet) {
										Ok(()) => *sent_disconnect = true,

										Err(err) => {
											log::warn!("couldn't send DISCONNECT: {}", err);
											self.0 = ClientState::ShutDown { reason: reason.take() };
											break;
										},
									}
								},

								std::task::Poll::Ready(Err(err)) => {
									log::warn!("couldn't send DISCONNECT: {}", err);
									self.0 = ClientState::ShutDown { reason: reason.take() };
									break;
								},

								std::task::Poll::Pending => return std::task::Poll::Pending,
							}
						}
					}
				},

				ClientState::ShutDown { reason } => match reason.take() {
					Some(err) => return std::task::Poll::Ready(Some(Err(err))),
					None => return std::task::Poll::Ready(None),
				},
			}
		};

		// If we're here, then we're transitioning from Up to ShuttingDown

		match std::mem::replace(&mut self.0, ClientState::ShutDown { reason: None }) {
			ClientState::Up {
				client_id,
				username,
				will,
				keep_alive,

				connect,
				..
			} => {
				log::warn!("Shutting down...");

				self.0 = ClientState::ShuttingDown {
					client_id,
					username,
					will,
					keep_alive,

					connect,

					sent_disconnect: false,

					reason,
				};
				self.poll_next(cx)
			},

			_ => unreachable!(),
		}
	}
}

/// This trait provides an I/O object and optional password that a [`Client`] can use.
///
/// The trait is automatically implemented for all [`FnMut`] that return a connection future.
pub trait IoSource {
	/// The I/O object
	type Io: tokio::io::AsyncRead + tokio::io::AsyncWrite;

	/// The error type for this I/O object's connection future.
	type Error;

	/// The connection future. Contains the I/O object and optional password.
	type Future: Future<Output = Result<(Self::Io, Option<String>), Self::Error>>;

	/// Attempts the connection and returns a [`Future`] that resolves when the connection succeeds
	fn connect(&mut self) -> Self::Future;
}

impl<F, A, I, E> IoSource for F
where
	F: FnMut() -> A,
	A: Future<Output = Result<(I, Option<String>), E>>,
	I: tokio::io::AsyncRead + tokio::io::AsyncWrite,
{
	type Io = I;
	type Error = E;
	type Future = A;

	fn connect(&mut self) -> Self::Future {
		(self)()
	}
}

/// An event generated by the [`Client`]
#[derive(Debug, PartialEq, Eq)]
pub enum Event {
	/// The [`Client`] established a new connection to the server.
	NewConnection {
		/// Whether the session was reset as part of this new connection or not
		reset_session: bool,
	},

	/// A publication received from the server
	Publication(ReceivedPublication),

	/// Subscription updates acked by the server
	SubscriptionUpdates(Vec<SubscriptionUpdateEvent>),
}

/// A subscription update event
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SubscriptionUpdateEvent {
	Subscribe(crate::proto::SubscribeTo),
	Unsubscribe(String),
}

/// A message that was received from the server
#[derive(Debug, PartialEq, Eq)]
pub struct ReceivedPublication {
	pub topic_name: String,
	pub dup: bool,
	pub qos: crate::proto::QoS,
	pub retain: bool,
	pub payload: bytes::Bytes,
}

pub struct ShutdownHandle(futures_channel::mpsc::Sender<()>);

impl ShutdownHandle {
	/// Signals the [`Client`] to shut down.
	///
	/// The returned `Future` resolves when the `Client` is guaranteed the notification,
	/// not necessarily when the `Client` has completed shutting down.
	pub async fn shutdown(&mut self) -> Result<(), ShutdownError> {
		use futures_util::SinkExt;

		match self.0.send(()).await {
			Ok(_) => Ok(()),
			Err(_) => Err(ShutdownError::ClientDoesNotExist),
		}
	}
}

#[derive(Debug)]
enum ClientState<IoS> where IoS: IoSource {
	Up {
		client_id: crate::proto::ClientId,
		username: Option<String>,
		will: Option<crate::proto::Publication>,
		keep_alive: std::time::Duration,

		shutdown_send: futures_channel::mpsc::Sender<()>,
		shutdown_recv: futures_channel::mpsc::Receiver<()>,

		packet_identifiers: PacketIdentifiers,

		connect: connect::Connect<IoS>,
		ping: ping::State,
		publish: publish::State,
		subscriptions: subscriptions::State,

		/// Packets waiting to be written to the underlying `Framed`
		packets_waiting_to_be_sent: std::collections::VecDeque<crate::proto::Packet>,
	},

	ShuttingDown {
		client_id: crate::proto::ClientId,
		username: Option<String>,
		will: Option<crate::proto::Publication>,
		keep_alive: std::time::Duration,

		connect: connect::Connect<IoS>,

		/// If the DISCONNECT packet has already been sent
		sent_disconnect: bool,

		/// The Error that caused the Client to transition away from Up, if any
		reason: Option<Error>,
	},

	ShutDown {
		/// The Error that caused the Client to transition away from Up, if any
		reason: Option<Error>,
	},
}

fn client_poll<S>(
	cx: &mut std::task::Context<'_>,

	framed: &mut crate::logging_framed::LoggingFramed<S>,
	keep_alive: std::time::Duration,
	packets_waiting_to_be_sent: &mut std::collections::VecDeque<crate::proto::Packet>,
	packet_identifiers: &mut PacketIdentifiers,
	ping: &mut ping::State,
	publish: &mut publish::State,
	subscriptions: &mut subscriptions::State,
) -> std::task::Poll<Result<Event, Error>>
where
	S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin,
{
	use futures_core::Stream;
	use futures_sink::Sink;

	loop {
		// Begin sending any packets waiting to be sent
		while let Some(packet) = packets_waiting_to_be_sent.pop_front() {
			match std::pin::Pin::new(&mut *framed).poll_ready(cx) {
				std::task::Poll::Ready(result) => {
					let () = result.map_err(Error::EncodePacket)?;
					let () = std::pin::Pin::new(&mut *framed).start_send(packet).map_err(Error::EncodePacket)?;
				},

				std::task::Poll::Pending => {
					packets_waiting_to_be_sent.push_front(packet);
					break;
				},
			}

		}

		// Finish sending any packets waiting to be sent.
		//
		// We don't care whether this returns Poll::Ready or Poll::Pending.
		let _: std::task::Poll<_> = std::pin::Pin::new(&mut *framed).poll_flush(cx).map_err(Error::EncodePacket)?;

		let mut continue_loop = false;

		let mut packet = match std::pin::Pin::new(&mut *framed).poll_next(cx) {
			std::task::Poll::Ready(Some(packet)) => {
				let packet = packet.map_err(Error::DecodePacket)?;

				// May have more packets after this one, so keep looping
				continue_loop = true;
				Some(packet)
			},
			std::task::Poll::Ready(None) => return std::task::Poll::Ready(Err(Error::ServerClosedConnection)),
			std::task::Poll::Pending => None,
		};

		let mut new_packets_to_be_sent = vec![];


		// Ping
		let ping_packet = ping.poll(cx, &mut packet, keep_alive);
		new_packets_to_be_sent.extend(ping_packet);

		// Publish
		let (new_publish_packets, publication_received) = publish.poll(
			cx,
			&mut packet,
			packet_identifiers,
		)?;
		new_packets_to_be_sent.extend(new_publish_packets);

		// Subscriptions
		let subscription_updates =
			if publication_received.is_some() {
				// Already have a new publication to return from this tick, so can't process pending subscription updates
				// because they might generate their own responses.
				vec![]
			}
			else {
				let (new_subscription_packets, subscription_updates) = subscriptions.poll(
					cx,
					&mut packet,
					packet_identifiers,
				)?;
				new_packets_to_be_sent.extend(new_subscription_packets);
				subscription_updates
			};


		assert!(packet.is_none(), "unconsumed packet");

		if !new_packets_to_be_sent.is_empty() {
			// Have new packets to send, so keep looping
			continue_loop = true;
			packets_waiting_to_be_sent.extend(new_packets_to_be_sent);
		}

		if let Some(publication_received) = publication_received {
			return std::task::Poll::Ready(Ok(Event::Publication(publication_received)));
		}

		if !subscription_updates.is_empty() {
			return std::task::Poll::Ready(Ok(Event::SubscriptionUpdates(subscription_updates)));
		}

		if !continue_loop {
			return std::task::Poll::Pending;
		}
	}
}

struct PacketIdentifiers {
	in_use: Box<[usize; PacketIdentifiers::SIZE]>,
	previous: crate::proto::PacketIdentifier,
}

impl PacketIdentifiers {
	#[allow(clippy::doc_markdown)]
	/// Size of a bitset for every packet identifier
	///
	/// Packet identifiers are u16's, so the number of usize's required
	/// = number of u16's / number of bits in a usize
	/// = pow(2, number of bits in a u16) / number of bits in a usize
	/// = pow(2, 16) / (size_of::<usize>() * 8)
	///
	/// We use a bitshift instead of usize::pow because the latter is not a const fn
	const SIZE: usize = (1 << 16) / (std::mem::size_of::<usize>() * 8);

	fn reserve(&mut self) -> Result<crate::proto::PacketIdentifier, Error> {
		let start = self.previous;
		let mut current = start;

		current += 1;

		let (block, mask) = self.entry(current);
		if (*block & mask) != 0 {
			return Err(Error::PacketIdentifiersExhausted);
		}

		*block |= mask;
		self.previous = current;
		Ok(current)
	}

	fn discard(&mut self, packet_identifier: crate::proto::PacketIdentifier) {
		let (block, mask) = self.entry(packet_identifier);
		*block &= !mask;
	}

	fn entry(&mut self, packet_identifier: crate::proto::PacketIdentifier) -> (&mut usize, usize) {
		let packet_identifier = usize::from(packet_identifier.get());
		let (block, offset) = (packet_identifier / (std::mem::size_of::<usize>() * 8), packet_identifier % (std::mem::size_of::<usize>() * 8));
		(&mut self.in_use[block], 1 << offset)
	}
}

impl std::fmt::Debug for PacketIdentifiers {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		f.debug_struct("PacketIdentifiers").field("previous", &self.previous).finish()
	}
}

impl Default for PacketIdentifiers {
	fn default() -> Self {
		PacketIdentifiers {
			in_use: Box::new([0; PacketIdentifiers::SIZE]),
			previous: crate::proto::PacketIdentifier::max_value(),
		}
	}
}

#[derive(Debug)]
pub enum Error {
	DecodePacket(crate::proto::DecodeError),
	DuplicateExactlyOncePublishPacketNotMarkedDuplicate(crate::proto::PacketIdentifier),
	EncodePacket(crate::proto::EncodeError),
	PacketIdentifiersExhausted,
	PingTimer(tokio::time::Error),
	ServerClosedConnection,
	SubAckDoesNotContainEnoughQoS(crate::proto::PacketIdentifier, usize, usize),
	SubscriptionDowngraded(String, crate::proto::QoS, crate::proto::QoS),
	SubscriptionRejectedByServer,
	UnexpectedSubAck(crate::proto::PacketIdentifier, UnexpectedSubUnsubAckReason),
	UnexpectedUnsubAck(crate::proto::PacketIdentifier, UnexpectedSubUnsubAckReason),
}

#[derive(Clone, Copy, Debug)]
pub enum UnexpectedSubUnsubAckReason {
	DidNotExpect,
	Expected(crate::proto::PacketIdentifier),
	ExpectedSubAck(crate::proto::PacketIdentifier),
	ExpectedUnsubAck(crate::proto::PacketIdentifier),
}

impl Error {
	fn is_user_error(&self) -> bool {
		match self {
			Error::EncodePacket(err) => err.is_user_error(),
			_ => false,
		}
	}

	fn session_is_resumable(&self) -> bool {
		match self {
			Error::DecodePacket(crate::proto::DecodeError::Io(err)) => match err.kind() {
				std::io::ErrorKind::TimedOut => true,
				_ => false,
			},
			Error::EncodePacket(crate::proto::EncodeError::Io(err)) => match err.kind() {
				std::io::ErrorKind::TimedOut |
				std::io::ErrorKind::WriteZero => true,
				_ => false,
			},
			Error::ServerClosedConnection => true,
			_ => false,
		}
	}
}

impl std::fmt::Display for Error {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			Error::DecodePacket(err) =>
				write!(f, "could not decode packet: {}", err),

			Error::DuplicateExactlyOncePublishPacketNotMarkedDuplicate(packet_identifier) =>
				write!(
					f,
					"server sent a new ExactlyOnce PUBLISH packet {} with the same packet identifier as another unacknowledged ExactlyOnce PUBLISH packet",
					packet_identifier,
				),

			Error::EncodePacket(err) =>
				write!(f, "could not encode packet: {}", err),

			Error::PacketIdentifiersExhausted =>
				write!(f, "all packet identifiers exhausted"),

			Error::PingTimer(err) =>
				write!(f, "ping timer failed: {}", err),

			Error::ServerClosedConnection =>
				write!(f, "connection closed by server"),

			Error::SubAckDoesNotContainEnoughQoS(packet_identifier, expected, actual) =>
				write!(f, "Expected SUBACK {} to contain {} QoS's but it actually contained {}", packet_identifier, expected, actual),

			Error::SubscriptionDowngraded(topic_name, expected, actual) =>
				write!(f, "Server downgraded subscription for topic filter {:?} with QoS {:?} to {:?}", topic_name, expected, actual),

			Error::SubscriptionRejectedByServer =>
				write!(f, "Server rejected one or more subscriptions"),

			Error::UnexpectedSubAck(packet_identifier, reason) =>
				write!(f, "received SUBACK {} but {}", packet_identifier, reason),

			Error::UnexpectedUnsubAck(packet_identifier, reason) =>
				write!(f, "received UNSUBACK {} but {}", packet_identifier, reason),
		}
	}
}

impl std::error::Error for Error {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		#[allow(clippy::match_same_arms)]
		match self {
			Error::DecodePacket(err) => Some(err),
			Error::DuplicateExactlyOncePublishPacketNotMarkedDuplicate(_) => None,
			Error::EncodePacket(err) => Some(err),
			Error::PacketIdentifiersExhausted => None,
			Error::PingTimer(err) => Some(err),
			Error::ServerClosedConnection => None,
			Error::SubAckDoesNotContainEnoughQoS(_, _, _) => None,
			Error::SubscriptionDowngraded(_, _, _) => None,
			Error::SubscriptionRejectedByServer => None,
			Error::UnexpectedSubAck(_, _) => None,
			Error::UnexpectedUnsubAck(_, _) => None,
		}
	}
}

impl std::fmt::Display for UnexpectedSubUnsubAckReason {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			UnexpectedSubUnsubAckReason::DidNotExpect => write!(f, "did not expect it"),
			UnexpectedSubUnsubAckReason::Expected(packet_identifier) => write!(f, "expected {}", packet_identifier),
			UnexpectedSubUnsubAckReason::ExpectedSubAck(packet_identifier) => write!(f, "expected SUBACK {}", packet_identifier),
			UnexpectedSubUnsubAckReason::ExpectedUnsubAck(packet_identifier) => write!(f, "expected UNSUBACK {}", packet_identifier),
		}
	}
}

#[derive(Debug)]
pub enum ShutdownError {
	ClientDoesNotExist,
}

impl std::fmt::Display for ShutdownError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			ShutdownError::ClientDoesNotExist =>
				write!(f, "client does not exist"),
		}
	}
}

impl std::error::Error for ShutdownError {
}

#[cfg(test)]
mod tests {
	use std::convert::TryInto;

	use super::*;

	#[test]
	fn packet_identifiers() {
		#[cfg(target_pointer_width = "32")]
		assert_eq!(PacketIdentifiers::SIZE, 2048);
		#[cfg(target_pointer_width = "64")]
		assert_eq!(PacketIdentifiers::SIZE, 1024);

		let mut packet_identifiers: PacketIdentifiers = Default::default();
		assert_eq!(packet_identifiers.in_use[..], Box::new([0; PacketIdentifiers::SIZE])[..]);

		assert_eq!(packet_identifiers.reserve().unwrap().get(), 1);
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = 1 << 1;
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		assert_eq!(packet_identifiers.reserve().unwrap().get(), 2);
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = (1 << 1) | (1 << 2);
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		assert_eq!(packet_identifiers.reserve().unwrap().get(), 3);
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = (1 << 1) | (1 << 2) | (1 << 3);
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		packet_identifiers.discard(crate::proto::PacketIdentifier::new(2).unwrap());
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = (1 << 1) | (1 << 3);
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		assert_eq!(packet_identifiers.reserve().unwrap().get(), 4);
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = (1 << 1) | (1 << 3) | (1 << 4);
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		packet_identifiers.discard(crate::proto::PacketIdentifier::new(1).unwrap());
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = (1 << 3) | (1 << 4);
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		packet_identifiers.discard(crate::proto::PacketIdentifier::new(3).unwrap());
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = 1 << 4;
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		packet_identifiers.discard(crate::proto::PacketIdentifier::new(4).unwrap());
		assert_eq!(packet_identifiers.in_use[..], Box::new([0; PacketIdentifiers::SIZE])[..]);

		assert_eq!(packet_identifiers.reserve().unwrap().get(), 5);
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		expected[0] = 1 << 5;
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		let goes_in_next_block: u16 = (std::mem::size_of::<usize>() * 8).try_into().unwrap();
		for i in 6..=goes_in_next_block {
			assert_eq!(packet_identifiers.reserve().unwrap().get(), i);
		}
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		#[allow(clippy::identity_op)]
		{
			expected[0] = usize::max_value() - (1 << 0) - (1 << 1) - (1 << 2) - (1 << 3) - (1 << 4);
			expected[1] |= 1 << 0;
		}
		assert_eq!(packet_identifiers.in_use[..], expected[..]);

		#[allow(clippy::range_minus_one)]
		for i in 5..=(goes_in_next_block - 1) {
			packet_identifiers.discard(crate::proto::PacketIdentifier::new(i).unwrap());
		}
		let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
		#[allow(clippy::identity_op)]
		{
			expected[1] |= 1 << 0;
		}
		assert_eq!(packet_identifiers.in_use[..], expected[..]);
	}
}
