use std::{
    pin::Pin,
    task::{Context, Poll},
    time::{Duration, Instant},
};

use futures_util::{sink::SinkExt, stream::Stream, StreamExt};
use lazy_static::lazy_static;
use tokio::net::{TcpStream, ToSocketAddrs};
use tokio_io_timeout::TimeoutStream;
use tokio_util::codec::Framed;

use mqtt3::{
    proto::{ClientId, Connect, Packet, PacketCodec, Publication, Publish, Subscribe},
    PROTOCOL_LEVEL, PROTOCOL_NAME,
};

lazy_static! {
    static ref DEFAULT_TIMEOUT: Duration = Duration::from_secs(5);
}

/// A simple wrapper around `TcpStream` + `PacketCodec` to send specific packets
/// to a broker for more granular integration testing.
#[derive(Debug)]
pub struct PacketStream {
    codec: Pin<Box<Framed<TimeoutStream<TcpStream>, PacketCodec>>>,
}

#[allow(dead_code)]
impl PacketStream {
    /// Creates a client and opens TCP connection to the server.
    /// No MQTT packets are sent at this moment.
    pub async fn open(server_addr: impl ToSocketAddrs) -> Self {
        // broker may not be available immediately in the test,
        // so we'll try to connect for some time.
        let mut result = TcpStream::connect(&server_addr).await;
        let start_time = Instant::now();
        while result.is_err() {
            tokio::time::sleep(Duration::from_millis(100)).await;
            if start_time.elapsed() > *DEFAULT_TIMEOUT {
                break;
            }
            result = TcpStream::connect(&server_addr).await;
        }

        let tcp_stream = result.expect("unable to establish tcp connection");
        let mut timeout = TimeoutStream::new(tcp_stream);

        timeout.set_read_timeout(Some(*DEFAULT_TIMEOUT));
        timeout.set_write_timeout(Some(*DEFAULT_TIMEOUT));

        let codec = Box::pin(Framed::new(timeout, PacketCodec::default()));

        Self { codec }
    }

    /// Creates a client, opens TCP connection to the server,
    /// and sends CONNECT packet.
    pub async fn connect(
        client_id: ClientId,
        server_addr: impl ToSocketAddrs,
        username: Option<String>,
        password: Option<String>,
        will: Option<Publication>,
    ) -> Self {
        let mut client = Self::open(server_addr).await;
        client
            .send_connect(Connect {
                username,
                password,
                client_id,
                will,
                keep_alive: Duration::from_secs(30),
                protocol_name: PROTOCOL_NAME.into(),
                protocol_level: PROTOCOL_LEVEL,
            })
            .await;
        client
    }

    pub async fn send_connect(&mut self, connect: Connect) {
        self.send_packet(Packet::Connect(connect)).await;
    }

    pub async fn send_publish(&mut self, publish: Publish) {
        self.send_packet(Packet::Publish(publish)).await;
    }

    pub async fn send_subscribe(&mut self, subscribe: Subscribe) {
        self.send_packet(Packet::Subscribe(subscribe)).await;
    }

    pub async fn send_packet(&mut self, packet: Packet) {
        self.codec
            .send(packet)
            .await
            .expect("Unable to send a packet");
    }
}

impl Stream for PacketStream {
    type Item = Packet;

    fn poll_next(
        mut self: std::pin::Pin<&mut Self>,
        cx: &mut Context<'_>,
    ) -> Poll<Option<Self::Item>> {
        match self.codec.poll_next_unpin(cx) {
            Poll::Ready(Some(result)) => {
                Poll::Ready(Some(result.expect("Error decoding incoming packet")))
            }
            Poll::Ready(None) => Poll::Ready(None),
            Poll::Pending => Poll::Pending,
        }
    }
}
