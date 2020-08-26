// Copyright (c) Microsoft. All rights reserved.

// Adapted from the conduit proxy signal handling:
// https://github.com/runconduit/conduit/blob/master/proxy/src/signal.rs

pub async fn shutdown() {
    imp::shutdown().await
}

mod imp {
    use futures_util::future::{self, Either};
    use futures_util::stream::StreamExt;
    use tokio::signal::unix::{signal, SignalKind};

    pub(super) async fn shutdown() {
        let mut term = signal(SignalKind::terminate()).expect("signal handling failed");
        let mut interrupt = signal(SignalKind::interrupt()).expect("signal handling failed");
        match future::select(term.next(), interrupt.next()).await {
            Either::Left(_) => log::info!("SIGTERM received"),
            Either::Right(_) => log::info!("SIGINT received"),
        }
    }
}
