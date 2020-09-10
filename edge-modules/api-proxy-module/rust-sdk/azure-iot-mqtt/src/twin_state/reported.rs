use std::future::Future;

#[derive(Debug)]
pub(crate) struct State {
	max_back_off: std::time::Duration,
	current_back_off: std::time::Duration,

	keep_alive: std::time::Duration,

	report_twin_state_send: futures_channel::mpsc::Sender<ReportTwinStateRequest>,
	report_twin_state_recv: futures_channel::mpsc::Receiver<ReportTwinStateRequest>,
	previous_twin_state: Option<std::collections::HashMap<String, serde_json::Value>>,
	current_twin_state: std::collections::HashMap<String, serde_json::Value>,
	pending_response: Option<(u8, tokio::time::Delay)>,

	inner: Inner,
}

enum Inner {
	BeginBackOff,

	EndBackOff(tokio::time::Delay),

	Idle,

	SendRequest,
}

impl State {
	pub(crate) fn new(
		max_back_off: std::time::Duration,
		keep_alive: std::time::Duration,
	) -> Self {
		let (report_twin_state_send, report_twin_state_recv) = futures_channel::mpsc::channel(0);

		State {
			max_back_off,
			current_back_off: std::time::Duration::from_secs(0),

			keep_alive,

			report_twin_state_send,
			report_twin_state_recv,
			previous_twin_state: None,
			current_twin_state: Default::default(),
			pending_response: None,

			inner: Default::default(),
		}
	}

	#[allow(
		clippy::unneeded_field_pattern, // Clippy wants wildcard pattern for the `if let Some(Response)` pattern below,
		                                // which would silently allow fields to be added to the variant without adding them here
	)]
	pub(crate) fn poll(
		&mut self,
		cx: &mut std::task::Context<'_>,

		client: &mut mqtt3::Client<crate::IoSource>,

		message: &mut Option<super::InternalTwinStateMessage>,
		previous_request_id: &mut u8,
	) -> Result<super::Response<Message>, super::MessageParseError> {
		use futures_core::Stream;

		loop {
			log::trace!("    {:?}", self.inner);

			match &mut self.inner {
				Inner::BeginBackOff => match self.current_back_off {
					back_off if back_off.as_secs() == 0 => {
						self.current_back_off = std::time::Duration::from_secs(1);
						self.inner = Inner::SendRequest;
					},

					back_off => {
						log::debug!("Backing off for {:?}", back_off);
						self.current_back_off = std::cmp::min(self.max_back_off, self.current_back_off * 2);
						self.inner = Inner::EndBackOff(tokio::time::delay_for(back_off));
					},
				},

				Inner::EndBackOff(back_off_timer) => match std::pin::Pin::new(back_off_timer).poll(cx) {
					std::task::Poll::Ready(()) => self.inner = Inner::SendRequest,
					std::task::Poll::Pending => (),
				},

				Inner::Idle => {
					let mut current_twin_state_changed = false;

					while let std::task::Poll::Ready(Some(report_twin_state_request)) = std::pin::Pin::new(&mut self.report_twin_state_recv).poll_next(cx) {
						match report_twin_state_request {
							ReportTwinStateRequest::Replace(properties) => self.current_twin_state = properties,
							ReportTwinStateRequest::Patch(patch) => merge(&mut self.current_twin_state, patch),
						}

						current_twin_state_changed = true;
					}

					if current_twin_state_changed && self.previous_twin_state.as_ref() != Some(&self.current_twin_state) {
						self.inner = Inner::SendRequest;
						continue;
					}

					if let Some((request_id, timeout)) = &mut self.pending_response {
						if let Some(super::InternalTwinStateMessage::Response { status, request_id: message_request_id, payload: _, version }) = message {
							if *message_request_id == *request_id {
								match status {
									crate::Status::Ok |
									crate::Status::NoContent => {
										let version = *version;

										let _ = message.take();

										self.previous_twin_state = Some(self.current_twin_state.clone());
										self.pending_response = None;

										return Ok(super::Response::Message(Message::Reported(version)));
									},

									status @ crate::Status::TooManyRequests |
									status @ crate::Status::Error(_) => {
										log::warn!("reporting twin state failed with status {}", status);

										let _ = message.take();

										self.inner = Inner::BeginBackOff;
										continue;
									},

									status => {
										let status = *status;
										let _ = message.take();
										return Err(super::MessageParseError::IotHubStatus(status));
									},
								}
							}
						}

						match std::pin::Pin::new(timeout).poll(cx) {
							std::task::Poll::Ready(()) => {
								log::warn!("timed out waiting for report twin state response");
								self.inner = Inner::SendRequest;
							},

							std::task::Poll::Pending => return Ok(super::Response::NotReady),
						}
					}
					else {
						return Ok(super::Response::NotReady);
					}
				},

				Inner::SendRequest => {
					let patch =
						if let Some(previous_twin_state) = &self.previous_twin_state {
							diff(&previous_twin_state, &self.current_twin_state)
						}
						else {
							// Wait for desired_properties to provide the initial reported twin state
							return Ok(super::Response::NotReady);
						};
					let payload = serde_json::to_vec(&patch).expect("cannot fail to serialize HashMap<String, serde_json::Value>");

					let request_id = previous_request_id.wrapping_add(1);
					*previous_request_id = request_id;

					// We don't care about the response since this is a QoS 0 publication.
					// We don't even need to `poll()` the future because `mqtt3::Client::publish` puts it in the send queue *synchronously*.
					// But we do need to tell the caller client to poll the `mqtt3::Client` at least once more so that it attempts to send the message,
					// so return `Response::Continue`.
					let _ = client.publish(mqtt3::proto::Publication {
						topic_name: format!("$iothub/twin/PATCH/properties/reported/?$rid={}", request_id),
						qos: mqtt3::proto::QoS::AtMostOnce,
						retain: false,
						payload: payload.into(),
					});

					let timeout = tokio::time::delay_for(2 * self.keep_alive);

					self.pending_response = Some((request_id, timeout));

					self.inner = Inner::Idle;

					return Ok(super::Response::Continue);
				},
			}
		}
	}

	pub (crate) fn new_connection(&mut self) {
		self.previous_twin_state = None;
		self.inner = Inner::SendRequest;
	}

	pub(crate) fn set_initial_state(&mut self, state: std::collections::HashMap<String, serde_json::Value>) {
		self.previous_twin_state = Some(state);
		self.inner = Inner::SendRequest;
	}

	pub(crate) fn report_twin_state_handle(&self) -> ReportTwinStateHandle {
		ReportTwinStateHandle(self.report_twin_state_send.clone())
	}
}

