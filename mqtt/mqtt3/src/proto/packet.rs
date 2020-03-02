use std::convert::TryInto;

use bytes::{ Buf, BufMut };
use tokio_util::codec::Decoder;

use super::{ BufMutExt, ByteBuf };

/// An MQTT packet
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum Packet {
	/// Ref: 3.2 CONNACK – Acknowledge connection request
	ConnAck(ConnAck),

	/// Ref: 3.1 CONNECT – Client requests a connection to a Server
	Connect(Connect),

	/// Ref: 3.14 DISCONNECT - Disconnect notification
	Disconnect(Disconnect),

	/// Ref: 3.12 PINGREQ – PING request
	PingReq(PingReq),

	/// Ref: 3.13 PINGRESP – PING response
	PingResp(PingResp),

	/// Ref: 3.4 PUBACK – Publish acknowledgement
	PubAck(PubAck),

	/// Ref: 3.7 PUBCOMP – Publish complete (QoS 2 publish received, part 3)
	PubComp(PubComp),

	/// 3.3 PUBLISH – Publish message
	Publish(Publish),

	/// Ref: 3.5 PUBREC – Publish received (QoS 2 publish received, part 1)
	PubRec(PubRec),

	/// Ref: 3.6 PUBREL – Publish release (QoS 2 publish received, part 2)
	PubRel(PubRel),

	/// Ref: 3.9 SUBACK – Subscribe acknowledgement
	SubAck(SubAck),

	/// Ref: 3.8 SUBSCRIBE - Subscribe to topics
	Subscribe(Subscribe),

	/// Ref: 3.11 UNSUBACK – Unsubscribe acknowledgement
	UnsubAck(UnsubAck),

	/// Ref: 3.10 UNSUBSCRIBE – Unsubscribe from topics
	Unsubscribe(Unsubscribe),
}

/// Metadata about a [`Packet`]
pub(crate) trait PacketMeta: Sized {
	/// The packet type for this kind of packet
	const PACKET_TYPE: u8;

	/// Decodes this packet from the given buffer
	fn decode(flags: u8, src: bytes::BytesMut) -> Result<Self, super::DecodeError>;

	/// Encodes the variable header and payload corresponding to this packet into the given buffer.
	/// The buffer is expected to already have the packet type and body length encoded into it,
	/// and to have reserved enough space to put the bytes of this packet directly into the buffer.
	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf;
}

/// Ref: 3.2 CONNACK – Acknowledge connection request
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ConnAck {
	pub session_present: bool,
	pub return_code: super::ConnectReturnCode,
}

impl PacketMeta for ConnAck {
	const PACKET_TYPE: u8 = 0x20;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() != (std::mem::size_of::<u8>() + std::mem::size_of::<u8>()) {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let connack_flags = src.get_u8();
		let session_present = match connack_flags {
			0x00 => false,
			0x01 => true,
			connack_flags => return Err(super::DecodeError::UnrecognizedConnAckFlags(connack_flags)),
		};

		let return_code: super::ConnectReturnCode = src.get_u8().into();

		Ok(ConnAck {
			session_present,
			return_code,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let ConnAck { session_present, return_code } = self;
		if *session_present {
			dst.put_u8_bytes(0x01);
		}
		else {
			dst.put_u8_bytes(0x00);
		}

		dst.put_u8_bytes((*return_code).into());

		Ok(())
	}
}

/// Ref: 3.1 CONNECT – Client requests a connection to a Server
#[derive(Clone, Eq, PartialEq)]
pub struct Connect {
	pub username: Option<String>,
	pub password: Option<String>,
	pub will: Option<Publication>,
	pub client_id: super::ClientId,
	pub keep_alive: std::time::Duration,
}

impl std::fmt::Debug for Connect {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		f.debug_struct("Connect")
			.field("username", &self.username)
			.field("will", &self.will)
			.field("client_id", &self.client_id)
			.field("keep_alive", &self.keep_alive)
			.finish()
	}
}

impl PacketMeta for Connect {
	const PACKET_TYPE: u8 = 0x10;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let protocol_name = super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?;
		if protocol_name != "MQTT" {
			return Err(super::DecodeError::UnrecognizedProtocolName(protocol_name));
		}

