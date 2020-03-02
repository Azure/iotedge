mod common;

#[test]
fn server_generated_id_can_connect_and_idle() {
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

	let client =
		mqtt3::Client::new(
			None,
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::NewConnection { reset_session: true },
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}

#[test]
fn client_id_can_connect_and_idle() {
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

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

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

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),

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

			common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(mqtt3::proto::PingReq)),

			common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(mqtt3::proto::PingResp)),
		],
	]);

	let client =
		mqtt3::Client::new(
			Some("idle_client_id".to_string()),
			None,
			None,
			io_source,
			std::time::Duration::from_secs(0),
			std::time::Duration::from_secs(4),
		);

	common::verify_client_events(&mut runtime, client, vec![
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::NewConnection { reset_session: true },
		mqtt3::Event::NewConnection { reset_session: false },
	]);

	let () = runtime.block_on(done).expect("connection broken while there were still steps remaining on the server");
}
