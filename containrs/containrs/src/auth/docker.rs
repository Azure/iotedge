use std::collections::HashMap;

use headers::Authorization as AuthorizationHeader;
use headers::HeaderMapExt;
use hyper::client::connect::Connect;
use hyper::http::{HeaderMap, Uri};
use hyper::Client as HyperClient;
// use log::*;
use serde::{Deserialize, Serialize};
use serde_urlencoded;

use crate::util::hyper::BodyExt;
use crate::Result;

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
pub async fn auth_flow<C: Connect + 'static>(
    client: &mut HyperClient<C>,
    mut parameters: HashMap<String, String>,
) -> Result<HeaderMap> {
    let realm = parameters.remove("realm").unwrap();
    let query = serde_urlencoded::to_string(parameters)?;

    let uri = (realm + "?" + &query).parse::<Uri>()?;
    let res = client.get(uri).await?;
    if !res.status().is_success() {
        // TODO: report an issue with the authentication
        panic!("auth server didn't respond")
    }

    let token_reponse: DockerAuthTokenResponse = res.into_body().json().await?;

    // TODO: use those expiration times?

    let token = token_reponse
        .token
        .or(token_reponse.access_token)
        .expect("auth server didn't return a token");

    let mut map = HeaderMap::new();
    map.typed_insert(AuthorizationHeader::bearer(&token).unwrap());
    Ok(map)
}
