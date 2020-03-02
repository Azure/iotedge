/*!
 * MQTT protocol types.
 */

use std::convert::TryInto;

use bytes::{ Buf, BufMut };

mod packet;
pub use packet::{
	Packet,

	ConnAck,
	Connect,
	Disconnect,
	PingReq,
	PingResp,
	PubAck,
	PubComp,
	Publish,
	PubRec,
	PubRel,
	SubAck,
	Subscribe,
	UnsubAck,
	Unsubscribe,

	PacketCodec,
	PacketIdentifierDupQoS,
	Publication,
	QoS,
	SubAckQos,
	SubscribeTo,
};

pub(crate) use packet::PacketMeta;

/// The client ID
///
/// Refs:
/// - 3.1.3.1 Client Identifier
/// - 3.1.2.4 Clean Session
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum ClientId {
	ServerGenerated,
	IdWithCleanSession(String),
	IdWithExistingSession(String),
}

/// The return code for a connection attempt
///
/// Ref: 3.2.2.3 Connect Return code
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ConnectReturnCode {
	Accepted,
	Refused(ConnectionRefusedReason),
}

/// The reason the connection was refused by the server
///
/// Ref: 3.2.2.3 Connect Return code
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ConnectionRefusedReason {
	UnacceptableProtocolVersion,
	IdentifierRejected,
	ServerUnavailable,
	BadUserNameOrPassword,
	NotAuthorized,
	Other(u8),
}

impl From<u8> for ConnectReturnCode {
	fn from(code: u8) -> Self {
		match code {
			0x00 => ConnectReturnCode::Accepted,
			0x01 => ConnectReturnCode::Refused(ConnectionRefusedReason::UnacceptableProtocolVersion),
			0x02 => ConnectReturnCode::Refused(ConnectionRefusedReason::IdentifierRejected),
			0x03 => ConnectReturnCode::Refused(ConnectionRefusedReason::ServerUnavailable),
			0x04 => ConnectReturnCode::Refused(ConnectionRefusedReason::BadUserNameOrPassword),
			0x05 => ConnectReturnCode::Refused(ConnectionRefusedReason::NotAuthorized),
			code => ConnectReturnCode::Refused(ConnectionRefusedReason::Other(code)),
		}
	}
}

impl From<ConnectReturnCode> for u8 {
	fn from(code: ConnectReturnCode) -> Self {
		match code {
			ConnectReturnCode::Accepted => 0x00,
			ConnectReturnCode::Refused(ConnectionRefusedReason::UnacceptableProtocolVersion) => 0x01,
			ConnectReturnCode::Refused(ConnectionRefusedReason::IdentifierRejected) => 0x02,
			ConnectReturnCode::Refused(ConnectionRefusedReason::ServerUnavailable) => 0x03,
			ConnectReturnCode::Refused(ConnectionRefusedReason::BadUserNameOrPassword) => 0x04,
			ConnectReturnCode::Refused(ConnectionRefusedReason::NotAuthorized) => 0x05,
			ConnectReturnCode::Refused(ConnectionRefusedReason::Other(code)) => code,
		}
	}
}

/// A tokio decoder of MQTT-format strings.
///
/// Strings are prefixed with a two-byte big-endian length and are encoded as utf-8.
///
/// Ref: 1.5.3 UTF-8 encoded strings
#[derive(Debug)]
pub enum Utf8StringDecoder {
	Empty,
	HaveLength(usize),
}

impl Default for Utf8StringDecoder {
	fn default() -> Self {
		Utf8StringDecoder::Empty
	}
}

impl tokio_util::codec::Decoder for Utf8StringDecoder {
	type Item = String;
	type Error = DecodeError;

	fn decode(&mut self, src: &mut bytes::BytesMut) -> Result<Option<Self::Item>, Self::Error> {
		loop {
			match self {
				Utf8StringDecoder::Empty => {
					let len = match src.try_get_u16_be() {
						Ok(len) => len as usize,
						Err(_) => return Ok(None),
					};
					*self = Utf8StringDecoder::HaveLength(len);
				},

				Utf8StringDecoder::HaveLength(len) => {
					if src.len() < *len {
						return Ok(None);
					}

					let s = match std::str::from_utf8(&src.split_to(*len)) {
						Ok(s) => s.to_string(),
						Err(err) => return Err(DecodeError::StringNotUtf8(err)),
					};
					*self = Utf8StringDecoder::Empty;
					return Ok(Some(s));
				},
			}
		}
	}
}

