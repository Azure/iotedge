pub async fn shutdown() {
    imp::shutdown().await
}

#[cfg(unix)]
mod imp {
    use futures_util::future::{self, Either};
    use futures_util::stream::StreamExt;
    use tokio::signal::unix::{signal, SignalKind};
    use tracing::info;

    pub(super) async fn shutdown() {
        let mut term = signal(SignalKind::terminate()).expect("signal handling failed");
        let mut interrupt = signal(SignalKind::interrupt()).expect("signal handling failed");
        match future::select(term.next(), interrupt.next()).await {
            Either::Left(_) => info!("SIGTERM received"),
            Either::Right(_) => info!("SIGINT received"),
        }
    }
}
