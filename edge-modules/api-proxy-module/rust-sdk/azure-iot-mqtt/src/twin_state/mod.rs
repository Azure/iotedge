pub(crate) mod desired;

pub(crate) mod reported;
pub use reported::{ ReportTwinStateHandle, ReportTwinStateRequest };

/// The full twin state stored in the Azure IoT Hub.
#[derive(Debug, serde_derive::Deserialize)]
pub struct TwinState {
	/// The desired twin state
	pub desired: TwinProperties,

	/// The twin state reported by the device
	pub reported: TwinProperties,
}

/// A collection of twin properties, including a version number
#[derive(Debug, serde_derive::Deserialize)]
pub struct TwinProperties {
	#[serde(rename = "$version")]
	pub version: usize,

	#[serde(flatten)]
	pub properties: std::collections::HashMap<String, serde_json::Value>,
}

#[derive(Debug)]
pub(crate) enum InternalTwinStateMessage {
	Response {
		status: crate::Status,
		request_id: u8,
		version: Option<usize>,
		payload: bytes::Bytes,
	},

	TwinPatch(TwinProperties),
}

impl InternalTwinStateMessage {
	pub(crate) fn parse(publication: mqtt3::ReceivedPublication) -> Result<Self, MessageParseError> {
		if let Some(captures) = RESPONSE_REGEX.captures(&publication.topic_name) {
			let status = &captures[1];
			let status: crate::Status = status.parse().map_err(|err| MessageParseError::ParseResponseStatus(status.to_string(), err))?;

			let query_string = &captures[2];

			let mut request_id = None;
			let mut version = None;

			for (key, value) in url::form_urlencoded::parse(query_string.as_bytes()) {
				match &*key {
					"$rid" => request_id = Some(value.parse().map_err(|err| MessageParseError::ParseResponseRequestId(status.to_string(), err))?),
					"$version" => version = Some(value.parse().map_err(|err| MessageParseError::ParseResponseVersion(status.to_string(), err))?),
					_ => (),
				}
			}

			let request_id =
				if let Some(request_id) = request_id {
					request_id
				}
				else {
					return Err(MessageParseError::MissingResponseRequestId);
				};

			Ok(InternalTwinStateMessage::Response { status, request_id, version, payload: publication.payload })
		}
		else if publication.topic_name.starts_with("$iothub/twin/PATCH/properties/desired/") {
			let twin_properties = serde_json::from_slice(&publication.payload).map_err(MessageParseError::Json)?;
			Ok(InternalTwinStateMessage::TwinPatch(twin_properties))
		}
		else {
			Err(MessageParseError::UnrecognizedMessage(publication))
		}
	}
}

#[derive(Debug)]
pub(crate) enum MessageParseError {
	IotHubStatus(crate::Status),
	Json(serde_json::Error),
	MissingResponseRequestId,
	ParseResponseVersion(String, std::num::ParseIntError),
	ParseResponseRequestId(String, std::num::ParseIntError),
	ParseResponseStatus(String, std::num::ParseIntError),
	UnrecognizedMessage(mqtt3::ReceivedPublication),
}

impl std::fmt::Display for MessageParseError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			MessageParseError::IotHubStatus(status) => write!(f, "IoT Hub failed request with status {}", status),
			MessageParseError::Json(err) => write!(f, "could not parse payload as valid JSON: {}", err),
			MessageParseError::MissingResponseRequestId => write!(f, r#"could not parse response: missing "$rid" property"#),
			MessageParseError::ParseResponseVersion(version, err) => write!(f, "could not parse {:?} as version number of reported twin state: {}", version, err),
			MessageParseError::ParseResponseRequestId(request_id, err) => write!(f, "could not parse {:?} as request ID: {}", request_id, err),
			MessageParseError::ParseResponseStatus(status, err) => write!(f, "could not parse {:?} as status code: {}", status, err),
			MessageParseError::UnrecognizedMessage(publication) => write!(f, "message with topic {:?} could not be recognized", publication.topic_name),
		}
	}
}

impl std::error::Error for MessageParseError {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		#[allow(clippy::match_same_arms)]
		match self {
			MessageParseError::IotHubStatus(_) => None,
			MessageParseError::Json(err) => Some(err),
			MessageParseError::MissingResponseRequestId => None,
			MessageParseError::ParseResponseVersion(_, err) => Some(err),
			MessageParseError::ParseResponseRequestId(_, err) => Some(err),
			MessageParseError::ParseResponseStatus(_, err) => Some(err),
			MessageParseError::UnrecognizedMessage(_) => None,
		}
	}
}

pub(crate) enum Response<M> {
	Message(M),
	Continue,
	NotReady,
}

lazy_static::lazy_static! {
	static ref RESPONSE_REGEX: regex::Regex = regex::Regex::new(r"^\$iothub/twin/res/(\d+)/\?(.+)$").expect("could not compile regex");
}
