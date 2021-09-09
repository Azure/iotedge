//! This module contains the device client and device message types.

/// A client for the Azure IoT Hub MQTT protocol. This client receives device-level messages.
///
/// A `Client` is a [`Stream`] of [`Message`]s. These messages contain twin state messages, direct method requests and cloud-to-device messages.
///
/// It automatically reconnects if the connection to the server is broken. Each reconnection will yield one [`Message::TwinInitial`] message.
pub struct Client {
	inner: mqtt3::Client<crate::IoSource>,

	num_default_subscriptions: usize,
	c2d_prefix: String,

	state: State,
	previous_request_id: u8,

	desired_properties: crate::twin_state::desired::State,
	reported_properties: crate::twin_state::reported::State,

	direct_method_response_send: futures_channel::mpsc::Sender<crate::DirectMethodResponse>,
	direct_method_response_recv: futures_channel::mpsc::Receiver<crate::DirectMethodResponse>,
}

#[derive(Debug)]
enum State {
	WaitingForSubscriptions {
		reset_session: bool,
		acked: usize,
	},

	Idle,
}

impl Client {
	/// Creates a new `Client`
	///
	/// * `iothub_hostname`
	///
	///     The hostname of the Azure IoT Hub. Eg "foo.azure-devices.net"
	///
	/// * `device_id`
	///
	///     The ID of the device.
	///
	/// * `authentication`
	///
	///     The method this client should use to authorize with the Azure IoT Hub.
	///
	/// * `transport`
	///
	///     The transport to use for the connection to the Azure IoT Hub.
	///
	/// * `will`
	///
	///     If set, this message will be published by the server if this client disconnects uncleanly.
	///     Use the handle from `.inner().shutdown_handle()` to disconnect cleanly.
	///
	/// * `max_back_off`
	///
	///     Every connection failure or server error will double the back-off period, to a maximum of this value.
	///
	/// * `keep_alive`
	///
	///     The keep-alive time advertised to the server. The client will ping the server at half this interval.
	pub fn new(
		iothub_hostname: String,
		device_id: &str,
		authentication: crate::Authentication,
		transport: crate::Transport,

		will: Option<bytes::Bytes>,

		max_back_off: std::time::Duration,
		keep_alive: std::time::Duration,
	) -> Result<Self, crate::CreateClientError> {
		let c2d_prefix = format!("devices/{}/messages/devicebound/", device_id);

		let (inner, num_default_subscriptions) = crate::client_new(
			iothub_hostname,

			device_id,
			None,

			authentication,
			transport,

			will,

			max_back_off,
			keep_alive,
		)?;

		let (direct_method_response_send, direct_method_response_recv) = futures_channel::mpsc::channel(0);

		Ok(Client {
			inner,

			num_default_subscriptions,
			c2d_prefix,

			state: State::WaitingForSubscriptions { reset_session: true, acked: 0 },
			previous_request_id: u8::max_value(),

			desired_properties: crate::twin_state::desired::State::new(max_back_off, keep_alive),
			reported_properties: crate::twin_state::reported::State::new(max_back_off, keep_alive),

			direct_method_response_send,
			direct_method_response_recv,
		})
	}

	/// Gets a reference to the inner `mqtt3::Client`
	pub fn inner(&self) -> &mqtt3::Client<crate::IoSource> {
		&self.inner
	}

	/// Returns a handle that can be used to respond to direct methods
	pub fn direct_method_response_handle(&self) -> crate::DirectMethodResponseHandle {
		crate::DirectMethodResponseHandle(self.direct_method_response_send.clone())
	}

	/// Returns a handle that can be used to publish reported twin state to the Azure IoT Hub
	pub fn report_twin_state_handle(&self) -> crate::ReportTwinStateHandle {
		self.reported_properties.report_twin_state_handle()
	}
}

impl futures_core::Stream for Client {
	type Item = Result<Message, mqtt3::Error>;

