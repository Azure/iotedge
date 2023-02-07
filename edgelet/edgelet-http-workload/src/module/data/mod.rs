// Copyright (c) Microsoft. All rights reserved.

pub(crate) mod decrypt;
pub(crate) mod encrypt;
pub(crate) mod sign;

#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use test_common::client::KeyClient;

fn base64_decode(data: String) -> Result<Vec<u8>, http_common::server::Error> {
    let engine = base64::engine::general_purpose::STANDARD;

    base64::Engine::decode(&engine, data).map_err(|err| http_common::server::Error {
        status_code: http::StatusCode::BAD_REQUEST,
        message: format!("invalid parameter: {}", err).into(),
    })
}

async fn master_encryption_key(
    client: &KeyClient,
) -> Result<aziot_key_common::KeyHandle, http_common::server::Error> {
    client
        .create_key_if_not_exists(
            "iotedge_master_encryption_id",
            aziot_key_common::CreateKeyValue::Generate,
            &[aziot_key_common::KeyUsage::Encrypt],
        )
        .await
        .map_err(|err| {
            edgelet_http::error::server_error(format!(
                "unable to load master encryption key: {}",
                err
            ))
        })
}
