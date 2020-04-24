#![allow(clippy::let_unit_value)]

use futures_util::{future, StreamExt};

mod common;

#[tokio::test]
async fn server_generated_id_can_connect_and_idle() {
    let (io_source, done) = common::IoSource::new(vec![
        vec![
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
                mqtt3::proto::Connect {
                    username: None,
                    password: None,
                    will: None,
                    client_id: mqtt3::proto::ClientId::ServerGenerated,
                    keep_alive: std::time::Duration::from_secs(4),
                    protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                    protocol_level: mqtt3::PROTOCOL_LEVEL,
                },
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(
                mqtt3::proto::ConnAck {
                    session_present: false,
                    return_code: mqtt3::proto::ConnectReturnCode::Accepted,
                },
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
        ],
        vec![
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
                mqtt3::proto::Connect {
                    username: None,
                    password: None,
                    will: None,
                    client_id: mqtt3::proto::ClientId::ServerGenerated,
                    keep_alive: std::time::Duration::from_secs(4),
                    protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                    protocol_level: mqtt3::PROTOCOL_LEVEL,
                },
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(
                mqtt3::proto::ConnAck {
                    session_present: false,
                    return_code: mqtt3::proto::ConnectReturnCode::Accepted,
                },
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
        ],
    ]);

    let client = mqtt3::Client::new(
        None,
        None,
        None,
        io_source,
        std::time::Duration::from_secs(0),
        std::time::Duration::from_secs(4),
    );

    let verify = common::verify_client_events(
        client,
        vec![
            mqtt3::Event::NewConnection {
                reset_session: true,
            },
            mqtt3::Event::NewConnection {
                reset_session: true,
            },
        ],
    );

    assert!(matches!(future::join(verify, done).await, (Ok(()), _)));
}

#[tokio::test]
async fn client_id_can_connect_and_idle() {
    let (io_source, done) = common::IoSource::new(vec![
        vec![
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
                mqtt3::proto::Connect {
                    username: None,
                    password: None,
                    will: None,
                    client_id: mqtt3::proto::ClientId::IdWithCleanSession(
                        "idle_client_id".to_string(),
                    ),
                    keep_alive: std::time::Duration::from_secs(4),
                    protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                    protocol_level: mqtt3::PROTOCOL_LEVEL,
                },
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(
                mqtt3::proto::ConnAck {
                    session_present: false,
                    return_code: mqtt3::proto::ConnectReturnCode::Accepted,
                },
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
        ],
        vec![
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
                mqtt3::proto::Connect {
                    username: None,
                    password: None,
                    will: None,
                    client_id: mqtt3::proto::ClientId::IdWithExistingSession(
                        "idle_client_id".to_string(),
                    ),
                    keep_alive: std::time::Duration::from_secs(4),
                    protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                    protocol_level: mqtt3::PROTOCOL_LEVEL,
                },
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(
                mqtt3::proto::ConnAck {
                    // The clean session bit also determines if the *current* session should be persisted.
                    // So when the previous session requested a clean session, the server would not persist *that* session either.
                    // So this second session will still have `session_present == false`
                    session_present: false,
                    return_code: mqtt3::proto::ConnectReturnCode::Accepted,
                },
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
        ],
        vec![
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
                mqtt3::proto::Connect {
                    username: None,
                    password: None,
                    will: None,
                    client_id: mqtt3::proto::ClientId::IdWithExistingSession(
                        "idle_client_id".to_string(),
                    ),
                    keep_alive: std::time::Duration::from_secs(4),
                    protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                    protocol_level: mqtt3::PROTOCOL_LEVEL,
                },
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(
                mqtt3::proto::ConnAck {
                    session_present: true,
                    return_code: mqtt3::proto::ConnectReturnCode::Accepted,
                },
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
            common::TestConnectionStep::Receives(mqtt3::proto::Packet::PingReq(
                mqtt3::proto::PingReq,
            )),
            common::TestConnectionStep::Sends(mqtt3::proto::Packet::PingResp(
                mqtt3::proto::PingResp,
            )),
        ],
    ]);

    let client = mqtt3::Client::new(
        Some("idle_client_id".to_string()),
        None,
        None,
        io_source,
        std::time::Duration::from_secs(0),
        std::time::Duration::from_secs(4),
    );

    let verify = common::verify_client_events(
        client,
        vec![
            mqtt3::Event::NewConnection {
                reset_session: true,
            },
            mqtt3::Event::NewConnection {
                reset_session: true,
            },
            mqtt3::Event::NewConnection {
                reset_session: false,
            },
        ],
    );

    assert!(matches!(future::join(verify, done).await, (Ok(()), _)));
}

#[tokio::test]
async fn server_generate_id_cannot_obtain_session_state_when_stopped() {
    let _ = env_logger::builder()
        .is_test(true)
        .filter_level(log::LevelFilter::Debug)
        .init();

    let (io_source, done) = common::IoSource::new(vec![vec![
        common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
            mqtt3::proto::Connect {
                username: None,
                password: None,
                will: None,
                client_id: mqtt3::proto::ClientId::ServerGenerated,
                keep_alive: std::time::Duration::from_secs(4),
                protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                protocol_level: mqtt3::PROTOCOL_LEVEL,
            },
        )),
        common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
            session_present: false,
            return_code: mqtt3::proto::ConnectReturnCode::Accepted,
        })),
        common::TestConnectionStep::Receives(mqtt3::proto::Packet::Disconnect(
            mqtt3::proto::Disconnect,
        )),
    ]]);

    let mut client = mqtt3::Client::new(
        None,
        None,
        None,
        io_source,
        std::time::Duration::from_secs(0),
        std::time::Duration::from_secs(4),
    );

    loop {
        if let Some(event) = client.next().await {
            match event {
                Ok(mqtt3::Event::NewConnection { reset_session }) => {
                    assert!(reset_session);
                    break;
                }
                _ => continue,
            }
        }
    }

    // assert_eq!(
    //     client.next().await.expect("next").unwrap(),
    //     mqtt3::Event::NewConnection {
    //         reset_session: true,
    //     }
    // );

    let mut handle = client.shutdown_handle().expect("shutdown");
    tokio::spawn(async move { handle.shutdown().await });

    // assert_eq!(
    //     client.next().await.expect("next").unwrap(),
    //     mqtt3::Event::Stopped(None),
    // );

    loop {
        if let Some(event) = client.next().await {
            match event {
                Ok(mqtt3::Event::Stopped(state)) => {
                    assert_eq!(state, None);
                    break;
                }
                _ => continue,
            }
        }
    }

    assert!(matches!(client.next().await, None));

    assert_eq!(done.await, Ok(()));
}