impl Default for Inner {
	fn default() -> Self {
		Inner::Idle
	}
}

impl std::fmt::Debug for Inner {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			Inner::BeginBackOff => f.debug_struct("BeginBackOff").finish(),

			Inner::EndBackOff(_) => f.debug_struct("EndBackOff").finish(),

			Inner::Idle => f.debug_struct("Idle").finish(),

			Inner::SendRequest => f.debug_struct("SendRequest").finish(),
		}
	}
}

/// Used to report twin state to the Azure IoT Hub
#[derive(Clone)]
pub struct ReportTwinStateHandle(futures_channel::mpsc::Sender<ReportTwinStateRequest>);

impl ReportTwinStateHandle {
	/// Send a direct method response with the given parameters
	pub async fn report_twin_state(&mut self, request: ReportTwinStateRequest) -> Result<(), ReportTwinStateError> {
		use futures_util::SinkExt;

		self.0.send(request).await.map_err(|_| ReportTwinStateError::ClientDoesNotExist)
	}
}

/// The kind of twin state update
#[derive(Debug)]
pub enum ReportTwinStateRequest {
	Replace(std::collections::HashMap<String, serde_json::Value>),
	Patch(std::collections::HashMap<String, serde_json::Value>),
}

#[derive(Debug)]
pub enum ReportTwinStateError {
	ClientDoesNotExist,
}

impl std::fmt::Display for ReportTwinStateError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			ReportTwinStateError::ClientDoesNotExist => write!(f, "client does not exist"),
		}
	}
}

impl std::error::Error for ReportTwinStateError {
}

#[derive(Debug)]
pub(crate) enum Message {
	Reported(Option<usize>),
}

