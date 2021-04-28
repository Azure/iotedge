use anyhow::Result;
use futures_util::{
    future::{self, select, Either},
    pin_mut,
};
use tokio::{
    self,
    signal::unix::{signal, SignalKind},
};
use tracing::{info, info_span, subscriber, Level};
use tracing_futures::Instrument;
use tracing_subscriber::fmt::Subscriber;

use generic_mqtt_tester::{
    settings::Settings, tester::MessageTester, ExitedWork, MessageTesterError,
};

#[tokio::main]
async fn main() -> Result<()> {
    init_logging();
    let settings = Settings::new()?;
    info!(
        "starting generic mqtt test module with settings: {:?}",
        settings
    );

    let tester = MessageTester::new(settings.clone()).await?;
    let tester_shutdown = tester.shutdown_handle();

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

            tester_shutdown.shutdown(ExitedWork::NoneOrUnknown).await;
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
    let mut terminate =
        signal(SignalKind::terminate()).map_err(MessageTesterError::CreateUnixSignalListener)?;
    let interrupt = interrupt.recv();
    let terminate = terminate.recv();
    pin_mut!(interrupt, terminate);

    info!("listening for unix signals");
    match select(interrupt, terminate).await {
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
