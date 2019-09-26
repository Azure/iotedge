use std::collections::HashMap;

use failure::ResultExt;
use headers::Authorization as AuthorizationHeader;
use headers::HeaderMapExt;
use hyper::client::connect::Connect;
use hyper::http::{HeaderMap, Request, StatusCode, Uri};
use hyper::Client as HyperClient;
use hyper::{header, Body};
use log::*;
use serde::{Deserialize, Serialize};
use serde_urlencoded;

use crate::error::*;
use crate::util::hyper::{BodyExt, ResponseExt};

use super::{AuthError, Credentials};

/// OAuth2 Token Response
/// https://www.oauth.com/oauth2-servers/access-tokens/access-token-response/

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenResponse {
    /// (required)  The access token string as issued by the authorization
    /// server.
    #[serde(rename = "access_token", skip_serializing_if = "Option::is_none")]
    pub access_token: Option<String>,
    /// (required) The type of token this is, typically just the string
    /// "bearer".
    // NOTE: The spec says token_type is a required field, but Azure Container
    // Registries do _not_ include that field.
    #[serde(rename = "token_type", skip_serializing_if = "Option::is_none")]
    pub token_type: Option<String>,
    /// (optional) If the access token expires, the server should reply with the
    /// duration of time the access token is granted for.
    #[serde(rename = "expires_in", skip_serializing_if = "Option::is_none")]
    pub expires_in: Option<u32>,
    /// (recommended) If the access token will expire, then it is useful to
    /// return a refresh token which applications can use to obtain another
    /// access token.
    #[serde(rename = "refresh_token", skip_serializing_if = "Option::is_none")]
    pub refresh_token: Option<String>,
    ///  (optional) If the scope the user granted is identical to the scope the
    /// app requested, this parameter is optional. If the granted scope is
    /// different from the requested scope, such as if the user modified the
    /// scope, then this parameter is required.
    #[serde(rename = "scope", skip_serializing_if = "Option::is_none")]
    pub scope: Option<String>,
}

/// OAuth2 username-password auth flow handler
pub async fn auth_flow<C: Connect + 'static>(
    client: &mut HyperClient<C>,
    creds: &Credentials,
    mut parameters: HashMap<String, String>,
) -> Result<HeaderMap> {
    let realm = parameters
        .remove("realm")
        .ok_or_else(|| AuthError::AuthServerUri)?;

    let (user, pass) = match creds {
        Credentials::UserPass(user, pass) => (user, pass),
        _ => return Err(AuthError::InvalidCredentials.into()),
    };

    parameters.insert("grant_type".to_string(), "password".to_string());
    parameters.insert("client_id".to_string(), "containrs".to_string());
    parameters.insert("username".to_string(), user.to_string());
    parameters.insert("password".to_string(), pass.to_string());

    // serializing HashMap<String, String> into urlencoded string cannot fail
    let query = serde_urlencoded::to_string(parameters).unwrap();

    let uri = realm.parse::<Uri>().context(AuthError::AuthServerUri)?;
    let req = Request::post(uri)
        .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
        .body(Body::from(query))
        .unwrap();
    trace!("OAuth2 auth server req: {:#?}", req);
    let res = client
        .request(req)
        .await
        .context(AuthError::AuthServerNoResponse)?;
    trace!("OAuth2 auth server res: {:#?}", res);

    if !res.status().is_success() {
        match res.status() {
            StatusCode::UNAUTHORIZED => return Err(AuthError::InvalidCredentials.into()),
            status => {
                res.dump_to_debug().await;
                return Err(AuthError::AuthServerError(status).into());
            }
        }
    }

    let token_reponse: TokenResponse = res
        .into_body()
        .json()
        .await
        .context(AuthError::AuthServerInvalidResponse)?;

    // TODO: use expiration time?

    let token = token_reponse
        .access_token
        .ok_or_else(|| AuthError::AuthServerMissingToken)?;

    let mut map = HeaderMap::new();
    map.typed_insert(
        AuthorizationHeader::bearer(&token).context(AuthError::AuthServerInvalidToken)?,
    );
    Ok(map)
}
