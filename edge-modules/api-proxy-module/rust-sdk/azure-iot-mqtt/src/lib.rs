//! This crate contains types related to the Azure IoT MQTT server.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
	clippy::cognitive_complexity,
	clippy::default_trait_access,
	clippy::doc_markdown,
	clippy::large_enum_variant,
	clippy::missing_errors_doc,
	clippy::module_name_repetitions,
	clippy::pub_enum_variant_names,
	clippy::similar_names,
	clippy::single_match_else,
	clippy::too_many_arguments,
	clippy::too_many_lines,
	clippy::use_self,
)]

use std::future::Future;

pub mod device;

mod io;
pub use io::{ Io, IoSource, Transport };

pub mod iotedge_client;

pub mod module;

mod system_properties;
pub use system_properties::{ IotHubAck, SystemProperties };

mod twin_state;
pub use twin_state::{ ReportTwinStateHandle, ReportTwinStateRequest, TwinProperties, TwinState };

/// The type of authentication the client should use to connect to the Azure IoT Hub
pub enum Authentication {
	/// The device ID and SAS key are used to generate a new SAS token for every connection attempt.
	/// Each token expires `max_token_valid_duration` time after the connection attempt.
	SasKey {
		device_id: String,
		key: Vec<u8>,
		max_token_valid_duration: std::time::Duration,
		/// Trusted server root certificate, if any
		server_root_certificate: Vec<native_tls::Certificate>,
	},

	/// SAS token to be used directly
	SasToken {
		token: String,
		/// Trusted server root certificate, if any
		server_root_certificate: Vec<native_tls::Certificate>,
	},

	/// Client certificate
	Certificate {
		/// PKCS12 certificate with private key
		der: Vec<u8>,
		/// Password to decrypt the private key
		password: String,
		/// Trusted server root certificate, if any
		server_root_certificate: Vec<native_tls::Certificate>,
	},

	/// Connect as an Edge module
	IotEdge {
		device_id: String,
		module_id: String,
		generation_id: String,
		iothub_hostname: String,
		workload_url: url::Url,
	},
}

/// Errors from creating a device or module client
#[derive(Debug)]
pub enum CreateClientError {
	InvalidDefaultSubscription(mqtt3::UpdateSubscriptionError),
	ParseEnvironmentVariable(&'static str, Box<dyn std::error::Error>),
	ResolveIotHubHostname(Option<std::io::Error>),
	WebSocketUrl(<http::Uri as std::str::FromStr>::Err),
}

impl std::fmt::Display for CreateClientError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			CreateClientError::InvalidDefaultSubscription(err) => write!(f, "could not create default MQTT subscription: {}", err),
			CreateClientError::ParseEnvironmentVariable(name, err) => write!(f, "could not parse environment variable {}: {}", name, err),
			CreateClientError::ResolveIotHubHostname(Some(err)) => write!(f, "could not resolve Azure IoT Hub hostname: {}", err),
			CreateClientError::ResolveIotHubHostname(None) => write!(f, "could not resolve Azure IoT Hub hostname: no addresses found"),
			CreateClientError::WebSocketUrl(err) => write!(f, "could not construct a valid URL for the Azure IoT Hub: {}", err),
		}
	}
}

impl std::error::Error for CreateClientError {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		match self {
			CreateClientError::InvalidDefaultSubscription(err) => Some(err),
			CreateClientError::ParseEnvironmentVariable(_, err) => Some(&**err),
			CreateClientError::ResolveIotHubHostname(Some(err)) => Some(err),
			CreateClientError::ResolveIotHubHostname(None) => None,
			CreateClientError::WebSocketUrl(err) => Some(err),
		}
	}
}

/// Used to respond to direct methods
#[derive(Clone)]
pub struct DirectMethodResponseHandle(futures_channel::mpsc::Sender<DirectMethodResponse>);

impl DirectMethodResponseHandle {
	/// Send a direct method response with the given parameters
	pub async fn respond(&mut self, request_id: String, status: crate::Status, payload: serde_json::Value) -> Result<(), DirectMethodResponseError> {
		use futures_util::SinkExt;

		let (ack_sender, ack_receiver) = futures_channel::oneshot::channel();

		let direct_method_response = DirectMethodResponse { request_id, status, payload, ack_sender };
		self.0.send(direct_method_response).await.map_err(|_| DirectMethodResponseError::ClientDoesNotExist)?;
		let publish = ack_receiver.await.map_err(|_| DirectMethodResponseError::ClientDoesNotExist)?;
		publish.await.map_err(|_| DirectMethodResponseError::ClientDoesNotExist)?;
		Ok(())
	}
}

#[derive(Debug)]
pub enum DirectMethodResponseError {
	ClientDoesNotExist,
}

impl std::fmt::Display for DirectMethodResponseError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			DirectMethodResponseError::ClientDoesNotExist => write!(f, "client does not exist"),
		}
	}
}