		let protocol_level = src.try_get_u8()?;
		if protocol_level != 0x04 {
			return Err(super::DecodeError::UnrecognizedProtocolLevel(protocol_level));
		}

		let connect_flags = src.try_get_u8()?;
		if connect_flags & 0x01 != 0 {
			return Err(super::DecodeError::ConnectReservedSet);
		}

		let keep_alive = std::time::Duration::from_secs(u64::from(src.try_get_u16_be()?));

		let client_id = super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?;
		let client_id =
			if client_id == "" {
				super::ClientId::ServerGenerated
			}
			else if connect_flags & 0x02 == 0 {
				super::ClientId::IdWithExistingSession(client_id)
			}
			else {
				super::ClientId::IdWithCleanSession(client_id)
			};

		let will =
			if connect_flags & 0x04 == 0 {
				None
			}
			else {
				let topic_name = super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?;

				let qos = match connect_flags & 0x18 {
					0x00 => QoS::AtMostOnce,
					0x08 => QoS::AtLeastOnce,
					0x10 => QoS::ExactlyOnce,
					qos => return Err(super::DecodeError::UnrecognizedQoS(qos >> 3)),
				};

				let retain = connect_flags & 0x20 != 0;

				let payload_len = usize::from(src.try_get_u16_be()?);
				if src.len() < payload_len {
					return Err(super::DecodeError::IncompletePacket);
				}
				let payload = src.split_to(payload_len).freeze();

				Some(Publication {
					topic_name,
					qos,
					retain,
					payload,
				})
			};

		let username =
			if connect_flags & 0x80 == 0 {
				None
			}
			else {
				Some(super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?)
			};

		let password =
			if connect_flags & 0x40 == 0 {
				None
			}
			else {
				Some(super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?)
			};

		Ok(Connect {
			username,
			password,
			will,
			client_id,
			keep_alive,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let Connect { username, password, will, client_id, keep_alive } = self;

		super::encode_utf8_str("MQTT", dst)?;

		dst.put_u8_bytes(0x04_u8);

		{
			let mut connect_flags = 0x00_u8;
			if username.is_some() {
				connect_flags |= 0x80;
			}
			if password.is_some() {
				connect_flags |= 0x40;
			}
			if let Some(will) = &will {
				if will.retain {
					connect_flags |= 0x20;
				}
				connect_flags |= match will.qos {
					QoS::AtMostOnce => 0x00,
					QoS::AtLeastOnce => 0x08,
					QoS::ExactlyOnce => 0x10,
				};
				connect_flags |= 0x04;
			}
			match client_id {
				super::ClientId::ServerGenerated |
				super::ClientId::IdWithCleanSession(_) => connect_flags |= 0x02,
				super::ClientId::IdWithExistingSession(_) => (),
			}
			dst.put_u8_bytes(connect_flags);
		}

		dst.put_u16_bytes(keep_alive.as_secs().try_into().map_err(|_| super::EncodeError::KeepAliveTooHigh(*keep_alive))?);

		match client_id {
			super::ClientId::ServerGenerated => super::encode_utf8_str("", dst)?,
			super::ClientId::IdWithCleanSession(id) |
			super::ClientId::IdWithExistingSession(id) => super::encode_utf8_str(id, dst)?,
		}

		if let Some(will) = will {
			super::encode_utf8_str(&will.topic_name, dst)?;

			let will_len = will.payload.len();
			dst.put_u16_bytes(will_len.try_into().map_err(|_| super::EncodeError::WillTooLarge(will_len))?);

			dst.put_slice_bytes(&will.payload);
		}

		if let Some(username) = username {
			super::encode_utf8_str(username, dst)?;
		}

		if let Some(password) = password {
			super::encode_utf8_str(password, dst)?;
		}

		Ok(())
	}
}

/// Ref: 3.14 DISCONNECT - Disconnect notification
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Disconnect;

impl PacketMeta for Disconnect {
	const PACKET_TYPE: u8 = 0xE0;

	fn decode(flags: u8, src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || !src.is_empty() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		Ok(Disconnect)
	}

	fn encode<B>(&self, _: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		Ok(())
	}
}

/// Ref: 3.12 PINGREQ – PING request
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PingReq;

impl PacketMeta for PingReq {
	const PACKET_TYPE: u8 = 0xC0;

