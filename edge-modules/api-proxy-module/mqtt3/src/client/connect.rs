use std::future::Future;

#[derive(Debug)]
pub(super) struct Connect<IoS> where IoS: super::IoSource {
	io_source: IoS,
	max_back_off: std::time::Duration,
	current_back_off: std::time::Duration,
	state: State<IoS>,
}

enum State<IoS> where IoS: super::IoSource {
	BeginBackOff,
	EndBackOff(tokio::time::Delay),
	BeginConnecting,
	WaitingForIoToConnect(<IoS as super::IoSource>::Future),
	Framed {
		framed: crate::logging_framed::LoggingFramed<<IoS as super::IoSource>::Io>,
		framed_state: FramedState,
		password: Option<String>,
	},
}

#[derive(Clone, Copy, Debug)]
enum FramedState {
	BeginSendingConnect,
	EndSendingConnect,
	WaitingForConnAck,
	Connected { new_connection: bool, reset_session: bool },
}

impl<IoS> std::fmt::Debug for State<IoS> where IoS: super::IoSource {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			State::BeginBackOff => f.write_str("BeginBackOff"),
			State::EndBackOff(_) => f.write_str("EndBackOff"),
			State::BeginConnecting => f.write_str("BeginConnecting"),
			State::WaitingForIoToConnect(_) => f.write_str("WaitingForIoToConnect"),
			State::Framed { framed_state, .. } => f.debug_struct("Framed").field("framed_state", framed_state).finish(),
		}
	}
}

impl<IoS> Connect<IoS> where IoS: super::IoSource {
	pub(super) fn new(io_source: IoS, max_back_off: std::time::Duration) -> Self {
		Connect {
			io_source,
			max_back_off,
			current_back_off: std::time::Duration::from_secs(0),
			state: State::BeginConnecting,
		}
	}

	pub(super) fn reconnect(&mut self) {
		self.state = State::BeginBackOff;
	}
}

