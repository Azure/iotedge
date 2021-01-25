use std::future::Future;

use pin_project::pin_project;

#[pin_project]
#[derive(Debug)]
pub(super) struct Connect<IoS>
where
    IoS: super::IoSource,
{
    io_source: IoS,
    max_back_off: std::time::Duration,
    current_back_off: std::time::Duration,

    #[pin]
    state: State<IoS>,
}

#[pin_project(project = StateProj)]
enum State<IoS>
where
    IoS: super::IoSource,
{
    BeginBackOff,
    EndBackOff(#[pin] tokio::time::Sleep),
    BeginConnecting,
    WaitingForIoToConnect(<IoS as super::IoSource>::Future),
    Framed {
        framed: crate::logging_framed::LoggingFramed<<IoS as super::IoSource>::Io>,
        framed_state: FramedState,
        password: Option<String>,
    },
}

#[derive(Clone, Copy, Debug)]
enum FramedState {
    BeginSendingConnect,
    EndSendingConnect,
    WaitingForConnAck,
    Connected {
        new_connection: bool,
        reset_session: bool,
    },
}

impl<IoS> std::fmt::Debug for State<IoS>
where
    IoS: super::IoSource,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            State::BeginBackOff => f.write_str("BeginBackOff"),
            State::EndBackOff(_) => f.write_str("EndBackOff"),
            State::BeginConnecting => f.write_str("BeginConnecting"),
            State::WaitingForIoToConnect(_) => f.write_str("WaitingForIoToConnect"),
            State::Framed { framed_state, .. } => f
                .debug_struct("Framed")
                .field("framed_state", framed_state)
                .finish(),
        }
    }
}

impl<IoS> Connect<IoS>
where
    IoS: super::IoSource,
{
    pub(super) fn new(io_source: IoS, max_back_off: std::time::Duration) -> Self {
        Connect {
            io_source,
            max_back_off,
            current_back_off: std::time::Duration::from_secs(0),
            state: State::BeginConnecting,
        }
    }

    pub(super) fn reconnect(&mut self) {
        self.state = State::BeginBackOff;
    }

    pub(super) fn reconnect_pin(mut self: std::pin::Pin<&mut Self>) {
        self.set_state(State::BeginBackOff);
    }

    fn set_state(self: &mut std::pin::Pin<&mut Self>, state: State<IoS>) {
        self.as_mut().project().state.set(state);
    }
}