impl std::error::Error for DirectMethodResponseError {
}

struct DirectMethodResponse {
	request_id: String,
	status: crate::Status,
	payload: serde_json::Value,
	ack_sender: futures_channel::oneshot::Sender<Box<dyn Future<Output = Result<(), mqtt3::PublishError>> + Send + Unpin>>,
}

/// Represents the status code used in initial twin responses and device method responses
#[derive(Clone, Copy, Debug)]
pub enum Status {
	/// 200
	Ok,

	/// 204
	NoContent,

	/// 400
	BadRequest,

	/// 429
	TooManyRequests,

	/// 5xx
	Error(u32),

	/// Other
	Other(u32),
}

impl std::fmt::Display for Status {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		#[allow(clippy::match_same_arms)]
		match self {
			Status::Ok => write!(f, "200"),
			Status::NoContent => write!(f, "204"),
			Status::BadRequest => write!(f, "400"),
			Status::TooManyRequests => write!(f, "429"),
			Status::Error(raw) => write!(f, "{}", raw),
			Status::Other(raw) => write!(f, "{}", raw),
		}
	}
}

impl std::str::FromStr for Status {
	type Err = std::num::ParseIntError;

	fn from_str(s: &str) -> Result<Self, Self::Err> {
		Ok(match s.parse()? {
			200 => Status::Ok,
			204 => Status::NoContent,
			400 => Status::BadRequest,
			429 => Status::TooManyRequests,
			raw if (500..600).contains(&raw) => Status::Error(raw),
			raw => Status::Other(raw),
		})
	}
}

fn client_new(
	iothub_hostname: String,

	device_id: &str,
	module_id: Option<&str>,

	authentication: crate::Authentication,
	transport: crate::Transport,

	will: Option<bytes::Bytes>,

	max_back_off: std::time::Duration,
	keep_alive: std::time::Duration,
) -> Result<(mqtt3::Client<crate::IoSource>, usize), crate::CreateClientError> {
	let client_id =
		if let Some(module_id) = &module_id {
			format!("{}/{}", device_id, module_id)
		}
		else {
			device_id.to_string()
		};

	let username =
		if let Some(module_id) = &module_id {
			format!("{}/{}/{}/?api-version=2018-06-30", iothub_hostname, device_id, module_id)
		}
		else {
			format!("{}/{}/?api-version=2018-06-30", iothub_hostname, device_id)
		};

	let will = match (will, module_id.as_ref()) {
		(Some(payload), Some(module_id)) => Some((format!("devices/{}/modules/{}/messages/events/", device_id, module_id), payload)),
		(Some(payload), None) => Some((format!("devices/{}/messages/events/", device_id), payload)),
		(None, _) => None,
	};
	let will = will.map(|(topic_name, payload)| mqtt3::proto::Publication {
		topic_name,
		qos: mqtt3::proto::QoS::AtMostOnce,
		retain: false,
		payload,
	});

	let io_source = crate::IoSource::new(
		iothub_hostname.into(),
		authentication,
		2 * keep_alive,
		transport,
	)?;

	let mut inner = mqtt3::Client::new(
		Some(client_id),
		Some(username),
		will,
		io_source,
		max_back_off,
		keep_alive,
	);

	let default_subscriptions = vec![
		// Twin initial GET response
		mqtt3::proto::SubscribeTo {
			topic_filter: "$iothub/twin/res/#".to_string(),
			qos: mqtt3::proto::QoS::AtMostOnce,
		},

		// Twin patches
		mqtt3::proto::SubscribeTo {
			topic_filter: "$iothub/twin/PATCH/properties/desired/#".to_string(),
			qos: mqtt3::proto::QoS::AtMostOnce,
		},

		// Direct methods / module methods
		mqtt3::proto::SubscribeTo {
			topic_filter: "$iothub/methods/POST/#".to_string(),
			qos: mqtt3::proto::QoS::AtLeastOnce,
		},
	];

	let num_default_subscriptions = default_subscriptions.len();

	for subscribe_to in default_subscriptions {
		match inner.subscribe(subscribe_to) {
			Ok(()) => (),

			// The subscription can only fail with this error if `inner` has shut down, which is not the case here
			Err(mqtt3::UpdateSubscriptionError::ClientDoesNotExist) => unreachable!(),

			Err(err @ mqtt3::UpdateSubscriptionError::EncodePacket(..)) => return Err(CreateClientError::InvalidDefaultSubscription(err)),
		}
	}

	Ok((inner, num_default_subscriptions))
}

lazy_static::lazy_static! {
	static ref DIRECT_METHOD_REGEX: regex::Regex = regex::Regex::new(r"^\$iothub/methods/POST/([^/]+)/\?\$rid=(.+)$").expect("could not compile regex");
}