fn encode_utf8_str<B>(item: &str, dst: &mut B) -> Result<(), EncodeError> where B: ByteBuf {
	let len = item.len();
	dst.put_u16_bytes(len.try_into().map_err(|_| EncodeError::StringTooLarge(len))?);

	dst.put_slice_bytes(item.as_bytes());

	Ok(())
}

/// A tokio decoder for MQTT-format "remaining length" numbers.
///
/// These numbers are encoded with a variable-length scheme that uses the MSB of each byte as a continuation bit.
///
/// Ref: 2.2.3 Remaining Length
#[derive(Debug)]
pub struct RemainingLengthDecoder {
	result: usize,
	num_bytes_read: usize,
}

impl Default for RemainingLengthDecoder {
	fn default() -> Self {
		RemainingLengthDecoder {
			result: 0,
			num_bytes_read: 0,
		}
	}
}

impl tokio_util::codec::Decoder for RemainingLengthDecoder {
	type Item = usize;
	type Error = DecodeError;

	fn decode(&mut self, src: &mut bytes::BytesMut) -> Result<Option<Self::Item>, Self::Error> {
		loop {
			let encoded_byte = match src.try_get_u8() {
				Ok(encoded_byte) => encoded_byte,
				Err(_) => return Ok(None),
			};

			self.result |= ((encoded_byte & 0x7F) as usize) << (self.num_bytes_read * 7);
			self.num_bytes_read += 1;

			if encoded_byte & 0x80 == 0 {
				let result = self.result;
				*self = Default::default();
				return Ok(Some(result));
			}

			if self.num_bytes_read == 4 {
				return Err(DecodeError::RemainingLengthTooHigh);
			}
		}
	}
}

pub(crate) fn encode_remaining_length<B>(mut item: usize, dst: &mut B) -> Result<(), EncodeError> where B: ByteBuf {
	dst.reserve_bytes(4 * std::mem::size_of::<u8>());

	let original = item;
	let mut num_bytes_written = 0_usize;

	loop {
		#[allow(clippy::cast_possible_truncation)]
		let mut encoded_byte = (item & 0x7F) as u8;

		item >>= 7;

		if item > 0 {
			encoded_byte |= 0x80;
		}

		dst.put_u8_bytes(encoded_byte);
		num_bytes_written += 1;

		if item == 0 {
			break;
		}

		if num_bytes_written == 4 {
			return Err(EncodeError::RemainingLengthTooHigh(original));
		}
	}

	Ok(())
}

/// A packet identifier. Two-byte unsigned integer that cannot be zero.
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd)]
pub struct PacketIdentifier(u16);

impl PacketIdentifier {
	/// Returns the largest value that is a valid packet identifier.
	pub const fn max_value() -> Self {
		PacketIdentifier(u16::max_value())
	}

	/// Convert the given raw packet identifier into this type.
	pub fn new(raw: u16) -> Option<Self> {
		match raw {
			0 => None,
			raw => Some(PacketIdentifier(raw)),
		}
	}

	/// Get the raw packet identifier.
	pub fn get(self) -> u16 {
		self.0
	}
}

impl std::fmt::Display for PacketIdentifier {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		self.0.fmt(f)
	}
}

impl std::ops::Add<u16> for PacketIdentifier {
	type Output = Self;

	fn add(self, other: u16) -> Self::Output {
		PacketIdentifier(match self.0.wrapping_add(other) {
			0 => 1,
			value => value,
		})
	}
}

impl std::ops::AddAssign<u16> for PacketIdentifier {
	fn add_assign(&mut self, other: u16) {
		*self = *self + other;
	}
}

