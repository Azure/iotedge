pub async fn shutdown() {
    imp::shutdown().await
}

#[cfg(unix)]
mod imp {
    use futures_util::{
        future::{self, Either},
        StreamExt,
    };
    use tokio::signal::unix::{signal, SignalKind};
    use tokio_stream::wrappers::SignalStream;
    use tracing::info;

    pub(super) async fn shutdown() {
        let term = signal(SignalKind::terminate()).expect("signal handling failed");
        let mut term = SignalStream::new(term);

        let interrupt = signal(SignalKind::interrupt()).expect("signal handling failed");
        let mut interrupt = SignalStream::new(interrupt);

        match future::select(term.next(), interrupt.next()).await {
            Either::Left(_) => info!("SIGTERM received"),
            Either::Right(_) => info!("SIGINT received"),
        }
    }
}

#[cfg(not(unix))]
mod imp {
    use tokio::signal;

    pub(super) async fn shutdown() {
        signal::ctrl_c().await.expect("signal handling failed");
    }
}
