// Copyright (c) Microsoft. All rights reserved.

// Adapted from the conduit proxy signal handling:
// https://github.com/linkerd/linkerd2-proxy/blob/79b6285f8/proxy/src/signal.rs

use futures_util::future::{self, Either};
use futures_util::stream::StreamExt;
use log::info;
use tokio::signal::unix::{signal, SignalKind};

pub async fn shutdown() {
    let mut term = signal(SignalKind::terminate()).expect("signal handling failed");
    let mut interrupt = signal(SignalKind::interrupt()).expect("signal handling failed");
    match future::select(term.next(), interrupt.next()).await {
        Either::Left(_) => info!("SIGTERM received"),
        Either::Right(_) => info!("SIGINT received"),
    }
}