	fn decode(flags: u8, src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || !src.is_empty() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		Ok(PingReq)
	}

	fn encode<B>(&self, _: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		Ok(())
	}
}

/// Ref: 3.13 PINGRESP – PING response
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PingResp;

impl PacketMeta for PingResp {
	const PACKET_TYPE: u8 = 0xD0;

	fn decode(flags: u8, src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || !src.is_empty() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		Ok(PingResp)
	}

	fn encode<B>(&self, _: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		Ok(())
	}
}

/// Ref: 3.4 PUBACK – Publish acknowledgement
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PubAck {
	pub packet_identifier: super::PacketIdentifier,
}

impl PacketMeta for PubAck {
	const PACKET_TYPE: u8 = 0x40;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() != std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		Ok(PubAck {
			packet_identifier,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let PubAck { packet_identifier } = self;
		dst.put_packet_identifier_bytes(*packet_identifier);
		Ok(())
	}
}

#[allow(clippy::doc_markdown)]
/// Ref: 3.7 PUBCOMP – Publish complete (QoS 2 publish received, part 3)
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PubComp {
	pub packet_identifier: super::PacketIdentifier,
}

impl PacketMeta for PubComp {
	const PACKET_TYPE: u8 = 0x70;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() != std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		Ok(PubComp {
			packet_identifier,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let PubComp { packet_identifier } = self;
		dst.put_packet_identifier_bytes(*packet_identifier);
		Ok(())
	}
}

/// 3.3 PUBLISH – Publish message
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Publish {
	pub packet_identifier_dup_qos: PacketIdentifierDupQoS,
	pub retain: bool,
	pub topic_name: String,
	pub payload: bytes::Bytes,
}

impl PacketMeta for Publish {
	const PACKET_TYPE: u8 = 0x30;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		let dup = (flags & 0x08) != 0;
		let retain = (flags & 0x01) != 0;

		let topic_name = super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?;

		let packet_identifier_dup_qos = match (flags & 0x06) >> 1 {
			0x00 if dup => return Err(super::DecodeError::PublishDupAtMostOnce),

			0x00 => PacketIdentifierDupQoS::AtMostOnce,

			0x01 => {
				let packet_identifier = src.try_get_packet_identifier()?;
				PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, dup)
			},

			0x02 => {
				let packet_identifier = src.try_get_packet_identifier()?;
				PacketIdentifierDupQoS::ExactlyOnce(packet_identifier, dup)
			},

			qos => return Err(super::DecodeError::UnrecognizedQoS(qos)),
		};

		let payload = src.freeze();

		Ok(Publish {
			packet_identifier_dup_qos,
			retain,
			topic_name,
			payload,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		#[allow(clippy::unneeded_field_pattern)]
		let Publish { packet_identifier_dup_qos, retain: _, topic_name, payload } = self;

		super::encode_utf8_str(topic_name, dst)?;

		match packet_identifier_dup_qos {
			PacketIdentifierDupQoS::AtMostOnce => (),
			PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, _) |
			PacketIdentifierDupQoS::ExactlyOnce(packet_identifier, _) =>
				dst.put_packet_identifier_bytes(*packet_identifier),
		}

		dst.put_slice_bytes(&payload);

		Ok(())
	}
}

#[allow(clippy::doc_markdown)]
/// Ref: 3.5 PUBREC – Publish received (QoS 2 publish received, part 1)
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PubRec {
	pub packet_identifier: super::PacketIdentifier,
}

impl PacketMeta for PubRec {
	const PACKET_TYPE: u8 = 0x50;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() != std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		Ok(PubRec {
			packet_identifier,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let PubRec { packet_identifier } = self;
		dst.put_packet_identifier_bytes(*packet_identifier);
		Ok(())
	}
}

#[allow(clippy::doc_markdown)]
/// Ref: 3.6 PUBREL – Publish release (QoS 2 publish received, part 2)
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PubRel {
	pub packet_identifier: super::PacketIdentifier,
}

impl PacketMeta for PubRel {
	const PACKET_TYPE: u8 = 0x60;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 2 || src.len() != std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		Ok(PubRel {
			packet_identifier,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let PubRel { packet_identifier } = self;
		dst.put_packet_identifier_bytes(*packet_identifier);
		Ok(())
	}
}

/// Ref: 3.9 SUBACK – Subscribe acknowledgement
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SubAck {
	pub packet_identifier: super::PacketIdentifier,
	pub qos: Vec<SubAckQos>,
}

impl PacketMeta for SubAck {
	const PACKET_TYPE: u8 = 0x90;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() < std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		let qos: Result<Vec<_>, _> = src.iter().map(|&qos| match qos {
			0x00 => Ok(SubAckQos::Success(QoS::AtMostOnce)),
			0x01 => Ok(SubAckQos::Success(QoS::AtLeastOnce)),
			0x02 => Ok(SubAckQos::Success(QoS::ExactlyOnce)),
			0x80 => Ok(SubAckQos::Failure),
			qos => Err(super::DecodeError::UnrecognizedQoS(qos)),
		}).collect();
		let qos = qos?;

