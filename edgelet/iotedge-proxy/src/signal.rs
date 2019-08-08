// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

use futures::{future, Future, Stream};
use log::info;
use tokio_signal::unix::{Signal, SIGINT, SIGTERM};

pub type ShutdownSignal = Box<dyn Future<Item = (), Error = ()> + Send>;

pub fn shutdown() -> ShutdownSignal {
    let signals = [SIGINT, SIGTERM].iter().map(|&sig| {
        Signal::new(sig)
            .flatten_stream()
            .into_future()
            .map(move |_| {
                info!("Received {}, starting shutdown", DisplaySignal(sig),);
            })
    });

    let on_any_signal = future::select_all(signals)
        .map(|_| ())
        .map_err(|_| unreachable!("Signal never returns an error"));

    Box::new(on_any_signal)
}

#[derive(Clone, Copy)]
struct DisplaySignal(i32);

impl fmt::Display for DisplaySignal {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let s = match self.0 {
            SIGINT => "SIGINT",
            SIGTERM => "SIGTERM",
            other => return write!(f, "signal {}", other),
        };
        f.write_str(s)
    }
}