impl<IoS> Connect<IoS> where
	IoS: super::IoSource,
	<IoS as super::IoSource>::Io: Unpin,
	<IoS as super::IoSource>::Error: std::fmt::Display,
	<IoS as super::IoSource>::Future: Unpin,
{
	pub(super) fn poll<'a>(
		&'a mut self,
		cx: &mut std::task::Context<'_>,

		username: Option<&str>,
		will: Option<&crate::proto::Publication>,
		client_id: &mut crate::proto::ClientId,
		keep_alive: std::time::Duration,
	) -> std::task::Poll<Connected<'a, IoS>> {
		use futures_core::Stream;
		use futures_sink::Sink;

		let state = &mut self.state;

		loop {
			log::trace!("    {:?}", state);

			match state {
				State::BeginBackOff => match self.current_back_off {
					back_off if back_off.as_secs() == 0 => {
						self.current_back_off = std::time::Duration::from_secs(1);
						*state = State::BeginConnecting;
					},

					back_off => {
						log::debug!("Backing off for {:?}", back_off);
						self.current_back_off = std::cmp::min(self.max_back_off, self.current_back_off * 2);
						*state = State::EndBackOff(tokio::time::delay_for(back_off));
					},
				},

				State::EndBackOff(back_off_timer) => match std::pin::Pin::new(back_off_timer).poll(cx) {
					std::task::Poll::Ready(()) => *state = State::BeginConnecting,
					std::task::Poll::Pending => return std::task::Poll::Pending,
				},

				State::BeginConnecting => {
					let io = self.io_source.connect();
					*state = State::WaitingForIoToConnect(io);
				},

				State::WaitingForIoToConnect(io) => match std::pin::Pin::new(io).poll(cx) {
					std::task::Poll::Ready(Ok((io, password))) => {
						let framed = crate::logging_framed::LoggingFramed::new(io);
						*state =
							State::Framed {
								framed,
								framed_state: FramedState::BeginSendingConnect,
								password,
							};
					},

					std::task::Poll::Ready(Err(err)) => {
						log::warn!("could not connect to server: {}", err);
						*state = State::BeginBackOff;
					},

					std::task::Poll::Pending => return std::task::Poll::Pending,
				},

				State::Framed { framed, framed_state: framed_state @ FramedState::BeginSendingConnect, password } => {
					match std::pin::Pin::new(&mut *framed).poll_ready(cx) {
						std::task::Poll::Ready(Ok(())) => {
							let packet = crate::proto::Packet::Connect(crate::proto::Connect {
								username: username.map(ToOwned::to_owned),
								password: password.clone(),
								will: will.cloned(),
								client_id: client_id.clone(),
								keep_alive,
							});

							match std::pin::Pin::new(&mut *framed).start_send(packet) {
								Ok(()) => *framed_state = FramedState::EndSendingConnect,
								Err(err) => {
									log::warn!("could not connect to server: {}", err);
									*state = State::BeginBackOff;
								},
							}
						},

						std::task::Poll::Ready(Err(err)) => {
							log::warn!("could not connect to server: {}", err);
							*state = State::BeginBackOff;
						},

						std::task::Poll::Pending => return std::task::Poll::Pending,
					}
				},

				State::Framed { framed, framed_state: framed_state @ FramedState::EndSendingConnect, .. } => match std::pin::Pin::new(framed).poll_flush(cx) {
					std::task::Poll::Ready(Ok(())) => *framed_state = FramedState::WaitingForConnAck,
					std::task::Poll::Ready(Err(err)) => {
						log::warn!("could not connect to server: {}", err);
						*state = State::BeginBackOff;
					},
					std::task::Poll::Pending => return std::task::Poll::Pending,
				},

				State::Framed { framed, framed_state: framed_state @ FramedState::WaitingForConnAck, .. } => match std::pin::Pin::new(framed).poll_next(cx) {
					std::task::Poll::Ready(Some(Ok(packet))) => match packet {
						crate::proto::Packet::ConnAck(crate::proto::ConnAck { session_present, return_code: crate::proto::ConnectReturnCode::Accepted }) => {
							self.current_back_off = std::time::Duration::from_secs(0);

							let reset_session = match client_id {
								crate::proto::ClientId::ServerGenerated => true,
								crate::proto::ClientId::IdWithCleanSession(id) => {
									*client_id = crate::proto::ClientId::IdWithExistingSession(std::mem::replace(id, Default::default()));
									true
								},
								crate::proto::ClientId::IdWithExistingSession(id) => {
									*client_id = crate::proto::ClientId::IdWithExistingSession(std::mem::replace(id, Default::default()));
									!session_present
								},
							};

							*framed_state = FramedState::Connected { new_connection: true, reset_session };
						},

						crate::proto::Packet::ConnAck(crate::proto::ConnAck { return_code: crate::proto::ConnectReturnCode::Refused(return_code), .. }) => {
							log::warn!("could not connect to server: connection refused: {:?}", return_code);
							*state = State::BeginBackOff;
						},

						packet => {
							log::warn!("could not connect to server: expected to receive ConnAck but received {:?}", packet);
							*state = State::BeginBackOff;
						},
					},

					std::task::Poll::Ready(Some(Err(err))) => {
						log::warn!("could not connect to server: {}", err);
						*state = State::BeginBackOff;
					},

					std::task::Poll::Ready(None) => {
						log::warn!("could not connect to server: connection closed by server");
						*state = State::BeginBackOff;
					},

					std::task::Poll::Pending => return std::task::Poll::Pending,
				},

				State::Framed { framed, framed_state: FramedState::Connected { new_connection, reset_session }, .. } => {
					let result = Connected {
						framed,
						new_connection: *new_connection,
						reset_session: *reset_session,
					};
					*new_connection = false;
					*reset_session = false;
					return std::task::Poll::Ready(result);
				},
			}
		}
	}
}

pub(super) struct Connected<'a, IoS> where IoS: super::IoSource {
	pub(super) framed: &'a mut crate::logging_framed::LoggingFramed<<IoS as super::IoSource>::Io>,
	pub(super) new_connection: bool,
	pub(super) reset_session: bool,
}