#[derive(Debug)]
pub enum DecodeError {
	ConnectReservedSet,
	IncompletePacket,
	Io(std::io::Error),
	PublishDupAtMostOnce,
	NoTopics,
	RemainingLengthTooHigh,
	StringNotUtf8(std::str::Utf8Error),
	UnrecognizedConnAckFlags(u8),
	UnrecognizedPacket { packet_type: u8, flags: u8, remaining_length: usize },
	UnrecognizedProtocolLevel(u8),
	UnrecognizedProtocolName(String),
	UnrecognizedQoS(u8),
	ZeroPacketIdentifier,
}

impl std::fmt::Display for DecodeError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			DecodeError::ConnectReservedSet => write!(f, "the reserved byte of the CONNECT flags is set"),
			DecodeError::IncompletePacket => write!(f, "packet is truncated"),
			DecodeError::Io(err) => write!(f, "I/O error: {}", err),
			DecodeError::NoTopics => write!(f, "expected at least one topic but there were none"),
			DecodeError::PublishDupAtMostOnce => write!(f, "PUBLISH packet has DUP flag set and QoS 0"),
			DecodeError::RemainingLengthTooHigh => write!(f, "remaining length is too high to be decoded"),
			DecodeError::StringNotUtf8(err) => err.fmt(f),
			DecodeError::UnrecognizedConnAckFlags(flags) => write!(f, "could not parse CONNACK flags 0x{:02X}", flags),
			DecodeError::UnrecognizedPacket { packet_type, flags, remaining_length } =>
				write!(
					f,
					"could not identify packet with type 0x{:1X}, flags 0x{:1X} and remaining length {}",
					packet_type,
					flags,
					remaining_length,
				),
			DecodeError::UnrecognizedProtocolLevel(level) => write!(f, "unexpected protocol level {:?}", level),
			DecodeError::UnrecognizedProtocolName(name) => write!(f, "unexpected protocol name {:?}", name),
			DecodeError::UnrecognizedQoS(qos) => write!(f, "could not parse QoS 0x{:02X}", qos),
			DecodeError::ZeroPacketIdentifier => write!(f, "packet identifier is 0"),
		}
	}
}

impl std::error::Error for DecodeError {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		#[allow(clippy::match_same_arms)]
		match self {
			DecodeError::ConnectReservedSet => None,
			DecodeError::IncompletePacket => None,
			DecodeError::Io(err) => Some(err),
			DecodeError::NoTopics => None,
			DecodeError::PublishDupAtMostOnce => None,
			DecodeError::RemainingLengthTooHigh => None,
			DecodeError::StringNotUtf8(err) => Some(err),
			DecodeError::UnrecognizedConnAckFlags(_) => None,
			DecodeError::UnrecognizedPacket { .. } => None,
			DecodeError::UnrecognizedProtocolLevel(_) => None,
			DecodeError::UnrecognizedProtocolName(_) => None,
			DecodeError::UnrecognizedQoS(_) => None,
			DecodeError::ZeroPacketIdentifier => None,
		}
	}
}

impl From<std::io::Error> for DecodeError {
	fn from(err: std::io::Error) -> Self {
		DecodeError::Io(err)
	}
}

#[derive(Debug)]
pub enum EncodeError {
	Io(std::io::Error),
	KeepAliveTooHigh(std::time::Duration),
	RemainingLengthTooHigh(usize),
	StringTooLarge(usize),
	WillTooLarge(usize),
}

impl EncodeError {
	pub fn is_user_error(&self) -> bool {
		#[allow(clippy::match_same_arms)]
		match self {
			EncodeError::Io(_) => false,
			EncodeError::KeepAliveTooHigh(_) => true,
			EncodeError::RemainingLengthTooHigh(_) => true,
			EncodeError::StringTooLarge(_) => true,
			EncodeError::WillTooLarge(_) => true,
		}
	}
}

impl std::fmt::Display for EncodeError {
	fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
		match self {
			EncodeError::Io(err) => write!(f, "I/O error: {}", err),
			EncodeError::KeepAliveTooHigh(keep_alive) => write!(f, "keep-alive {:?} is too high", keep_alive),
			EncodeError::RemainingLengthTooHigh(len) => write!(f, "remaining length {} is too high to be encoded", len),
			EncodeError::StringTooLarge(len) => write!(f, "string of length {} is too large to be encoded", len),
			EncodeError::WillTooLarge(len) => write!(f, "will payload of length {} is too large to be encoded", len),
		}
	}
}

