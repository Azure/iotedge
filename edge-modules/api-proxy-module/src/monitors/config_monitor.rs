use super::utils;
use futures_util::future::Either;
use std::sync::Arc;
use tokio::sync::Notify;
use anyhow::{Context, Result, Error};
use tokio::task::JoinHandle;
use utils::ShutdownHandle;
use std::time::Duration;
use chrono::Utc;

const PROXY_CONFIG_TAG: &str = "proxy_config";
const PROXY_CONFIG_PATH_RAW: &str = "/app/nginx_default_config.conf";
const PROXY_CONFIG_PATH_PARSED: &str = "/app/nginx_config.conf";
const PROXY_CONFIG_DEFAULT_VARS_LIST:&str = "NGINX_DEFAULT_PORT,NGINX_HAS_BLOB_MODULE,NGINX_BLOB_MODULE_NAME_ADDRESS,DOCKER_REQUEST_ROUTE_ADDRESS,NGINX_NOT_ROOT,GATEWAY_HOSTNAME";
const TWIN_PROXY_CONFIG_KEY: &str = "nginx_config";

const PROXY_CONFIG_DEFAULT_VALUES: &'static [(&str, &str)] = &[("NGINX_DEFAULT_PORT", "443")];
const TWIN_STATE_POLL_INTERVAL: Duration = Duration::from_secs(5);

fn duration_from_secs_str(s: &str) -> Result<Duration, <u64 as std::str::FromStr>::Err> {
    Ok(Duration::from_secs(s.parse()?))
}

#[derive(Debug, structopt::StructOpt)]
struct Options {
    #[structopt(
        help = "Whether to use websockets or bare TLS to connect to the Iot Hub",
        long = "use-websocket"
    )]
    use_websocket: bool,

    #[structopt(
        help = "Will message to publish if this client disconnects unexpectedly",
        long = "will"
    )]
    will: Option<String>,

    #[structopt(
		help = "Maximum back-off time between reconnections to the server, in seconds.",
		long = "max-back-off",
		default_value = "30",
		parse(try_from_str = duration_from_secs_str),
	)]
    max_back_off: Duration,

    #[structopt(
		help = "Keep-alive time advertised to the server, in seconds.",
		long = "keep-alive",
		default_value = "5",
		parse(try_from_str = duration_from_secs_str),
	)]
    keep_alive: Duration,
}

pub fn get_sdk_client() -> Result<azure_iot_mqtt::module::Client, Error> {
    let Options {
        use_websocket,
        will,
        max_back_off,
        keep_alive,
    } = structopt::StructOpt::from_args();

    let client = match azure_iot_mqtt::module::Client::new_for_edge_module(
        if use_websocket {
            azure_iot_mqtt::Transport::WebSocket
        } else {
            azure_iot_mqtt::Transport::Tcp
        },
        will.map(Into::into),
        max_back_off,
        keep_alive,
    ){
        Ok(client) => client,
        Err(err) => return Err(anyhow::anyhow!("Could not create client: {:?}", err)),
    };

    Ok(client)
}

pub fn start(
    mut client: azure_iot_mqtt::module::Client,
    notify_received_config: Arc<Notify>,
) -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = utils::ShutdownHandle(shutdown_signal.clone());


    use futures_util::StreamExt;

    //Set default value for some environment variables here
    set_default_env_vars();

    //Parse default config and notify to reboot nginx if it has already started
    //If the config is incorrect, return error because otherwise nginx doesn't have any config.
    parse_config().context("Unable to read default configuration")?;
    match parse_config() {
        //Notify watchdog config is there
        Ok(()) => notify_received_config.notify(),
        Err(err) => return Err(anyhow::anyhow!("Error while parsing default config: {:?}", err)),
    };

    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {

        loop {       
            let wait_shutdown = shutdown_signal.notified();
            futures::pin_mut!(wait_shutdown);

            let message = match futures::future::select(wait_shutdown, client.next()).await {
                Either::Left(_) => {
                    log::warn!("Shutting down config monitor!");
                    return Ok(());
                }
                Either::Right((Some(Ok(message)), _)) => message,
                Either::Right((Some(Err(err)), _)) => {
                    log::error!("Error receiving a message! {:?}", err);
                    continue;
                }
                Either::Right((None, _)) => {
                    log::warn!("Shutting down config monitor!");
                    return Ok(());
                }
            };

            if let azure_iot_mqtt::module::Message::TwinPatch(twin) = message {
                if let Err(err) = save_raw_config(&twin) {
                    log::error!("received message {:?}", err);
                } else {
                    log::info!("received config {:?}", twin);
                    //Here we don't need to panic if config is wrong. There is already a good config running.
                    match parse_config() {
                        //Notify watchdog config is there
                        Ok(()) => notify_received_config.notify(),
                        Err(error) => {
                            log::error!("Error while parsing default config: {:?}", error)
                        }
                    };
                }
            };
        }
    });

    Ok((monitor_loop, shutdown_handle))
}

