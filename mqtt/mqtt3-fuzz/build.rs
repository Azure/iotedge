/// Writes the inputs for the fuzzer
use std::io::Write;

use tokio_util::codec::Encoder;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let in_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("in");
    if in_dir.exists() {
        std::fs::remove_dir_all(&in_dir)?;
    }
    std::fs::create_dir(&in_dir)?;

    let packets = vec![
        (
            "connack",
            mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
                session_present: true,
                return_code: mqtt3::proto::ConnectReturnCode::Accepted,
            }),
        ),
        (
            "connect",
            mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
                username: Some("username".to_string()),
                password: Some("password".to_string()),
                will: Some(mqtt3::proto::Publication {
                    topic_name: "will-topic".to_string(),
                    qos: mqtt3::proto::QoS::ExactlyOnce,
                    retain: true,
                    payload: b"\x00\x01\x02\xFF\xFE\xFD"[..].into(),
                }),
                client_id: mqtt3::proto::ClientId::IdWithExistingSession("id".to_string()),
                keep_alive: std::time::Duration::from_secs(5),
                protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                protocol_level: mqtt3::PROTOCOL_LEVEL,
            }),
        ),
        (
            "disconnect",
            mqtt3::proto::Packet::Disconnect(mqtt3::proto::Disconnect),
        ),
        (
            "pingreq",
            mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq),
        ),
        (
            "pingresp",
            mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp),
        ),
        (
            "puback",
            mqtt3::proto::Packet::PubAck(mqtt3::proto::PubAck {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
            }),
        ),
        (
            "pubcomp",
            mqtt3::proto::Packet::PubComp(mqtt3::proto::PubComp {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
            }),
        ),
        (
            "publish",
            mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
                packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::ExactlyOnce(
                    mqtt3::proto::PacketIdentifier::new(5).unwrap(),
                    true,
                ),
                retain: true,
                topic_name: "publish-topic".to_string(),
                payload: b"\x00\x01\x02\xFF\xFE\xFD"[..].into(),
            }),
        ),
        (
            "pubrec",
            mqtt3::proto::Packet::PubRec(mqtt3::proto::PubRec {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
            }),
        ),
        (
            "pubrel",
            mqtt3::proto::Packet::PubRel(mqtt3::proto::PubRel {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
            }),
        ),
        (
            "suback",
            mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
                qos: vec![
                    mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
                    mqtt3::proto::SubAckQos::Failure,
                ],
            }),
        ),
        (
            "subscribe",
            mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
                subscribe_to: vec![mqtt3::proto::SubscribeTo {
                    topic_filter: "subscribe-topic".to_string(),
                    qos: mqtt3::proto::QoS::ExactlyOnce,
                }],
            }),
        ),
        (
            "unsuback",
            mqtt3::proto::Packet::UnsubAck(mqtt3::proto::UnsubAck {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
            }),
        ),
        (
            "unsubscribe",
            mqtt3::proto::Packet::Unsubscribe(mqtt3::proto::Unsubscribe {
                packet_identifier: mqtt3::proto::PacketIdentifier::new(5).unwrap(),
                unsubscribe_from: vec!["unsubscribe-topic".to_string()],
            }),
        ),
    ];

    for (filename, packet) in packets {
        let file = std::fs::OpenOptions::new()
            .create(true)
            .write(true)
            .open(in_dir.join(filename))?;
        let mut file = std::io::BufWriter::new(file);

        let mut codec: mqtt3::proto::PacketCodec = Default::default();

        let mut bytes = bytes::BytesMut::new();

        codec.encode(packet, &mut bytes)?;

        file.write_all(&bytes)?;

        file.flush()?;
    }

    Ok(())
}