	fn poll_next(mut self: std::pin::Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> std::task::Poll<Option<Self::Item>> {
		let this = &mut *self;

		loop {
			log::trace!("    {:?}", this.state);

			while let std::task::Poll::Ready(Some(direct_method_response)) = std::pin::Pin::new(&mut this.direct_method_response_recv).poll_next(cx) {
				let crate::DirectMethodResponse { request_id, status, payload, ack_sender } = direct_method_response;
				let payload = serde_json::to_vec(&payload).expect("cannot fail to serialize serde_json::Value");
				let publication = mqtt3::proto::Publication {
					topic_name: format!("$iothub/methods/res/{}/?$rid={}", status, request_id),
					qos: mqtt3::proto::QoS::AtLeastOnce,
					retain: false,
					payload: payload.into(),
				};

				if ack_sender.send(Box::new(this.inner.publish(publication))).is_err() {
					log::debug!("could not send ack for direct method response because ack receiver has been dropped");
				}
			}

			match &mut this.state {
				State::WaitingForSubscriptions { reset_session, acked } =>
					if *reset_session {
						match std::pin::Pin::new(&mut this.inner).poll_next(cx) {
							std::task::Poll::Ready(Some(Ok(mqtt3::Event::NewConnection { .. }))) | std::task::Poll::Ready(Some(Ok(mqtt3::Event::Disconnected(_)))) => (),

							std::task::Poll::Ready(Some(Ok(mqtt3::Event::Publication(publication)))) => match InternalMessage::parse(publication, &this.c2d_prefix) {
 								Ok(InternalMessage::CloudToDevice(message)) =>
								 	return std::task::Poll::Ready(Some(Ok(Message::CloudToDevice(message)))),

								Ok(InternalMessage::DirectMethod { name, payload, request_id }) =>
									return std::task::Poll::Ready(Some(Ok(Message::DirectMethod { name, payload, request_id }))),

								Ok(message @ InternalMessage::TwinState(_)) =>
									log::debug!("Discarding message {:?} because we haven't finished subscribing yet", message),

								Err(err) =>
									log::warn!("Discarding message that could not be parsed: {}", err),
							},

							std::task::Poll::Ready(Some(Ok(mqtt3::Event::SubscriptionUpdates(updates)))) => {
								log::debug!("subscriptions acked by server: {:?}", updates);
								*acked += updates.len();
								log::debug!("waiting for {} more subscriptions", this.num_default_subscriptions - *acked);
								if *acked == this.num_default_subscriptions {
									this.state = State::Idle;
								}
							},

							std::task::Poll::Ready(Some(Err(err))) => return std::task::Poll::Ready(Some(Err(err))),

							std::task::Poll::Ready(None) => return std::task::Poll::Ready(None),

							std::task::Poll::Pending => return std::task::Poll::Pending,
						}
					}
					else {
						this.state = State::Idle;
					},

				State::Idle => {
					let mut continue_loop = false;

					let mut twin_state_message = match std::pin::Pin::new(&mut this.inner).poll_next(cx) {
						std::task::Poll::Ready(Some(Ok(mqtt3::Event::NewConnection { reset_session }))) => {
							this.state = State::WaitingForSubscriptions { reset_session, acked: 0 };
							this.desired_properties.new_connection();
							this.reported_properties.new_connection();
							continue;
						},

						std::task::Poll::Ready(Some(Ok(mqtt3::Event::Publication(publication)))) => match InternalMessage::parse(publication, &this.c2d_prefix) {
							Ok(InternalMessage::CloudToDevice(message)) =>
								return std::task::Poll::Ready(Some(Ok(Message::CloudToDevice(message)))),

							Ok(InternalMessage::DirectMethod { name, payload, request_id }) =>
								return std::task::Poll::Ready(Some(Ok(Message::DirectMethod { name, payload, request_id }))),

							Ok(InternalMessage::TwinState(message)) => {
								// There may be more messages, so continue the loop
								continue_loop = true;

								Some(message)
							},

							Err(err) => {
								log::warn!("Discarding message that could not be parsed: {}", err);
								continue;
							},
						},

						std::task::Poll::Ready(Some(Ok(mqtt3::Event::Disconnected(_)))) => continue,

						// Don't expect any subscription updates at this point
						std::task::Poll::Ready(Some(Ok(mqtt3::Event::SubscriptionUpdates(_)))) => unreachable!(),

						std::task::Poll::Ready(Some(Err(err))) => return std::task::Poll::Ready(Some(Err(err))),

						std::task::Poll::Ready(None) => return std::task::Poll::Ready(None),

						std::task::Poll::Pending => None,
					};

					match this.desired_properties.poll(cx, &mut this.inner, &mut twin_state_message, &mut this.previous_request_id) {
						Ok(crate::twin_state::Response::Message(crate::twin_state::desired::Message::Initial(twin_state))) => {
							this.reported_properties.set_initial_state(twin_state.reported.properties.clone());
							return std::task::Poll::Ready(Some(Ok(Message::TwinInitial(twin_state))));
						},

						Ok(crate::twin_state::Response::Message(crate::twin_state::desired::Message::Patch(properties))) =>
							return std::task::Poll::Ready(Some(Ok(Message::TwinPatch(properties)))),

						Ok(crate::twin_state::Response::Continue) => continue_loop = true,

						Ok(crate::twin_state::Response::NotReady) => (),

						Err(err) => log::warn!("Discarding message that could not be parsed: {}", err),
					}

					match this.reported_properties.poll(cx, &mut this.inner, &mut twin_state_message, &mut this.previous_request_id) {
						Ok(crate::twin_state::Response::Message(message)) => match message {
							crate::twin_state::reported::Message::Reported(version) =>
								return std::task::Poll::Ready(Some(Ok(Message::ReportedTwinState(version)))),
						},
						Ok(crate::twin_state::Response::Continue) => continue_loop = true,
						Ok(crate::twin_state::Response::NotReady) => (),
						Err(err) => log::warn!("Discarding message that could not be parsed: {}", err),
					}

					if let Some(twin_state_message) = twin_state_message {
						// This can happen if the Azure IoT Hub responded to a reported property request that we aren't waiting for
						// because we have since sent a new one
						log::debug!("unconsumed twin state message {:?}", twin_state_message);
					}

					if !continue_loop {
						return std::task::Poll::Pending;
					}
				},
			}
		}
	}
}

/// A message generated by a [`Client`]
#[derive(Debug)]
pub enum Message {
	/// A cloud-to-device message
	CloudToDevice(CloudToDeviceMessage),

