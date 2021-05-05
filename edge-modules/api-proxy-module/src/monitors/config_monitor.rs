use std::{sync::Arc, time::Duration};

use anyhow::{Context, Error, Result};
use futures_util::{future::Either, pin_mut, StreamExt};
use log::{error, info, warn};
use tokio::{sync::Notify, task::JoinHandle};

use crate::monitors::config_parser;
use crate::utils::file;
use crate::utils::shutdown_handle;

use azure_iot_mqtt::{module::Client, Transport::Tcp, TwinProperties};
use config_parser::ConfigParser;
use shutdown_handle::ShutdownHandle;

const PROXY_CONFIG_TAG: &str = "proxy_config";
const PROXY_CONFIG_PATH_RAW: &str = "/app/nginx_default_config.conf";
const PROXY_CONFIG_PATH_PARSED: &str = "/app/nginx_config.conf";

const TWIN_CONFIG_MAX_BACK_OFF: Duration = Duration::from_secs(30);
const TWIN_CONFIG_KEEP_ALIVE: Duration = Duration::from_secs(300);

pub fn get_sdk_client() -> Result<Client, Error> {
    let client = match Client::new_for_edge_module(
        Tcp,
        None,
        TWIN_CONFIG_MAX_BACK_OFF,
        TWIN_CONFIG_KEEP_ALIVE,
    ) {
        Ok(client) => client,
        Err(err) => return Err(anyhow::anyhow!("Could not create client: {}", err)),
    };

    Ok(client)
}

pub fn start(
    mut client: Client,
    notify_received_config: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    info!("Initializing config monitoring loop");
    let config_parser = ConfigParser::new();
    parse_config(&config_parser)?;

    info!("Starting config monitoring loop");
    //Config is ready, send notification.
    notify_received_config.notify_one();

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {
        loop {
            let wait_shutdown = shutdown_signal.notified();
            pin_mut!(wait_shutdown);

            let parse_config_request =
                match futures_util::future::select(wait_shutdown, client.next()).await {
                    Either::Left(_) => {
                        warn!("Shutting down config monitor!");
                        return Ok(());
                    }
                    Either::Right((Some(Ok(message)), _)) => {
                        if let azure_iot_mqtt::module::Message::TwinPatch(twin) = message {
                            if let Err(err) = save_raw_config(&twin) {
                                error!("received message {}", err);
                                false
                            } else {
                                info!("New config received from twin, reloading the config");
                                true
                            }
                        } else {
                            false
                        }
                    }
                    Either::Right((Some(Err(err)), _)) => {
                        error!("Error receiving a message! {}", err);
                        false
                    }
                    Either::Right((None, _)) => {
                        warn!("Shutting down config monitor!");
                        return Ok(());
                    }
                };

            if parse_config_request {
                match parse_config(&config_parser) {
                    //Notify watchdog config is there
                    Ok(()) => notify_received_config.notify_one(),
                    Err(error) => error!("Error while parsing default config: {}", error),
                };
            };
        }
    });

    Ok((monitor_loop, shutdown_handle))
}

fn save_raw_config(twin: &TwinProperties) -> Result<()> {
    let json = twin.properties.get_key_value(PROXY_CONFIG_TAG);

    //Get value associated with the key and extract is as a string.
    let str = (*(json
        .context(format!("Key {} not found in twin", PROXY_CONFIG_TAG))?
        .1))
        .as_str()
        .context("Cannot extract json as base64 string")?;

    let bytes = get_raw_config(str)?;

    file::write_binary_to_file(&bytes, PROXY_CONFIG_PATH_RAW)?;

    Ok(())
}

fn parse_config(parse_config: &ConfigParser) -> Result<()> {
    //Read "raw configuration". Contains environment variables and sections.
    //Extract IO calls from core function for mocking
    let str = file::get_string_from_file(PROXY_CONFIG_PATH_RAW)?;

    let str = parse_config.get_parsed_config(&str)?;
    //Extract IO calls from core function for mocking
    file::write_binary_to_file(&str.as_bytes(), PROXY_CONFIG_PATH_PARSED)?;

    Ok(())
}

fn get_raw_config(encoded_file: &str) -> Result<Vec<u8>, anyhow::Error> {
    let bytes = match base64::decode(encoded_file) {
        Ok(bytes) => bytes,
        Err(err) => return Err(anyhow::anyhow!(format!("Cannot decode base64 {}", err))),
    };

    Ok(bytes)
}
