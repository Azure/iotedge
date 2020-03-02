mod common;

#[test]
fn server_generated_id_must_always_resubscribe() {
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
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtMostOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
				],
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],

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
				packet_identifier: mqtt3::proto::PacketIdentifier::new(2).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(2).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtMostOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
				],
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
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
		mqtt3::Event::NewConnection { reset_session: false },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn client_id_should_not_resubscribe_when_session_is_present() {
	let mut runtime = tokio::runtime::Builder::new().basic_scheduler().enable_time().build().expect("couldn't initialize tokio runtime");

	let (io_source, done) = common::IoSource::new(vec![
		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithCleanSession("idle_client_id".to_string()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtMostOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
				],
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("idle_client_id".to_string()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				// The clean session bit also determines if the *current* session should be persisted.
				// So when the previous session requested a clean session, the server would not persist *that* session either.
				// So this second session will still have `session_present == false`
				session_present: false,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Subscribe(mqtt3::proto::Subscribe {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(2).unwrap(),
				subscribe_to: vec![
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(2).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtMostOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
				],
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],

		vec![
			common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(mqtt3::proto::Connect {
				username: None,
				password: None,
				will: None,
				client_id: mqtt3::proto::ClientId::IdWithExistingSession("idle_client_id".to_string()),
				keep_alive: std::time::Duration::from_secs(4),
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
				session_present: true,
				return_code: mqtt3::proto::ConnectReturnCode::Accepted,
			})),

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let mut client =
		mqtt3::Client::new(
			Some("idle_client_id".to_string()),
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
		mqtt3::Event::NewConnection { reset_session: false },
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn should_combine_pending_subscription_updates() {
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
					mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce },
					mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce },
				],
			})),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::SubAck(mqtt3::proto::SubAck {
				packet_identifier: mqtt3::proto::PacketIdentifier::new(1).unwrap(),
				qos: vec![
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::AtLeastOnce),
					mqtt3::proto::SubAckQos::Success(mqtt3::proto::QoS::ExactlyOnce),
				],
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
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtMostOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic2".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }).unwrap();
	client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }).unwrap();
	client.unsubscribe("topic2".to_string()).unwrap();

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::SubscriptionUpdates(vec![
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic1".to_string(), qos: mqtt3::proto::QoS::AtLeastOnce }),
			mqtt3::SubscriptionUpdateEvent::Subscribe(mqtt3::proto::SubscribeTo { topic_filter: "topic3".to_string(), qos: mqtt3::proto::QoS::ExactlyOnce }),
		]),
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn should_reject_invalid_subscriptions() {
	let (io_source, _) = common::IoSource::new(vec![]);

	let mut client =
		mqtt3::Client::new(
			None,
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);

	let too_large_topic_filter = "a".repeat(usize::from(u16::max_value()) + 1);

	match client.subscribe(mqtt3::proto::SubscribeTo { topic_filter: too_large_topic_filter.clone(), qos: mqtt3::proto::QoS::AtMostOnce }) {
		Err(mqtt3::UpdateSubscriptionError::EncodePacket(_, mqtt3::proto::EncodeError::StringTooLarge(_))) => (),
		result => panic!("expected client.subscribe() to fail with EncodePacket(StringTooLarge) but it returned {:?}", result),
	}
	match client.unsubscribe(too_large_topic_filter.clone()) {
		Err(mqtt3::UpdateSubscriptionError::EncodePacket(_, mqtt3::proto::EncodeError::StringTooLarge(_))) => (),
		result => panic!("expected client.unsubscribe() to fail with EncodePacket(StringTooLarge) but it returned {:?}", result),
	}
}
