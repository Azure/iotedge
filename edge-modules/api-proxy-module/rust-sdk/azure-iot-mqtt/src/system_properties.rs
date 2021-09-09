/// System properties
#[derive(Debug)]
pub struct SystemProperties {
	pub correlation_id: Option<String>,
	pub message_id: String,
	pub to: String,
	pub iothub_ack: IotHubAck,
}

#[derive(Debug)]
pub enum IotHubAck {
	Full,
	Negative,
	None,
	Positive,
	Other(String),
}

#[derive(Debug, Default)]
pub(crate) struct SystemPropertiesBuilder {
	correlation_id: Option<String>,
	message_id: Option<String>,
	to: Option<String>,
	iothub_ack: Option<IotHubAck>,
}

impl SystemPropertiesBuilder {
	pub(super) fn new() -> Self {
		Default::default()
	}

	/// Checks if `key` corresponds to a system property.
	///
	/// If it does, this function consumes `value` and returns `None`.
	///
	/// If it doesn't, this function returns `Some(value)`.
	pub(super) fn try_property<'a>(&mut self, key: &str, value: std::borrow::Cow<'a, str>) -> Option<std::borrow::Cow<'a, str>> {
		match key {
			"$.cid" => {
				self.correlation_id = Some(value.into_owned());
				None
			},

			"$.mid" => {
				self.message_id = Some(value.into_owned());
				None
			},

			"$.to" => {
				self.to = Some(value.into_owned());
				None
			},

			"iothub-ack" => {
				self.iothub_ack = Some(match &*value {
					"full" => IotHubAck::Full,
					"negative" => IotHubAck::Negative,
					"none" => IotHubAck::None,
					"positive" => IotHubAck::Positive,
					_ => IotHubAck::Other(value.into_owned()),
				});

				None
			},

			_ => Some(value),
		}
	}

	/// Returns an error containing the name of the required property that is missing, if any.
	pub(super) fn build(self) -> Result<SystemProperties, &'static str> {
		let message_id = self.message_id.ok_or("$.mid")?;
		let to = self.to.ok_or("$.to")?;
		let iothub_ack = self.iothub_ack.ok_or("iothub-ack")?;

		Ok(SystemProperties {
			correlation_id: self.correlation_id,
			message_id,
			to,
			iothub_ack,
		})
	}
}
