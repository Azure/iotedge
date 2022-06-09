use std::env;

use anyhow::{Context, Error, Result};
use chrono::{DateTime, Duration, Utc};
use log::{error, info};
use tokio::time;

use super::token_client;

use token_client::TokenClient;

pub struct TokenManager {
    token_client: TokenClient,
    token_expiration_date: Option<DateTime<Utc>>,
}

impl TokenManager {
    pub fn new() -> Result<Self, Error> {
        let device_id = env::var("IOTEDGE_DEVICEID")
            .context(format!("Missing env var {}", "IOTEDGE_DEVICEID"))?;
        let module_id = env::var("IOTEDGE_MODULEID")
            .context(format!("Missing env var {}", "IOTEDGE_MODULEID"))?;
        let generation_id = env::var("IOTEDGE_MODULEGENERATIONID")
            .context(format!("Missing env var {}", "IOTEDGE_MODULEGENERATIONID"))?;
        let iothub_hostname = env::var("IOTEDGE_IOTHUBHOSTNAME")
            .context(format!("Missing env var {}", "IOTEDGE_IOTHUBHOSTNAME"))?;
        let workload_url = env::var("IOTEDGE_WORKLOADURI")
            .context(format!("Missing env var {}", "IOTEDGE_WORKLOADURI"))?;

        let work_load_api_client =
            edgelet_client::workload(&workload_url).context("Could not get workload client")?;

        let token_client = TokenClient::new(
            device_id,
            module_id,
            generation_id,
            iothub_hostname,
            work_load_api_client,
        )?;

        //Create expiry date in the past so cert has to be rotated now.
        let token_expiration_date = None;

        Ok(TokenManager {
            token_client,
            token_expiration_date,
        })
    }

    pub async fn poll_new_sas_token(
        &mut self,
        current_date: DateTime<Utc>,
        validity_duration: Duration,
        margin_before_expiry: Duration,
    ) -> Result<Option<String>, Error> {
        //If there is no previous token request a new one right away
        if let Some(expiration_date) = self.token_expiration_date {
            //If Token is not expired, sleep until it is.
            use std::convert::TryInto;
            if current_date < expiration_date {
                let delay_seconds: u64 = expiration_date
                    .signed_duration_since(current_date)
                    .num_seconds()
                    .try_into()
                    .unwrap_or(0);

                let delay_seconds = tokio::time::Duration::from_secs(delay_seconds);
                time::delay_for(delay_seconds).await;
            } else {
                error!("Token has expired before API proxy was able to request a new one. Requesting a new SAS token now");
            }
        }

        info!("Generating new token");
        // Create token expiration date, current date + duration
        let token_renewal_time = Utc::now()
            .checked_add_signed(validity_duration)
            .context("Could not compute new expiration date for certificate")?;

        let token = self
            .token_client
            .get_new_sas_token(&token_renewal_time.timestamp().to_string())
            .await?;

        //Set a "conservative" expiration date, the actual expiration date, minus a margin
        let token_renewal_time = token_renewal_time
            .checked_sub_signed(margin_before_expiry)
            .context(
                "When removing the margin, could not compute new expiration date for certificate",
            )?;

        self.token_expiration_date = Some(token_renewal_time);

        Ok(token)
    }
}
