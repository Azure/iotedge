use anyhow::Result;
use tokio::{self};
use tracing::{info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

use generic_mqtt_tester::settings::Settings;

#[tokio::main]
async fn main() -> Result<()> {
    init_logging();
    info!("Starting generic mqtt test module");

    let settings = Settings::new()?;

    todo!()
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::TRACE).finish();
    let _ = subscriber::set_global_default(subscriber);
}
