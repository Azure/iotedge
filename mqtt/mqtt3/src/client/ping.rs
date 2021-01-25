use std::future::Future;

use pin_project::pin_project;

#[pin_project(project = StateProj)]
pub(super) enum State {
    BeginWaitingForNextPing,
    WaitingForNextPing(#[pin] tokio::time::Sleep),
}

impl State {
    pub(super) fn poll(
        mut self: std::pin::Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,

        packet: &mut Option<crate::proto::Packet>,
        keep_alive: std::time::Duration,
    ) -> Option<crate::proto::Packet> {
        if let Some(crate::proto::Packet::PingResp(crate::proto::PingResp)) = packet {
            let _ = packet.take();

            match self.as_mut().project() {
                StateProj::BeginWaitingForNextPing => (),
                StateProj::WaitingForNextPing(ping_timer) => {
                    ping_timer.reset(deadline(tokio::time::Instant::now(), keep_alive))
                }
            }
        }

        loop {
            log::trace!("    {:?}", self);

            match self.as_mut().project() {
                StateProj::BeginWaitingForNextPing => {
                    let ping_timer = tokio::time::sleep(keep_alive);
                    self.set(State::WaitingForNextPing(ping_timer));
                }

                StateProj::WaitingForNextPing(mut ping_timer) => {
                    match ping_timer.as_mut().poll(cx) {
                        std::task::Poll::Ready(()) => {
                            let deadline = deadline(ping_timer.deadline(), keep_alive);
                            ping_timer.reset(deadline);
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

    pub(super) fn new_connection_pin(mut self: std::pin::Pin<&mut Self>) {
        self.set(State::BeginWaitingForNextPing);
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
