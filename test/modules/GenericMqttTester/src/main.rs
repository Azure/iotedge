use anyhow::Result;
use futures_util::{
    future::{self, select, Either},
    pin_mut,
};
use tokio::{self, stream::StreamExt};
use tokio::{
    signal::unix::{signal, SignalKind},
    time,
};
use tracing::{info, info_span, subscriber, Level};
use tracing_futures::Instrument;
use tracing_subscriber::fmt::Subscriber;

use generic_mqtt_tester::{settings::Settings, tester::MessageTester, MessageTesterError};

#[tokio::main]
async fn main() -> Result<()> {
    init_logging();
    info!("starting generic mqtt test module");
    let settings = Settings::new()?;

    let tester = MessageTester::new(settings.clone()).await?;
    let tester_shutdown = tester.shutdown_handle();

    time::delay_for(settings.test_start_delay()).await;

    let test_fut = tester.run().instrument(info_span!("tester"));
    let shutdown_fut = listen_for_shutdown().instrument(info_span!("shutdown"));
    pin_mut!(test_fut);
    pin_mut!(shutdown_fut);

    match future::select(test_fut, shutdown_fut).await {
        Either::Left((test_result, _)) => {
            info!("test finished");
            test_result?;
        }
        Either::Right((shutdown, test_fut)) => {
            info!("processing shutdown, stopping test");
            shutdown?;

            tester_shutdown.shutdown().await?;
            test_fut.await?;
        }
    };

    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

async fn listen_for_shutdown() -> Result<(), MessageTesterError> {
    info!("registering unix signal listeners");
    let mut interrupt =
        signal(SignalKind::interrupt()).map_err(MessageTesterError::CreateUnixSignalListener)?;
    let mut term =
        signal(SignalKind::terminate()).map_err(MessageTesterError::CreateUnixSignalListener)?;
    let interrupt_fut = interrupt.next();
    let term_fut = term.next();

    info!("listening for unix signals");
    match select(interrupt_fut, term_fut).await {
        Either::Left((interrupt, _)) => {
            info!("received SIGINT");
            interrupt.ok_or(MessageTesterError::ListenForUnixSignal)?
        }
        Either::Right((term, _)) => {
            info!("received SIGTERM");
            term.ok_or(MessageTesterError::ListenForUnixSignal)?
        }
    }

    Ok(())
}
