pub(super) enum State {
    BeginWaitingForNextPing,
    WaitingForNextPing(std::pin::Pin<Box<tokio::time::Sleep>>),
}

impl State {
    pub(super) fn poll(
        &mut self,
        cx: &mut std::task::Context<'_>,

        packet: &mut Option<crate::proto::Packet>,
        keep_alive: std::time::Duration,
    ) -> Option<crate::proto::Packet> {
        if let Some(crate::proto::Packet::PingResp(crate::proto::PingResp)) = packet {
            let _ping = packet.take();

            match self {
                State::BeginWaitingForNextPing => (),
                State::WaitingForNextPing(ping_timer) => ping_timer
                    .as_mut()
                    .reset(deadline(tokio::time::Instant::now(), keep_alive)),
            }
        }

        loop {
            log::trace!("    {:?}", self);

            match self {
                State::BeginWaitingForNextPing => {
                    let ping_timer = tokio::time::sleep(keep_alive);
                    *self = State::WaitingForNextPing(Box::pin(ping_timer));
                }

                State::WaitingForNextPing(ping_timer) => {
                    use futures_util::FutureExt;
                    match ping_timer.poll_unpin(cx) {
                        std::task::Poll::Ready(()) => {
                            let now = ping_timer.deadline();
                            ping_timer.as_mut().reset(deadline(now, keep_alive));
                            return Some(crate::proto::Packet::PingReq(crate::proto::PingReq));
                        }

                        std::task::Poll::Pending => return None,
                    }
                }
            }
        }
    }

    pub(super) fn new_connection(&mut self) {
        *self = State::BeginWaitingForNextPing;
    }
}

impl std::fmt::Debug for State {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            State::BeginWaitingForNextPing => f.write_str("BeginWaitingForNextPing"),
            State::WaitingForNextPing { .. } => f.write_str("WaitingForNextPing"),
        }
    }
}

fn deadline(now: tokio::time::Instant, keep_alive: std::time::Duration) -> tokio::time::Instant {
    now + keep_alive / 2
}