#[tokio::test]
async fn client_id_can_obtain_session_state_when_stopped() {
    let (io_source, done) = common::IoSource::new(vec![vec![
        common::TestConnectionStep::Receives(mqtt3::proto::Packet::Connect(
            mqtt3::proto::Connect {
                username: None,
                password: None,
                will: None,
                client_id: mqtt3::proto::ClientId::IdWithExistingSession("client_id".to_string()),
                keep_alive: std::time::Duration::from_secs(4),
                protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
                protocol_level: mqtt3::PROTOCOL_LEVEL,
            },
        )),
        common::TestConnectionStep::Sends(mqtt3::proto::Packet::ConnAck(mqtt3::proto::ConnAck {
            session_present: false,
            return_code: mqtt3::proto::ConnectReturnCode::Accepted,
        })),
        common::TestConnectionStep::Receives(mqtt3::proto::Packet::Disconnect(
            mqtt3::proto::Disconnect,
        )),
    ]]);

    let state = mqtt3::SessionState {
        client_id: "client_id".into(),
        username: None,
        will: None,
        publications: mqtt3::Publications::default(),
        subscriptions: mqtt3::Subscriptions::default(),
    };

    let mut client = mqtt3::Client::from_state(
        state,
        io_source,
        std::time::Duration::from_secs(0),
        std::time::Duration::from_secs(4),
    );

    assert_eq!(
        client.next().await.expect("next").unwrap(),
        mqtt3::Event::NewConnection {
            reset_session: true,
        }
    );

    let mut handle = client.shutdown_handle().expect("shutdown");
    tokio::spawn(async move { handle.shutdown().await });

    assert_eq!(
        client.next().await.expect("next").unwrap(),
        mqtt3::Event::Stopped(Some(mqtt3::SessionState {
            client_id: "client_id".into(),
            username: None,
            will: None,
            publications: mqtt3::Publications {
                publish_requests_waiting_to_be_sent: Default::default(),
                waiting_to_be_acked: Default::default(),
                waiting_to_be_released: Default::default(),
                waiting_to_be_completed: Default::default(),
            },
            subscriptions: mqtt3::Subscriptions {
                subscriptions: std::collections::BTreeMap::default(),
                subscription_updates_waiting_to_be_sent: std::collections::VecDeque::default(),
                subscription_updates_waiting_to_be_acked: std::collections::VecDeque::default(),
            }
        })),
    );

    assert!(matches!(client.next().await, None));

    assert_eq!(done.await, Ok(()));
}