fn set_default_env_vars() {
    for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
        match std::env::var(key) {
            //If env variable is already declared, do nothing
            Ok(_) => continue,
            //Else add the default value
            Err(_) => std::env::set_var(key, value),
        };
    }
}

fn save_raw_config(twin: &azure_iot_mqtt::TwinProperties) -> Result<()> {
    let json = twin.properties.get_key_value(TWIN_PROXY_CONFIG_KEY);

    //Get value associated with the key and extract is as a string.
    let str = (*(json
        .context(format!("Key {} not found in twin", PROXY_CONFIG_TAG))?
        .1))
        .as_str()
        .context("Cannot extract json as base64 string")?;

    let bytes = get_raw_config(str)?;

    utils::write_binary_to_file(&bytes, PROXY_CONFIG_PATH_RAW)?;

    Ok(())
}

fn parse_config() -> Result<()> {
    //Read "raw configuration". Contains environment variables and sections.
    //Extract IO calls from core function for mocking
    let str = utils::get_string_from_file(PROXY_CONFIG_PATH_RAW)?;

    let str = get_parsed_config(&str)?;
    //Extract IO calls from core function for mocking
    utils::write_binary_to_file(&str.as_bytes(), PROXY_CONFIG_PATH_PARSED)?;

    Ok(())
}

fn get_raw_config(encoded_file: &str) -> Result<Vec<u8>, anyhow::Error> {
    let bytes = match base64::decode(encoded_file) {
        Ok(bytes) => bytes,
        Err(err) => return Err(anyhow::anyhow!(format!("Cannot decode base64 {:?}", err))),
    };

    Ok(bytes)
}

//Check readme for details of how parsing is done.
fn get_parsed_config(str: &str) -> Result<String, anyhow::Error> {
    let mut context = std::collections::HashMap::new();

    //Check if user passed their own env variable list.
    let vars = match std::env::var("NGINX_CONFIG_ENV_VAR_LIST") {
        Ok(vars) => vars,
        //@TO CHECK It copies the string, is that ok?
        Err(_) => PROXY_CONFIG_DEFAULT_VARS_LIST.to_string(),
    };
    let vars = vars.split(',');

    for key in vars {
        let val = match std::env::var(key) {
            Ok(val) => val,
            Err(_) => "0".to_string(),
        };
        context.insert(key.to_string(), val);
    }

    let str: String = envsubst::substitute(str, &context).unwrap();

    //Replace is 0
    let re = regex::Regex::new(r"#if_tag 0((.|\n)*?)#endif_tag 0").unwrap();
    let str = re.replace_all(&str, "").to_string();

    //Or not 1. This allows usage of if ... else ....
    let re = regex::Regex::new(r"#if_tag !1((.|\n)*?)#endif_tag !1").unwrap();
    let str = re.replace_all(&str, "").to_string();

    Ok(str)
}

