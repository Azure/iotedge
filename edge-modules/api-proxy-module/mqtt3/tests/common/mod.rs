use std::future::Future;

pub(crate) fn verify_client_events(
	runtime: &mut tokio::runtime::Runtime,
	mut client: mqtt3::Client<IoSource>,
	expected: Vec<mqtt3::Event>,
) {
	let mut expected = expected.into_iter();

	runtime.spawn(async move {
		use futures_util::StreamExt;

		while let Some(event) = client.next().await {
			let event = event.unwrap();
			assert_eq!(expected.next(), Some(event));
		}
	});
}

/// An `mqtt3::IoSource` impl suitable for use with an `mqtt3::Client`. The IoSource pretends to provide connections
/// to a real MQTT server.
#[derive(Debug)]
pub(crate) struct IoSource(std::vec::IntoIter<TestConnection>);

impl IoSource {
	/// Each element of `server_steps` represents a single connection between the client and server. The element contains
	/// an ordered sequence of what packets the server expects to send or receive in that connection.
	///
	/// When the connection is broken by the server, the current element is dropped. When the client reconnects,
	/// it is served based on the next element.
	///
	/// The second value returned by this function is a future that resolves when all connections have been dropped,
	/// *and* each connection's packets were used up completely before that connection was dropped.
	/// If any connection is dropped before its packets have been used up, the future will resolve to an error.
	pub(crate) fn new(
		server_steps: Vec<Vec<TestConnectionStep<mqtt3::proto::Packet, mqtt3::proto::Packet>>>,
	) -> (Self, impl Future<Output = Result<(), futures_channel::oneshot::Canceled>>) {
		use futures_util::TryFutureExt;
		use tokio_util::codec::Encoder;

		let (connections, done): (Vec<_>, Vec<_>) =
			server_steps.into_iter()
			.map(|server_steps| {
				let steps =
					server_steps.into_iter()
					.map(|step| match step {
						TestConnectionStep::Receives(packet) => TestConnectionStep::Receives((packet, bytes::BytesMut::new())),

						TestConnectionStep::Sends(packet) => {
							let mut packet_codec: mqtt3::proto::PacketCodec = Default::default();
							let mut bytes = bytes::BytesMut::new();
							packet_codec.encode(packet.clone(), &mut bytes).unwrap();
							TestConnectionStep::Sends((packet, std::io::Cursor::new(bytes)))
						},
					})
					.collect();

				let (done_send, done_recv) = futures_channel::oneshot::channel();

				(
					TestConnection {
						steps,
						done_send: Some(done_send),
					},
					done_recv,
				)
			})
			.unzip();

		let done = futures_util::future::try_join_all(done).map_ok(|_: Vec<()>| ());

		(IoSource(connections.into_iter()), done)
	}
}

impl mqtt3::IoSource for IoSource {
	type Io = TestConnection;
	type Error = std::io::Error;
	type Future = std::pin::Pin<Box<dyn Future<Output = std::io::Result<(Self::Io, Option<String>)>> + Send>>;

	fn connect(&mut self) -> Self::Future {
		println!("client is creating new connection");

		if let Some(io) = self.0.next() {
			Box::pin(async {
				Ok((io, None))
			})
		}
		else {
			// The client drops the previous Io (TestConnection) before requesting a new one from the IoSource.
			// Dropping the TestConnection would have dropped the futures_channel::oneshot::Sender inside it.
			//
			// If the client is requesting a new Io because the last TestConnection ran out of steps and broke the previous connection,
			// then that TestConnection would've already used its sender to signal the future held by the test.
			// We can just delay a bit here till the test receives the signal and exits.
			//
			// If the connection broke while there were still steps remaining in the TestConnection, then the dropped sender will cause the test
			// to receive a futures_channel::oneshot::Canceled error, so the test will panic before this deadline elapses anyway.
			Box::pin(async {
				let () = tokio::time::delay_for(std::time::Duration::from_secs(5)).await;
				unreachable!();
			})
		}
	}
}