		if qos.is_empty() {
			return Err(super::DecodeError::NoTopics);
		}

		Ok(SubAck {
			packet_identifier,
			qos,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let SubAck { packet_identifier, qos } = self;

		dst.put_packet_identifier_bytes(*packet_identifier);

		for &qos in qos {
			dst.put_u8_bytes(qos.into());
		}

		Ok(())
	}
}

/// Ref: 3.8 SUBSCRIBE - Subscribe to topics
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Subscribe {
	pub packet_identifier: super::PacketIdentifier,
	pub subscribe_to: Vec<SubscribeTo>,
}

impl PacketMeta for Subscribe {
	const PACKET_TYPE: u8 = 0x80;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 2 || src.len() < std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		let mut subscribe_to = vec![];

		while !src.is_empty() {
			let topic_filter = super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?;
			let qos = match src.try_get_u8()? {
				0x00 => QoS::AtMostOnce,
				0x01 => QoS::AtLeastOnce,
				0x02 => QoS::ExactlyOnce,
				qos => return Err(super::DecodeError::UnrecognizedQoS(qos)),
			};
			subscribe_to.push(SubscribeTo { topic_filter, qos });
		}

		if subscribe_to.is_empty() {
			return Err(super::DecodeError::NoTopics);
		}

		Ok(Subscribe {
			packet_identifier,
			subscribe_to,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let Subscribe { packet_identifier, subscribe_to } = self;

		dst.put_packet_identifier_bytes(*packet_identifier);

		for SubscribeTo { topic_filter, qos } in subscribe_to {
			super::encode_utf8_str(topic_filter, dst)?;
			dst.put_u8_bytes((*qos).into());
		}

		Ok(())
	}
}

/// Ref: 3.11 UNSUBACK – Unsubscribe acknowledgement
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct UnsubAck {
	pub packet_identifier: super::PacketIdentifier,
}

impl PacketMeta for UnsubAck {
	const PACKET_TYPE: u8 = 0xB0;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 0 || src.len() != std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		Ok(UnsubAck {
			packet_identifier,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let UnsubAck { packet_identifier } = self;
		dst.put_packet_identifier_bytes(*packet_identifier);
		Ok(())
	}
}

/// Ref: 3.10 UNSUBSCRIBE – Unsubscribe from topics
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Unsubscribe {
	pub packet_identifier: super::PacketIdentifier,
	pub unsubscribe_from: Vec<String>,
}

impl PacketMeta for Unsubscribe {
	const PACKET_TYPE: u8 = 0xA0;

	fn decode(flags: u8, mut src: bytes::BytesMut) -> Result<Self, super::DecodeError> {
		if flags != 2 || src.len() < std::mem::size_of::<u16>() {
			return Err(super::DecodeError::UnrecognizedPacket { packet_type: Self::PACKET_TYPE, flags, remaining_length: src.len() });
		}

		let packet_identifier = src.get_packet_identifier()?;

		let mut unsubscribe_from = vec![];

		while !src.is_empty() {
			unsubscribe_from.push(super::Utf8StringDecoder::default().decode(&mut src)?.ok_or(super::DecodeError::IncompletePacket)?);
		}

		if unsubscribe_from.is_empty() {
			return Err(super::DecodeError::NoTopics);
		}

		Ok(Unsubscribe {
			packet_identifier,
			unsubscribe_from,
		})
	}

