// Copyright (c) Microsoft. All rights reserved.

// Adapted from the conduit proxy signal handling:
// https://github.com/runconduit/conduit/blob/master/proxy/src/signal.rs

use futures::Future;

type ShutdownSignal = Box<Future<Item = (), Error = ()> + Send>;

pub fn shutdown() -> ShutdownSignal {
    imp::shutdown()
}

#[cfg(unix)]
mod imp {
    use std::fmt;

    use futures::{future, Future, Stream};
    use tokio_signal::unix::{Signal, SIGINT, SIGTERM};

    use super::ShutdownSignal;

    pub(super) fn shutdown() -> ShutdownSignal {
        let signals = [SIGINT, SIGTERM].iter().map(|&sig| {
            Signal::new(sig)
                .flatten_stream()
                .into_future()
                .map(move |_| {
                    info!(
                        target: "iotedged::signal",
                        "Received {}, starting shutdown",
                        DisplaySignal(sig),
                    );
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
        fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
            let s = match self.0 {
                SIGINT => "SIGINT",
                SIGTERM => "SIGTERM",
                other => return write!(f, "signal {}", other),
            };
            f.write_str(s)
        }
    }
}

#[cfg(not(unix))]
mod imp {
    use futures::{Future, Stream};
    use tokio_signal;

    use super::ShutdownSignal;

    pub(super) fn shutdown() -> ShutdownSignal {
        let on_ctrl_c = tokio_signal::ctrl_c()
            .flatten_stream()
            .into_future()
            .map(|_| {
                info!(
                    target: "iotedged::signal",
                    "Received Ctrl+C, starting shutdown",
                );
            })
            .map_err(|_| unreachable!("ctrl_c never returns errors"));
        Box::new(on_ctrl_c)
    }
}
