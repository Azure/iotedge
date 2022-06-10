///     rm -rf out/ && cargo afl build && cargo afl fuzz -i in -o out target/debug/mqtt3-fuzz
use bytes::Buf;
use tokio_util::codec::{Decoder, Encoder};

fn main() {
    afl::fuzz(true, |data| {
        let mut codec: mqtt3::proto::PacketCodec = Default::default();

        let mut bytes: bytes::BytesMut = data.into();

        if let Ok(Some(packet)) = codec.decode(&mut bytes) {
            // Encode and re-decode the parsed packet, and assert that it matches the originally decoded packet
            //
            // We do this rather than just encoding the parsed packet and comparing the raw bytes
            // because there is variance in the representation of "remaining lengths"
            //
            // So the fuzzer can generate a remaining length like `0x81 0x00` which would get re-encoded as `0x01`
            // and not match the input.

            // Re-encode the decoded packet at the end of the input buffer.
            // This simulates encoding at the end of a partially populated output buffer.
            let input_remaining = bytes.len();
            codec.encode(packet.clone(), &mut bytes).unwrap();
            bytes.advance(input_remaining);

            let packet2 = codec.decode(&mut bytes).unwrap().unwrap();
            assert_eq!(packet, packet2);

            assert!(bytes.is_empty());

            // Another encode-decode cycle, this time with an initially empty buffer.
            codec.encode(packet.clone(), &mut bytes).unwrap();

            let packet2 = codec.decode(&mut bytes).unwrap().unwrap();
            assert_eq!(packet, packet2);
        }
    })
}