/// A single connection between a client and a server
#[derive(Debug)]
pub(crate) struct TestConnection {
	steps: std::collections::VecDeque<TestConnectionStep<
		(mqtt3::proto::Packet, bytes::BytesMut),
		(mqtt3::proto::Packet, std::io::Cursor<bytes::BytesMut>),
	>>,
	done_send: Option<futures_channel::oneshot::Sender<()>>,
}

/// A single step in the connection between a client and a server
#[derive(Debug)]
pub(crate) enum TestConnectionStep<TReceives, TSends> {
	Receives(TReceives),
	Sends(TSends),
}

impl tokio::io::AsyncRead for TestConnection {
	fn poll_read(mut self: std::pin::Pin<&mut Self>, _cx: &mut std::task::Context<'_>, buf: &mut [u8]) -> std::task::Poll<std::io::Result<usize>> {
		let (read, step_done) = match self.steps.front_mut() {
			Some(TestConnectionStep::Receives(_)) => {
				println!("client is reading from server but server wants to receive something first");

				// Since the TestConnection always makes progress with either Read or Write, we don't need to register for wakeup here.
				return std::task::Poll::Pending;
			},

			Some(TestConnectionStep::Sends((packet, cursor))) => {
				println!("server sends {:?}", packet);
				let read = std::io::Read::read(cursor, buf)?;
				(read, cursor.position() == cursor.get_ref().len() as u64)
			},

			None => {
				if let Some(done_send) = self.done_send.take() {
					done_send.send(()).unwrap();
				}

				(0, false)
			},
		};

		if step_done {
			let _ = self.steps.pop_front();
		}

		println!("client read {} bytes from server", read);

		std::task::Poll::Ready(Ok(read))
	}
}

impl tokio::io::AsyncWrite for TestConnection {
	fn poll_write(mut self: std::pin::Pin<&mut Self>, _cx: &mut std::task::Context<'_>, buf: &[u8]) -> std::task::Poll<std::io::Result<usize>> {
		use tokio_util::codec::Decoder;

		let (written, step_done) = match self.steps.front_mut() {
			Some(TestConnectionStep::Receives((expected_packet, bytes))) => {
				println!("server expects to receive {:?}", expected_packet);

				let previous_bytes_len = bytes.len();

				bytes.extend_from_slice(buf);

				let mut packet_codec: mqtt3::proto::PacketCodec = Default::default();
				match packet_codec.decode(bytes) {
					Ok(Some(actual_packet)) => {
						// Codec will remove the bytes it's parsed successfully, so whatever's left is what didn't get parsed
						let written = previous_bytes_len + buf.len() - bytes.len();

						println!("server received {:?}", actual_packet);
						assert_eq!(*expected_packet, actual_packet);

						(written, true)
					},

					Ok(None) => (buf.len(), false),

					Err(err) => panic!("{:?}", err),
				}
			},

			Some(TestConnectionStep::Sends(_)) => {
				println!("client is writing to server but server wants to send something first");

				// Since the TestConnection always makes progress with either Read or Write, we don't need to register for wakeup here.
				return std::task::Poll::Pending;
			},

			None => {
				if let Some(done_send) = self.done_send.take() {
					done_send.send(()).unwrap();
				}

				(0, false)
			},
		};

		if step_done {
			let _ = self.steps.pop_front();
		}

		println!("client wrote {} bytes to server", written);

		std::task::Poll::Ready(Ok(written))
	}

	fn poll_flush(self: std::pin::Pin<&mut Self>, _cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		std::task::Poll::Ready(Ok(()))
	}

	fn poll_shutdown(self: std::pin::Pin<&mut Self>, _cx: &mut std::task::Context<'_>) -> std::task::Poll<std::io::Result<()>> {
		std::task::Poll::Ready(Ok(()))
	}
}