impl<IoS> Connect<IoS>
where
    IoS: super::IoSource,
    <IoS as super::IoSource>::Io: Unpin,
    <IoS as super::IoSource>::Error: std::fmt::Display,
    <IoS as super::IoSource>::Future: Unpin,
{
    pub(super) fn poll<'a>(
        mut self: std::pin::Pin<&'a mut Self>,
        cx: &mut std::task::Context<'_>,

        username: Option<&str>,
        will: Option<&crate::proto::Publication>,
        client_id: &mut crate::proto::ClientId,
        keep_alive: std::time::Duration,
    ) -> std::task::Poll<Connected<'a, IoS>> {
        use futures_core::Stream;
        use futures_sink::Sink;

        loop {
            // log::trace!("    {:?}", state);

            let this = self.as_mut();

            match this.project().state.as_mut().project() {
                StateProj::BeginBackOff => match self.current_back_off {
                    back_off if back_off.as_secs() == 0 => {
                        self.current_back_off = std::time::Duration::from_secs(1);
                        self.set_state(State::BeginConnecting);
                    }

                    back_off => {
                        log::debug!("Backing off for {:?}", back_off);
                        self.current_back_off =
                            std::cmp::min(self.max_back_off, self.current_back_off * 2);
                        self.set_state(State::EndBackOff(tokio::time::sleep(back_off)));
                    }
                },

                StateProj::EndBackOff(back_off_timer) => match back_off_timer.poll(cx) {
                    std::task::Poll::Ready(()) => self.set_state(State::BeginConnecting),
                    std::task::Poll::Pending => return std::task::Poll::Pending,
                },

                StateProj::BeginConnecting => {
                    let io = self.io_source.connect();
                    self.set_state(State::WaitingForIoToConnect(io))
                }

                StateProj::WaitingForIoToConnect(io) => match std::pin::Pin::new(io).poll(cx) {
                    std::task::Poll::Ready(Ok((io, password))) => {
                        let framed = crate::logging_framed::LoggingFramed::new(io);
                        let state = State::Framed {
                            framed,
                            framed_state: FramedState::BeginSendingConnect,
                            password,
                        };
                        self.set_state(state);
                    }

                    std::task::Poll::Ready(Err(err)) => {
                        log::warn!("could not connect to server: {}", err);
                        self.set_state(State::BeginBackOff);
                    }

                    std::task::Poll::Pending => return std::task::Poll::Pending,
                },

                StateProj::Framed {
                    framed,
                    framed_state: framed_state @ FramedState::BeginSendingConnect,
                    password,
                } => match std::pin::Pin::new(&mut *framed).poll_ready(cx) {
                    std::task::Poll::Ready(Ok(())) => {
                        let packet = crate::proto::Packet::Connect(crate::proto::Connect {
                            username: username.map(ToOwned::to_owned),
                            password: password.clone(),
                            will: will.cloned(),
                            client_id: client_id.clone(),
                            keep_alive,
                            protocol_name: crate::PROTOCOL_NAME.to_string(),
                            protocol_level: crate::PROTOCOL_LEVEL,
                        });

                        match std::pin::Pin::new(&mut *framed).start_send(packet) {
                            Ok(()) => *framed_state = FramedState::EndSendingConnect,
                            Err(err) => {
                                log::warn!("could not connect to server: {}", err);
                                self.set_state(State::BeginBackOff);
                            }
                        }
                    }

                    std::task::Poll::Ready(Err(err)) => {
                        log::warn!("could not connect to server: {}", err);
                        self.set_state(State::BeginBackOff);
                    }

                    std::task::Poll::Pending => return std::task::Poll::Pending,
                },

                StateProj::Framed {
                    framed,
                    framed_state: framed_state @ FramedState::EndSendingConnect,
                    ..
                } => match std::pin::Pin::new(framed).poll_flush(cx) {
                    std::task::Poll::Ready(Ok(())) => {
                        *framed_state = FramedState::WaitingForConnAck
                    }
                    std::task::Poll::Ready(Err(err)) => {
                        log::warn!("could not connect to server: {}", err);
                        self.set_state(State::BeginBackOff);
                    }
                    std::task::Poll::Pending => return std::task::Poll::Pending,
                },

                StateProj::Framed {
                    framed,
                    framed_state: framed_state @ FramedState::WaitingForConnAck,
                    ..
                } => match std::pin::Pin::new(framed).poll_next(cx) {
                    std::task::Poll::Ready(Some(Ok(packet))) => match packet {
                        crate::proto::Packet::ConnAck(crate::proto::ConnAck {
                            session_present,
                            return_code: crate::proto::ConnectReturnCode::Accepted,
                        }) => {
                            self.current_back_off = std::time::Duration::from_secs(0);

                            let reset_session = match client_id {
                                crate::proto::ClientId::ServerGenerated => true,
                                crate::proto::ClientId::IdWithCleanSession(id) => {
                                    *client_id = crate::proto::ClientId::IdWithExistingSession(
                                        std::mem::take(id),
                                    );
                                    true
                                }
                                crate::proto::ClientId::IdWithExistingSession(id) => {
                                    *client_id = crate::proto::ClientId::IdWithExistingSession(
                                        std::mem::take(id),
                                    );
                                    !session_present
                                }
                            };

                            *framed_state = FramedState::Connected {
                                new_connection: true,
                                reset_session,
                            };
                        }

                        crate::proto::Packet::ConnAck(crate::proto::ConnAck {
                            return_code: crate::proto::ConnectReturnCode::Refused(return_code),
                            ..
                        }) => {
                            log::warn!(
                                "could not connect to server: connection refused: {:?}",
                                return_code
                            );
                            self.set_state(State::BeginBackOff);
                        }

                        packet => {
                            log::warn!("could not connect to server: expected to receive ConnAck but received {:?}", packet);
                            self.set_state(State::BeginBackOff);
                        }
                    },

                    std::task::Poll::Ready(Some(Err(err))) => {
                        log::warn!("could not connect to server: {}", err);
                        self.set_state(State::BeginBackOff);
                    }

                    std::task::Poll::Ready(None) => {
                        log::warn!("could not connect to server: connection closed by server");
                        self.set_state(State::BeginBackOff);
                    }

                    std::task::Poll::Pending => return std::task::Poll::Pending,
                },

                StateProj::Framed {
                    framed,
                    framed_state:
                        FramedState::Connected {
                            new_connection,
                            reset_session,
                        },
                    ..
                } => {
                    let result = Connected {
                        framed,
                        new_connection: *new_connection,
                        reset_session: *reset_session,
                    };
                    *new_connection = false;
                    *reset_session = false;
                    return std::task::Poll::Ready(result);
                }
            }
        }
    }
}

pub(super) struct Connected<'a, IoS>
where
    IoS: super::IoSource,
{
    pub(super) framed: &'a mut crate::logging_framed::LoggingFramed<<IoS as super::IoSource>::Io>,
    pub(super) new_connection: bool,
    pub(super) reset_session: bool,
}