	/// A direct method invocation
	DirectMethod {
		name: String,
		payload: serde_json::Value,
		request_id: String,
	},

	/// The server acknowledged a report of the twin state. Contains the version number of the updated section.
	ReportedTwinState(Option<usize>),

	/// The full twin state, as currently stored in the Azure IoT Hub.
	TwinInitial(crate::TwinState),

	/// A patch to the twin state that should be applied to the current state to get the new state.
	TwinPatch(crate::TwinProperties),
}

#[derive(Debug)]
enum MessageParseError {
	C2DMessageMissingRequiredProperty(&'static str),
	Json(serde_json::Error),
	UnrecognizedMessage(crate::twin_state::MessageParseError),
}

impl std::fmt::Display for MessageParseError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			MessageParseError::C2DMessageMissingRequiredProperty(property) => write!(f, "C2D message does not contain required property {}", property),
			MessageParseError::Json(err) => write!(f, "could not parse payload as valid JSON: {}", err),
			MessageParseError::UnrecognizedMessage(err) => write!(f, "message could not be recognized: {}", err),
		}
	}
}

impl std::error::Error for MessageParseError {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		#[allow(clippy::match_same_arms)]
		match self {
			MessageParseError::C2DMessageMissingRequiredProperty(_) => None,
			MessageParseError::Json(err) => Some(err),
			MessageParseError::UnrecognizedMessage(err) => Some(err),
		}
	}
}

#[derive(Debug)]
pub struct CloudToDeviceMessage {
	pub system_properties: crate::SystemProperties,
	pub properties: std::collections::HashMap<String, String>,
	pub data: bytes::Bytes,
}

#[derive(Debug)]
enum InternalMessage {
	CloudToDevice(CloudToDeviceMessage),

	DirectMethod {
		name: String,
		payload: serde_json::Value,
		request_id: String,
	},

	TwinState(crate::twin_state::InternalTwinStateMessage),
}

impl InternalMessage {
	fn parse(
		publication: mqtt3::ReceivedPublication,
		c2d_prefix: &str,
	) -> Result<Self, MessageParseError> {
		if publication.topic_name.starts_with(c2d_prefix) {
			let mut system_properties = crate::system_properties::SystemPropertiesBuilder::new();
			let mut properties: std::collections::HashMap<_, _> = Default::default();

			for (key, value) in url::form_urlencoded::parse(publication.topic_name[c2d_prefix.len()..].as_bytes()) {
				if let Some(value) = system_properties.try_property(&*key, value) {
					properties.insert(key.into_owned(), value.into_owned());
				}
			}

			let system_properties = system_properties.build().map_err(MessageParseError::C2DMessageMissingRequiredProperty)?;
			Ok(InternalMessage::CloudToDevice(CloudToDeviceMessage {
				system_properties,
				properties,
				data: publication.payload,
			}))
		}
		else if let Some(captures) = crate::DIRECT_METHOD_REGEX.captures(&publication.topic_name) {
			let name = captures[1].to_string();
			let payload = serde_json::from_slice(&publication.payload).map_err(MessageParseError::Json)?;
			let request_id = captures[2].to_string();

			Ok(InternalMessage::DirectMethod {
				name,
				payload,
				request_id,
			})
		}
		else {
			match crate::twin_state::InternalTwinStateMessage::parse(publication) {
				Ok(message) => Ok(InternalMessage::TwinState(message)),
				Err(err) => Err(MessageParseError::UnrecognizedMessage(err))
			}
		}
	}
}