pub fn report_twin_state(
    mut report_twin_state_handle: azure_iot_mqtt::ReportTwinStateHandle,
) -> (JoinHandle<Result<()>>, ShutdownHandle) {
    use futures_util::StreamExt;

    let shutdown_signal = Arc::new(tokio::sync::Notify::new());
    let shutdown_handle = utils::ShutdownHandle(shutdown_signal.clone());

    let mut interval = tokio::time::interval(TWIN_STATE_POLL_INTERVAL);
    let monitor_loop: JoinHandle<Result<()>> = tokio::spawn(async move {

        let result = report_twin_state_handle
            .report_twin_state(azure_iot_mqtt::ReportTwinStateRequest::Replace(
                vec![(
                    "start-time".to_string(),
                    Utc::now().to_string().into(),
                )]
                .into_iter()
                .collect(),
            ))
            .await;
        let () = result.context("couldn't report initial twin state")?;

        loop {
            let wait_shutdown = shutdown_signal.notified();
            futures::pin_mut!(wait_shutdown);
            match futures::future::select(wait_shutdown, interval.next()).await {
                Either::Left(_) => {
                    log::warn!("Shutting down twin state polling!");
                    return Ok(());
                }
                Either::Right((result, _)) => {
                    if result.is_some() {
                        let result = report_twin_state_handle
                            .report_twin_state(azure_iot_mqtt::ReportTwinStateRequest::Patch(
                                vec![(
                                    "current-time".to_string(),
                                    Utc::now().to_string().into(),
                                )]
                                .into_iter()
                                .collect(),
                            ))
                            .await;

                        let () = result.context("couldn't report twin state patch")?;
                    } else {
                        log::warn!("Shutting down twin state polling!");
                        //Should send a ctrl c event here?
                        return Ok(());
                    }
                }
            };
        }
    });
    (monitor_loop, shutdown_handle)
}