fn merge(properties: &mut std::collections::HashMap<String, serde_json::Value>, patch: std::collections::HashMap<String, serde_json::Value>) {
	fn merge_inner(original_value: &mut serde_json::Value, patch: serde_json::Value) {
		if let serde_json::Value::Object(original_value) = original_value {
			if let serde_json::Value::Object(patch) = patch {
				for (key, value) in patch {
					if value.is_null() {
						original_value.remove(&key);
					}
					else {
						merge_inner(original_value.entry(key).or_insert(serde_json::Value::Null), value);
					}
				}

				return;
			}
		}

		*original_value = patch;
	}

	for (key, value) in patch {
		if value.is_null() {
			properties.remove(&key);
		}
		else {
			merge_inner(properties.entry(key).or_insert(serde_json::Value::Null), value);
		}
	}
}

fn diff(
	previous: &std::collections::HashMap<String, serde_json::Value>,
	current: &std::collections::HashMap<String, serde_json::Value>,
) -> std::collections::HashMap<String, serde_json::Value> {
	fn diff_inner(previous: &serde_json::Value, current: &serde_json::Value) -> serde_json::Value {
		match (previous, current) {
			(serde_json::Value::Object(previous), serde_json::Value::Object(current)) => {
				let mut result: serde_json::Map<_, _> = Default::default();

				for (key, previous_value) in previous {
					if let Some(current_value) = current.get(key) {
						if previous_value != current_value {
							result.insert(key.clone(), diff_inner(previous_value, current_value));
						}
					}
					else {
						result.insert(key.clone(), serde_json::Value::Null);
					}
				}

				for (key, current_value) in current {
					if !previous.contains_key(key) {
						result.insert(key.clone(), current_value.clone());
					}
				}

				serde_json::Value::Object(result)
			},

			(_, _) => current.clone()
		}
	}

	let mut result: std::collections::HashMap<_, _> = Default::default();

	for (key, previous_value) in previous {
		if let Some(current_value) = current.get(key) {
			if previous_value != current_value {
				result.insert(key.clone(), diff_inner(previous_value, current_value));
			}
		}
		else {
			result.insert(key.clone(), serde_json::Value::Null);
		}
	}

	for (key, current_value) in current {
		if !previous.contains_key(key) {
			result.insert(key.clone(), current_value.clone());
		}
	}

	result
}

#[cfg(test)]
mod tests {
	#[test]
	fn diff_merge() {
		verify_diff_merge(
			serde_json::json!({
				"key1": "value1",
				"key2": ["value2"],
				"key3": {
					"key3.1": "value3.1",
					"key3.2": "value3.2"
				},
				"key4": {
					"key4.1": 5
				}
			}),

			serde_json::json!({
				"key1": "new_value1",
				"key2": ["new_value2.1", "new_value2.2"],
				"key3": {
					"key3.1": "new_value3.1",
					"key3.3": "new_value3.3"
				},
				"key4": "new_value4"
			}),

			serde_json::json!({
				"key1": "new_value1",
				"key2": ["new_value2.1", "new_value2.2"],
				"key3": {
					"key3.1": "new_value3.1",
					"key3.2": "value3.2",
					"key3.3": "new_value3.3"
				},
				"key4": "new_value4"
			}),
		);

		verify_diff_merge(
			serde_json::json!({
				"key1": "value1",
				"key2": ["value2"],
				"key3": {
					"key3.1": "value3.1",
					"key3.2": "value3.2"
				}
			}),

			serde_json::json!({
				"key1": null,
				"key2": null,
				"key3": null
			}),

			serde_json::json!({
			}),
		);
	}

	fn verify_diff_merge(previous: serde_json::Value, patch: serde_json::Value, current: serde_json::Value) {
		let mut previous: std::collections::HashMap<_, _> =
			if let serde_json::Value::Object(map) = previous {
				map.into_iter().collect()
			}
			else {
				panic!("previous should be a map");
			};

		let patch: std::collections::HashMap<_, _> =
			if let serde_json::Value::Object(map) = patch {
				map.into_iter().collect()
			}
			else {
				panic!("patch should be a map");
			};

		let current: std::collections::HashMap<_, _> =
			if let serde_json::Value::Object(map) = current {
				map.into_iter().collect()
			}
			else {
				panic!("current should be a map");
			};

		let actual_patch = super::diff(&previous, &current);
		assert_eq!(patch, actual_patch);

		super::merge(&mut previous, patch);
		assert_eq!(previous, current);
	}
}
