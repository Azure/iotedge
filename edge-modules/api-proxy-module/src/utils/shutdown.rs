use futures_util::{
    future::{self, Either},
    pin_mut,
};
use log::info;
use tokio::signal::unix::{signal, SignalKind};

pub async fn shutdown() {
    let mut terminate = signal(SignalKind::terminate()).expect("signal handling failed");
    let terminate = terminate.recv();

    let mut interrupt = signal(SignalKind::interrupt()).expect("signal handling failed");
    let interrupt = interrupt.recv();

    pin_mut!(terminate, interrupt);

    match future::select(terminate, interrupt).await {
        Either::Left(_) => info!("SIGTERM received"),
        Either::Right(_) => info!("SIGINT received"),
    }
}
