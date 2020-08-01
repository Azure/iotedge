/// Parses the given file as an MQTT packet and prints it to stdout.
///
/// Also checks that a successfully parsed packet can be encoded and re-decoded successfully.
///
/// Primarily meant to be used to investigate mqtt3-fuzz crashes.
///
/// Example:
///
///     cargo run --example decode -- /path/to/some/raw/mqtt/packet.bin

use std::io::Read;

use bytes::Buf;
use tokio_util::codec::{ Decoder, Encoder };

fn main() -> Result<(), Box<dyn std::error::Error>> {
	let filename = std::env::args_os().nth(1).ok_or("expected one argument set to the name of the file to decode")?;
	let file = std::fs::OpenOptions::new().read(true).open(filename)?;
	let mut file = std::io::BufReader::new(file);

	let mut codec: mqtt3::proto::PacketCodec = Default::default();

	let mut data = vec![];
	file.read_to_end(&mut data)?;
	let mut bytes: bytes::BytesMut = (&*data).into();

	let packet = codec.decode(&mut bytes)?.ok_or("incomplete packet")?;
	println!("{:#?}", packet);

	let input_remaining = bytes.len();
	codec.encode(packet.clone(), &mut bytes)?;
	bytes.advance(input_remaining);

	let packet2 = codec.decode(&mut bytes)?.ok_or("could not decode re-encoded packet")?;
	assert_eq!(packet, packet2);

	if !bytes.is_empty() {
		return Err("leftover bytes".into());
	}

	codec.encode(packet.clone(), &mut bytes)?;

	let packet2 = codec.decode(&mut bytes)?.ok_or("could not decode re-encoded packet")?;
	assert_eq!(packet, packet2);

	Ok(())
}