	fn encode<B>(&self, dst: &mut B) -> Result<(), super::EncodeError> where B: ByteBuf {
		let Unsubscribe { packet_identifier, unsubscribe_from } = self;

		dst.put_packet_identifier_bytes(*packet_identifier);

		for unsubscribe_from in unsubscribe_from {
			super::encode_utf8_str(unsubscribe_from, dst)?;
		}

		Ok(())
	}
}

#[allow(clippy::doc_markdown)]
/// A combination of the packet identifier, dup flag and QoS that only allows valid combinations of these three properties.
/// Used in [`Packet::Publish`]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PacketIdentifierDupQoS {
	AtMostOnce,
	AtLeastOnce(super::PacketIdentifier, bool),
	ExactlyOnce(super::PacketIdentifier, bool),
}

/// A subscription request.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct SubscribeTo {
	pub topic_filter: String,
	pub qos: QoS,
}

/// The level of reliability for a publication
///
/// Ref: 4.3 Quality of Service levels and protocol flows
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd)]
pub enum QoS {
	AtMostOnce,
	AtLeastOnce,
	ExactlyOnce,
}

impl From<QoS> for u8 {
	fn from(qos: QoS) -> Self {
		match qos {
			QoS::AtMostOnce => 0x00,
			QoS::AtLeastOnce => 0x01,
			QoS::ExactlyOnce => 0x02,
		}
	}
}

#[allow(clippy::doc_markdown)]
/// QoS returned in a SUBACK packet. Either one of the [`QoS`] values, or an error code.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum SubAckQos {
	Success(QoS),
	Failure,
}

impl From<SubAckQos> for u8 {
	fn from(qos: SubAckQos) -> Self {
		match qos {
			SubAckQos::Success(qos) => qos.into(),
			SubAckQos::Failure => 0x80,
		}
	}
}

/// A message that can be published to the server
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Publication {
	pub topic_name: String,
	pub qos: crate::proto::QoS,
	pub retain: bool,
	pub payload: bytes::Bytes,
}

/// A tokio codec that encodes and decodes MQTT packets.
///
/// Ref: 2 MQTT Control Packet format
#[derive(Debug, Default)]
pub struct PacketCodec {
	decoder_state: PacketDecoderState,
}

#[derive(Debug)]
pub enum PacketDecoderState {
	Empty,
	HaveFirstByte { first_byte: u8, remaining_length: super::RemainingLengthDecoder },
	HaveFixedHeader { first_byte: u8, remaining_length: usize },
}

impl Default for PacketDecoderState {
	fn default() -> Self {
		PacketDecoderState::Empty
	}
}

impl tokio_util::codec::Decoder for PacketCodec {
	type Item = Packet;
	type Error = super::DecodeError;

