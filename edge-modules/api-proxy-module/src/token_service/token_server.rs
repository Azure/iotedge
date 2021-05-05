use std::{env, sync::Arc};

use anyhow::{Context, Error, Result};
use chrono::{Duration, Utc};
use futures_util::{future::Either, pin_mut};
use hyper::service::{make_service_fn, service_fn};
use hyper::{Body, Request, Response, Server};
use log::{error, warn};
use tokio::{sync::Notify, task::JoinHandle};

use crate::token_service::token_client;
use crate::utils::shutdown_handle;

use token_client::TokenClient;

pub const TOKEN_VALIDITY_SECONDS: i64 = 3600;
pub const TOKEN_SERVER_PORT: u16 = 6001;
pub const TOKEN_SERVER_IP: [u8; 4] = [127, 0, 0, 1];

use shutdown_handle::ShutdownHandle;

pub fn start() -> Result<(JoinHandle<Result<()>>, ShutdownHandle), Error> {
    let shutdown_signal = Arc::new(Notify::new());
    let shutdown_handle = ShutdownHandle(shutdown_signal.clone());

    let token_server: JoinHandle<Result<()>> = tokio::spawn(async move {
        let token_client = get_token_client()?;
        let token_client = Arc::new(token_client);

        loop {
            let wait_shutdown = shutdown_signal.notified();
            let local_token_client = token_client.clone();

            let make_svc = make_service_fn(move |_conn| {
                let token_client_clone = local_token_client.clone();
                async move {
                    Ok::<_, Error>(service_fn(move |req| {
                        server_callback(req, token_client_clone.clone())
                    }))
                }
            });

            let addr = (TOKEN_SERVER_IP, TOKEN_SERVER_PORT).into();
            let server = Server::bind(&addr).serve(make_svc);
            pin_mut!(wait_shutdown);
            pin_mut!(server);

            match futures_util::future::select(wait_shutdown, server).await {
                Either::Left(_) => {
                    warn!("Shutting down token server monitor!");
                    return Ok(());
                }
                Either::Right(_) => {
                    error!("Server crashed, restarting");
                }
            }
        }
    });

    Ok((token_server, shutdown_handle))
}

pub fn get_token_client() -> Result<TokenClient, Error> {
    let device_id =
        env::var("IOTEDGE_DEVICEID").context(format!("Missing env var {}", "IOTEDGE_DEVICEID"))?;
    let module_id =
        env::var("IOTEDGE_MODULEID").context(format!("Missing env var {}", "IOTEDGE_MODULEID"))?;
    let generation_id = env::var("IOTEDGE_MODULEGENERATIONID")
        .context(format!("Missing env var {}", "IOTEDGE_MODULEGENERATIONID"))?;
    let iothub_hostname = env::var("IOTEDGE_IOTHUBHOSTNAME")
        .context(format!("Missing env var {}", "IOTEDGE_IOTHUBHOSTNAME"))?;
    let workload_url = env::var("IOTEDGE_WORKLOADURI")
        .context(format!("Missing env var {}", "IOTEDGE_WORKLOADURI"))?;

    let work_load_api_client =
        edgelet_client::workload(&workload_url).context("Could not get workload client")?;

    Ok(TokenClient::new(
        device_id,
        module_id,
        generation_id,
        iothub_hostname,
        work_load_api_client,
    ))
}

async fn server_callback(
    _: Request<Body>,
    token_client: Arc<token_client::TokenClient>,
) -> Result<Response<Body>, Error> {
    let validity_duration = Duration::seconds(TOKEN_VALIDITY_SECONDS);

    let token_renewal_time = Utc::now()
        .checked_add_signed(validity_duration)
        .context("Could not compute new expiration date for certificate")?;

    let token = token_client
        .get_new_sas_token(&token_renewal_time.timestamp().to_string())
        .await?;

    let response = Response::builder()
        .header("X-Token", token)
        .body(Body::empty())?;

    Ok(response)
}