impl std::error::Error for EncodeError {
	fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
		#[allow(clippy::match_same_arms)]
		match self {
			EncodeError::Io(err) => Some(err),
			EncodeError::KeepAliveTooHigh(_) => None,
			EncodeError::RemainingLengthTooHigh(_) => None,
			EncodeError::StringTooLarge(_) => None,
			EncodeError::WillTooLarge(_) => None,
		}
	}
}

impl From<std::io::Error> for EncodeError {
	fn from(err: std::io::Error) -> Self {
		EncodeError::Io(err)
	}
}

pub(crate) trait ByteBuf {
	fn reserve_bytes(&mut self, additional: usize);

	fn put_u8_bytes(&mut self, n: u8);

	fn put_u16_bytes(&mut self, n: u16);

	fn put_packet_identifier_bytes(&mut self, packet_identifier: PacketIdentifier) {
		self.put_u16_bytes(packet_identifier.0);
	}

	fn put_slice_bytes(&mut self, src: &[u8]);
}

impl ByteBuf for bytes::BytesMut {
	fn reserve_bytes(&mut self, additional: usize) {
		self.reserve(additional);
	}

	fn put_u8_bytes(&mut self, n: u8) {
		self.put_u8(n);
	}

	fn put_u16_bytes(&mut self, n: u16) {
		self.put_u16(n);
	}

	fn put_slice_bytes(&mut self, src: &[u8]) {
		self.put_slice(src);
	}
}

pub(crate) struct ByteCounter(pub(crate) usize);

impl ByteCounter {
	pub(crate) fn new() -> Self {
		ByteCounter(0)
	}
}

impl ByteBuf for ByteCounter {
	fn reserve_bytes(&mut self, _: usize) {
	}

	fn put_u8_bytes(&mut self, _: u8) {
		self.0 += std::mem::size_of::<u8>();
	}

	fn put_u16_bytes(&mut self, _: u16) {
		self.0 += std::mem::size_of::<u16>();
	}

	fn put_slice_bytes(&mut self, src: &[u8]) {
		self.0 += src.len();
	}
}

trait BufMutExt {
	fn get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError>;

	fn try_get_u8(&mut self) -> Result<u8, DecodeError>;
	fn try_get_u16_be(&mut self) -> Result<u16, DecodeError>;
	fn try_get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError>;
}

impl BufMutExt for bytes::BytesMut {
	fn get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError> {
		let packet_identifier = self.get_u16();
		PacketIdentifier::new(packet_identifier).ok_or(DecodeError::ZeroPacketIdentifier)
	}

	fn try_get_u8(&mut self) -> Result<u8, DecodeError> {
		if self.len() < std::mem::size_of::<u8>() {
			return Err(DecodeError::IncompletePacket);
		}

		Ok(self.get_u8())
	}

	fn try_get_u16_be(&mut self) -> Result<u16, DecodeError> {
		if self.len() < std::mem::size_of::<u16>() {
			return Err(DecodeError::IncompletePacket);
		}

		Ok(self.get_u16())
	}

	fn try_get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError> {
		if self.len() < std::mem::size_of::<u16>() {
			return Err(DecodeError::IncompletePacket);
		}

		self.get_packet_identifier()
	}
}

#[cfg(test)]
mod tests {
	#[test]
	fn remaining_length_encode() {
		remaining_length_encode_inner_ok(0x00, &[0x00]);
		remaining_length_encode_inner_ok(0x01, &[0x01]);

		remaining_length_encode_inner_ok(0x7F, &[0x7F]);
		remaining_length_encode_inner_ok(0x80, &[0x80, 0x01]);
		remaining_length_encode_inner_ok(0x3FFF, &[0xFF, 0x7F]);
		remaining_length_encode_inner_ok(0x4000, &[0x80, 0x80, 0x01]);
		remaining_length_encode_inner_ok(0x001F_FFFF, &[0xFF, 0xFF, 0x7F]);
		remaining_length_encode_inner_ok(0x0020_0000, &[0x80, 0x80, 0x80, 0x01]);
		remaining_length_encode_inner_ok(0x0FFF_FFFF, &[0xFF, 0xFF, 0xFF, 0x7F]);

		remaining_length_encode_inner_too_high(0x1000_0000);
		remaining_length_encode_inner_too_high(0xFFFF_FFFF);
		remaining_length_encode_inner_too_high(0xFFFF_FFFF_FFFF_FFFF);
	}