	fn decode(&mut self, src: &mut bytes::BytesMut) -> Result<Option<Self::Item>, Self::Error> {
		let (first_byte, src) = loop {
			match &mut self.decoder_state {
				PacketDecoderState::Empty => {
					let first_byte = match src.try_get_u8() {
						Ok(first_byte) => first_byte,
						Err(_) => return Ok(None),
					};
					self.decoder_state = PacketDecoderState::HaveFirstByte { first_byte, remaining_length: Default::default() };
				},

				PacketDecoderState::HaveFirstByte { first_byte, remaining_length } => match remaining_length.decode(src)? {
					Some(remaining_length) => self.decoder_state = PacketDecoderState::HaveFixedHeader { first_byte: *first_byte, remaining_length },
					None => return Ok(None),
				},

				PacketDecoderState::HaveFixedHeader { first_byte, remaining_length } => {
					if src.len() < *remaining_length {
						return Ok(None);
					}

					let first_byte = *first_byte;
					let src = src.split_to(*remaining_length);
					self.decoder_state = PacketDecoderState::Empty;
					break (first_byte, src);
				},
			}
		};

		let packet_type = first_byte & 0xF0;
		let flags = first_byte & 0x0F;
		match packet_type {
			ConnAck::PACKET_TYPE => Ok(Some(Packet::ConnAck(ConnAck::decode(flags, src)?))),
			Connect::PACKET_TYPE => Ok(Some(Packet::Connect(Connect::decode(flags, src)?))),
			Disconnect::PACKET_TYPE => Ok(Some(Packet::Disconnect(Disconnect::decode(flags, src)?))),
			PingReq::PACKET_TYPE => Ok(Some(Packet::PingReq(PingReq::decode(flags, src)?))),
			PingResp::PACKET_TYPE => Ok(Some(Packet::PingResp(PingResp::decode(flags, src)?))),
			PubAck::PACKET_TYPE => Ok(Some(Packet::PubAck(PubAck::decode(flags, src)?))),
			PubComp::PACKET_TYPE => Ok(Some(Packet::PubComp(PubComp::decode(flags, src)?))),
			Publish::PACKET_TYPE => Ok(Some(Packet::Publish(Publish::decode(flags, src)?))),
			PubRec::PACKET_TYPE => Ok(Some(Packet::PubRec(PubRec::decode(flags, src)?))),
			PubRel::PACKET_TYPE => Ok(Some(Packet::PubRel(PubRel::decode(flags, src)?))),
			SubAck::PACKET_TYPE => Ok(Some(Packet::SubAck(SubAck::decode(flags, src)?))),
			Subscribe::PACKET_TYPE => Ok(Some(Packet::Subscribe(Subscribe::decode(flags, src)?))),
			UnsubAck::PACKET_TYPE => Ok(Some(Packet::UnsubAck(UnsubAck::decode(flags, src)?))),
			Unsubscribe::PACKET_TYPE => Ok(Some(Packet::Unsubscribe(Unsubscribe::decode(flags, src)?))),
			packet_type => Err(super::DecodeError::UnrecognizedPacket { packet_type, flags, remaining_length: src.len() }),
		}
	}
}

impl tokio_util::codec::Encoder for PacketCodec {
	type Item = Packet;
	type Error = super::EncodeError;

	fn encode(&mut self, item: Self::Item, dst: &mut bytes::BytesMut) -> Result<(), Self::Error> {
		dst.reserve(std::mem::size_of::<u8>() + 4 * std::mem::size_of::<u8>());

		match &item {
			Packet::ConnAck(packet) => encode_packet(packet, 0, dst),
			Packet::Connect(packet) => encode_packet(packet, 0, dst),
			Packet::Disconnect(packet) => encode_packet(packet, 0, dst),
			Packet::PingReq(packet) => encode_packet(packet, 0, dst),
			Packet::PingResp(packet) => encode_packet(packet, 0, dst),
			Packet::PubAck(packet) => encode_packet(packet, 0, dst),
			Packet::PubComp(packet) => encode_packet(packet, 0, dst),
			Packet::Publish(packet) => {
				let mut flags = match packet.packet_identifier_dup_qos {
					PacketIdentifierDupQoS::AtMostOnce => 0x00,
					PacketIdentifierDupQoS::AtLeastOnce(_, true) => 0x0A,
					PacketIdentifierDupQoS::AtLeastOnce(_, false) => 0x02,
					PacketIdentifierDupQoS::ExactlyOnce(_, true) => 0x0C,
					PacketIdentifierDupQoS::ExactlyOnce(_, false) => 0x04,
				};
				if packet.retain {
					flags |= 0x01;
				};
				encode_packet(packet, flags, dst)
			},
			Packet::PubRec(packet) => encode_packet(packet, 0, dst),
			Packet::PubRel(packet) => encode_packet(packet, 0x02, dst),
			Packet::SubAck(packet) => encode_packet(packet, 0, dst),
			Packet::Subscribe(packet) => encode_packet(packet, 0x02, dst),
			Packet::UnsubAck(packet) => encode_packet(packet, 0, dst),
			Packet::Unsubscribe(packet) => encode_packet(packet, 0x02, dst),
		}
	}
}

fn encode_packet<P>(packet: &P, flags: u8, dst: &mut bytes::BytesMut) -> Result<(), super::EncodeError> where P: PacketMeta {
	let mut counter = super::ByteCounter::new();
	packet.encode(&mut counter)?;
	let body_len = counter.0;

	dst.reserve(
		std::mem::size_of::<u8>() + // packet type
		4 * std::mem::size_of::<u8>() + // remaining length
		body_len);

	dst.put_u8(<P as PacketMeta>::PACKET_TYPE | flags);
	super::encode_remaining_length(body_len, dst)?;
	packet.encode(dst)?;

	Ok(())
}
