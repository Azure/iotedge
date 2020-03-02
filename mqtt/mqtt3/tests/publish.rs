mod common;

#[test]
fn server_publishes_at_most_once() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::ServerGenerated,
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtMostOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtMostOnce),
				],
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
				packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::AtMostOnce,
				retain: false,
				topic_name: "topic1".to_owned(),
				payload: [0x01, 0x02, 0x03][..].into(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			None,
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtMostOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtMostOnce }),
		]),
		mqtt3::Event::Publication(mqtt3::ReceivedPublication {
			topic_name: "topic1".to_owned(),
			dup: false,
			qos: mqtt3::proto::QoS::AtMostOnce,
			retain: false,
			payload: [0x01, 0x02, 0x03][..].into(),
		}),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn server_publishes_at_least_once() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::ServerGenerated,
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
				],
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
				packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt3::proto::PacketIdentifier::new(2).unwrap(), false),
				retain: false,
				topic_name: "topic1".to_owned(),
				payload: [0x01, 0x02, 0x03][..].into(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PubAck(mqtt3::proto::PubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(2).unwrap(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			None,
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }),
		]),
		mqtt3::Event::Publication(mqtt3::ReceivedPublication {
			topic_name: "topic1".to_owned(),
			dup: false,
			qos: mqtt3::proto::QoS::AtLeastOnce,
			retain: false,
			payload: [0x01, 0x02, 0x03][..].into(),
		}),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn server_publishes_at_least_once_with_reconnect_before_publish() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithCleanSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: true,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
				],
			})),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: true,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
				packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt3::proto::PacketIdentifier::new(1).unwrap(), true),
				retain: false,
				topic_name: "topic1".to_owned(),
				payload: [0x01, 0x02, 0x03][..].into(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PubAck(mqtt3::proto::PubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			Some("client_id".to_owned()),
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::NewConnection { reset_session: false },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }),
		]),
		mqtt3::Event::NewConnection { reset_session: false },
		mqtt3::Event::Publication(mqtt3::ReceivedPublication {
			topic_name: "topic1".to_owned(),
			dup: true,
			qos: mqtt3::proto::QoS::AtLeastOnce,
			retain: false,
			payload: [0x01, 0x02, 0x03][..].into(),
		}),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn server_publishes_at_least_once_with_reconnect_before_ack() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithCleanSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: true,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
				packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt3::proto::PacketIdentifier::new(1).unwrap(), false),
				retain: false,
				topic_name: "topic1".to_owned(),
				payload: [0x01, 0x02, 0x03][..].into(),
			})),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("client_id".to_owned()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: true,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::Publish(mqtt3::proto::Publish {
				packet_identifier_dup_qos: mqtt3::proto::PacketIdentifierDupQoS::AtLeastOnce(mqtt3::proto::PacketIdentifier::new(1).unwrap(), true),
				retain: false,
				topic_name: "topic1".to_owned(),
				payload: [0x01, 0x02, 0x03][..].into(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PubAck(mqtt3::proto::PubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			Some("client_id".to_owned()),
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::NewConnection { reset_session: false },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_owned(), qos: mqtt3::proto::QoS::AtLeastOnce }),
		]),
		mqtt3::Event::Publication(mqtt3::ReceivedPublication {
			topic_name: "topic1".to_owned(),
			dup: false,
			qos: mqtt3::proto::QoS::AtLeastOnce,
			retain: false,
			payload: [0x01, 0x02, 0x03][..].into(),
		}),
		mqtt3::Event::NewConnection { reset_session: false },
		mqtt3::Event::Publication(mqtt3::ReceivedPublication {
			topic_name: "topic1".to_owned(),
			dup: true,
			qos: mqtt3::proto::QoS::AtLeastOnce,
			retain: false,
			payload: [0x01, 0x02, 0x03][..].into(),
		}),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn should_reject_invalid_publications() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::ServerGenerated,
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			None,
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);

	let too_large_topic_name = "a".repeat(usize::from(u16::max_value()) + 1);

	let publish_future = client.publish(mqtt3::proto::Publication {
		topic_name: too_large_topic_name,
		qos: mqtt3::proto::QoS::AtMostOnce,
		retain: false,
		payload: Default::default(),
	});

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
	match runtime.block_on(publish_future) {
		Err(mqtt3::PublishError::EncodePacket(_, mqtt3::proto::EncodeError::StringTooLarge(_))) => (),
		result => panic!("expected client.publish() to fail with EncodePacket(StringTooLarge) but it returned {:?}", result),
	}
}