	fn remaining_length_encode_inner_ok(value: usize, expected: &[u8]) {
		// Can encode into an empty buffer
		let mut bytes = bytes::BytesMut::new();
		super::encode_remaining_length(value, &mut bytes).unwrap();
		assert_eq!(&*bytes, expected);

		// Can encode into a partially populated buffer
		let mut bytes: bytes::BytesMut = (&[0x00; 3][..]).into();
		super::encode_remaining_length(value, &mut bytes).unwrap();
		assert_eq!(&bytes[3..], expected);
	}

	fn remaining_length_encode_inner_too_high(value: usize) {
		let mut bytes = bytes::BytesMut::new();
		let err = super::encode_remaining_length(value, &mut bytes).unwrap_err();
		if let super::EncodeError::RemainingLengthTooHigh(v) = err {
			assert_eq!(v, value);
		}
		else {
			panic!("{:?}", err);
		}
	}

	#[test]
	fn remaining_length_decode() {
		remaining_length_decode_inner_ok(&[0x00], 0x00);
		remaining_length_decode_inner_ok(&[0x01], 0x01);

		remaining_length_decode_inner_ok(&[0x7F], 0x7F);
		remaining_length_decode_inner_ok(&[0x80, 0x01], 0x80);
		remaining_length_decode_inner_ok(&[0xFF, 0x7F], 0x3FFF);
		remaining_length_decode_inner_ok(&[0x80, 0x80, 0x01], 0x4000);
		remaining_length_decode_inner_ok(&[0xFF, 0xFF, 0x7F], 0x001F_FFFF);
		remaining_length_decode_inner_ok(&[0x80, 0x80, 0x80, 0x01], 0x0020_0000);
		remaining_length_decode_inner_ok(&[0xFF, 0xFF, 0xFF, 0x7F], 0x0FFF_FFFF);

		// Longer-than-necessary encodings are not disallowed by the spec
		remaining_length_decode_inner_ok(&[0x81, 0x00], 0x01);
		remaining_length_decode_inner_ok(&[0x81, 0x80, 0x00], 0x01);
		remaining_length_decode_inner_ok(&[0x81, 0x80, 0x80, 0x00], 0x01);

		remaining_length_decode_inner_too_high(&[0x80, 0x80, 0x80, 0x80]);
		remaining_length_decode_inner_too_high(&[0xFF, 0xFF, 0xFF, 0xFF]);

		remaining_length_decode_inner_incomplete_packet(&[0x80]);
		remaining_length_decode_inner_incomplete_packet(&[0x80, 0x80]);
		remaining_length_decode_inner_incomplete_packet(&[0x80, 0x80, 0x80]);
	}

	fn remaining_length_decode_inner_ok(bytes: &[u8], expected: usize) {
		use tokio_util::codec::Decoder;

		let mut bytes = bytes::BytesMut::from(bytes);
		let actual = super::RemainingLengthDecoder::default().decode(&mut bytes).unwrap().unwrap();
		assert_eq!(actual, expected);
		assert!(bytes.is_empty());
	}

	fn remaining_length_decode_inner_too_high(bytes: &[u8]) {
		use tokio_util::codec::Decoder;

		let mut bytes = bytes::BytesMut::from(bytes);
		let err = super::RemainingLengthDecoder::default().decode(&mut bytes).unwrap_err();
		if let super::DecodeError::RemainingLengthTooHigh = err {
		}
		else {
			panic!("{:?}", err);
		}
	}

	fn remaining_length_decode_inner_incomplete_packet(bytes: &[u8]) {
		use tokio_util::codec::Decoder;

		let mut bytes = bytes::BytesMut::from(bytes);
		assert_eq!(super::RemainingLengthDecoder::default().decode(&mut bytes).unwrap(), None);
	}
}
