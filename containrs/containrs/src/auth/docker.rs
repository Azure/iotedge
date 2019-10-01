//! FIXME: Docker _should_ support oauth2 username-password authentication, but
//! for some reason, registry-1.docker.io keeps throwing 405s whenever you POST
//! the auth server.

use std::collections::HashMap;

use failure::ResultExt;
use headers::Authorization as AuthorizationHeader;
use headers::HeaderMapExt;
use log::*;
use reqwest::header::HeaderMap;
use reqwest::{Client as ReqwestClient, StatusCode};
use serde::{Deserialize, Serialize};

use crate::error::*;

use super::{AuthError, Credentials};

/// Docker Authorization Token Response.
///
/// See https://docs.docker.com/registry/spec/auth/token/
#[derive(Debug, Serialize, Deserialize)]
pub struct DockerAuthTokenResponse {
    /// An opaque Bearer token that clients should supply to subsequent requests
    /// in the Authorization header.
    #[serde(rename = "token", skip_serializing_if = "Option::is_none")]
    pub token: Option<String>,
    /// For compatibility with OAuth 2.0, also accept token under the name
    /// access_token. At least one of these fields must be specified, but both
    /// may also appear (for compatibility with older clients). When both are
    /// specified, they should be equivalent; if they differ the client's choice
    /// is undefined.
    #[serde(rename = "access_token", skip_serializing_if = "Option::is_none")]
    pub access_token: Option<String>,
    /// (Optional) The duration in seconds since the token was issued that it
    /// will remain valid. When omitted, this defaults to 60 seconds. For
    /// compatibility with older clients, a token should never be returned with
    /// less than 60 seconds to live.
    #[serde(rename = "expires_in", skip_serializing_if = "Option::is_none")]
    pub expires_in: Option<u32>,
    /// (Optional) The RFC3339-serialized UTC standard time at which a given
    /// token was issued. If issued_at is omitted, the expiration is from when
    /// the token exchange completed.
    #[serde(rename = "issued_at", skip_serializing_if = "Option::is_none")]
    pub issued_at: Option<String>,
    /// (Optional) Token which can be used to get additional access tokens for
    /// the same subject with different scopes. This token should be kept secure
    /// by the client and only sent to the authorization server which issues
    /// bearer tokens. This field will only be set when `offline_token=true` is
    /// provided in the request.
    #[serde(rename = "refresh_token", skip_serializing_if = "Option::is_none")]
    pub refresh_token: Option<String>,
}

/// Docker auth flow handler
pub async fn auth_flow(
    client: &ReqwestClient,
    creds: &Credentials,
    mut parameters: HashMap<String, String>,
) -> Result<HeaderMap> {
    let realm = parameters
        .remove("realm")
        .ok_or_else(|| AuthError::AuthServerUri)?;

    let mut req = client.get(realm.as_str()).query(&parameters);
    // Credentials aren't required to access public repos
    if let Credentials::UserPass(user, pass) = creds {
        let mut m = HeaderMap::new();
        m.typed_insert(AuthorizationHeader::basic(user, pass));
        req = req.headers(m);
    }
    trace!("Docker auth server req: {:#?}", req);
    let res = req.send().await.context(AuthError::AuthServerNoResponse)?;
    trace!("Docker auth server res: {:#?}", res);

    if !res.status().is_success() {
        match res.status() {
            StatusCode::UNAUTHORIZED => return Err(AuthError::InvalidCredentials.into()),
            status => {
                debug!("Unexpected response: {:#?}", res);
                debug!("Unexpected response content: {:#?}", res.text().await);
                return Err(AuthError::AuthServerError(status).into());
            }
        }
    }

    let token_reponse: DockerAuthTokenResponse = res
        .json()
        .await
        .context(AuthError::AuthServerInvalidResponse)?;

    // TODO: use those expiration times

    let token = token_reponse
        .token
        .or(token_reponse.access_token)
        .ok_or_else(|| AuthError::AuthServerMissingToken)?;

    let mut map = HeaderMap::new();
    map.typed_insert(
        AuthorizationHeader::bearer(&token).context(AuthError::AuthServerInvalidToken)?,
    );
    Ok(map)
}