#[cfg(test)]
mod tests {
    const RAW_CONFIG_BASE64:&str = "ZXZlbnRzIHsgfQ0KDQoNCmh0dHAgew0KICAgIHByb3h5X2J1ZmZlcnMgMzIgMTYwazsgIA0KICAgIHByb3h5X2J1ZmZlcl9zaXplIDE2MGs7DQogICAgcHJveHlfcmVhZF90aW1lb3V0IDM2MDA7DQogICAgZXJyb3JfbG9nIC9kZXYvc3Rkb3V0IGluZm87DQogICAgYWNjZXNzX2xvZyAvZGV2L3N0ZG91dDsNCg0KICAgIHNlcnZlciB7DQogICAgICAgIGxpc3RlbiAke05HSU5YX0RFRkFVTFRfUE9SVH0gc3NsIGRlZmF1bHRfc2VydmVyOw0KDQogICAgICAgIGNodW5rZWRfdHJhbnNmZXJfZW5jb2Rpbmcgb247DQoNCiAgICAgICAgc3NsX2NlcnRpZmljYXRlICAgICAgICBzZXJ2ZXIuY3J0Ow0KICAgICAgICBzc2xfY2VydGlmaWNhdGVfa2V5ICAgIHByaXZhdGVfa2V5LnBlbTsgDQogICAgICAgIHNzbF9jbGllbnRfY2VydGlmaWNhdGUgdHJ1c3RlZENBLmNydDsNCiAgICAgICAgc3NsX3ZlcmlmeV9jbGllbnQgb247DQoNCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfSEFTX0JMT0JfTU9EVUxFfQ0KICAgICAgICBpZiAoJGh0dHBfeF9tc19ibG9iX3R5cGUgPSBCbG9ja0Jsb2IpDQogICAgICAgIHsNCiAgICAgICAgICAgIHJld3JpdGUgXiguKikkIC9zdG9yYWdlJDEgbGFzdDsNCiAgICAgICAgfSANCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7RE9DS0VSX1JFUVVFU1RfUk9VVEVfQUREUkVTU30NCiAgICAgICAgbG9jYXRpb24gL3YyIHsNCiAgICAgICAgICAgIHByb3h5X2h0dHBfdmVyc2lvbiAxLjE7DQogICAgICAgICAgICByZXNvbHZlciAxMjcuMC4wLjExOw0KICAgICAgICAgICAgc2V0ICRiYWNrZW5kICJodHRwOi8vJHtET0NLRVJfUkVRVUVTVF9ST1VURV9BRERSRVNTfSI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kOw0KICAgICAgICB9DQogICAgICAgI2VuZGlmX3RhZyAke0RPQ0tFUl9SRVFVRVNUX1JPVVRFX0FERFJFU1N9DQoNCiAgICAgICAgI2lmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCiAgICAgICAgbG9jYXRpb24gfl4vc3RvcmFnZS8oLiopew0KICAgICAgICAgICAgcHJveHlfaHR0cF92ZXJzaW9uIDEuMTsNCiAgICAgICAgICAgIHJlc29sdmVyIDEyNy4wLjAuMTE7DQogICAgICAgICAgICBzZXQgJGJhY2tlbmQgImh0dHA6Ly8ke05HSU5YX0JMT0JfTU9EVUxFX05BTUVfQUREUkVTU30iOw0KICAgICAgICAgICAgcHJveHlfcGFzcyAgICAgICAgICAkYmFja2VuZC8kMSRpc19hcmdzJGFyZ3M7DQogICAgICAgIH0NCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9ICAgICAgDQogICAgICAgIGxvY2F0aW9uIC97DQogICAgICAgICAgICBwcm94eV9odHRwX3ZlcnNpb24gMS4xOw0KICAgICAgICAgICAgcmVzb2x2ZXIgMTI3LjAuMC4xMTsNCiAgICAgICAgICAgIHNldCAkYmFja2VuZCAiaHR0cHM6Ly8ke0dBVEVXQVlfSE9TVE5BTUV9OjQ0MyI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kLyQxJGlzX2FyZ3MkYXJnczsNCiAgICAgICAgfQ0KICAgICAgICAjZW5kaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9DQogICAgfQ0KfQ==";
    const RAW_CONFIG_TEXT:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen ${NGINX_DEFAULT_PORT} ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        if ($http_x_ms_blob_type = BlockBlob)\r\n        {\r\n            rewrite ^(.*)$ /storage$1 last;\r\n        } \r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${DOCKER_REQUEST_ROUTE_ADDRESS}\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        location ~^/storage/(.*){\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${NGINX_BLOB_MODULE_NAME_ADDRESS}\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${NGINX_NOT_ROOT}      \r\n        location /{\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"https://${GATEWAY_HOSTNAME}:443\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_NOT_ROOT}\r\n    }\r\n}";
    const PARSED_CONFIG:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen 443 ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        \r\n\r\n        #if_tag registry:5000\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://registry:5000\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag registry:5000\r\n\r\n        \r\n\r\n        \r\n    }\r\n}";
    use super::*;

    #[test]
    fn env_var_tests() {
        //unset all variables
        std::env::set_var("NGINX_CONFIG_ENV_VAR_LIST", PROXY_CONFIG_DEFAULT_VARS_LIST);
        let vars = PROXY_CONFIG_DEFAULT_VARS_LIST.split(',');
        for key in vars {
            std::env::remove_var(key);
        }

        //All environment variable tests are grouped in one test.
        //The reason is concurrency. Rust test are multi threaded by default
        //And environment variable are globals, so race condition happens.

        //Check config
        std::env::set_var("NGINX_DEFAULT_PORT", "443");
        std::env::set_var("DOCKER_REQUEST_ROUTE_ADDRESS", "registry:5000");

        let byte_str = get_raw_config(RAW_CONFIG_BASE64).unwrap();
        let config = std::str::from_utf8(&byte_str).unwrap();
        assert_eq!(config, RAW_CONFIG_TEXT);

        let config = get_parsed_config(RAW_CONFIG_TEXT).unwrap();

        assert_eq!(&config, PARSED_CONFIG);

        //Check defaults variables set
        //unset all default variables
        for (key, _value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            std::env::remove_var(*key);
        }
        set_default_env_vars();
        for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            let var = std::env::var(key).unwrap();
            assert_eq!(*value, &var);
        }

        //Check the the default function doesn't override user variable
        for (key, _value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            std::env::set_var(*key, "Dummy value");
        }
        set_default_env_vars();
        for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            let var = std::env::var(key).unwrap();
            assert_ne!(*value, &var);
        }
    }
}
