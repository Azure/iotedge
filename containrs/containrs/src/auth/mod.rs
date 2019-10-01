use std::collections::HashMap;
use std::sync::RwLock;

use failure::ResultExt;
use log::*;
use reqwest::header::{self, HeaderMap};
use reqwest::{Client as ReqwestClient, IntoUrl, Method, RequestBuilder, StatusCode};

use www_authenticate::{ChallengeScheme, WWWAuthenticate};

use crate::error::*;

mod error;
mod scope;

mod docker;
mod oauth2_userpass;

pub use error::AuthError;
pub use scope::*;

/// Credentials used to authenticate with server
#[derive(Debug)]
pub enum Credentials {
    Anonymous,
    UserPass(String, String),
    AzureActiveDirectory, // Parameters TBD (once I learn more about how AAD works)
}

/// Wrapper around reqwest::Client to handle authenticating with various
/// registries, and caching authentication tokens
#[derive(Debug)]
pub struct AuthClient {
    client: ReqwestClient,
    creds: Credentials,
    // TODO?: explore more generic approach to cache auth tokens, tailored to specific registries
    headers_cache: RwLock<HashMap<Scope, HeaderMap>>,
}

impl AuthClient {
    /// Construct a new AuthClient with given `creds`
    pub fn new(client: ReqwestClient, creds: Credentials) -> AuthClient {
        AuthClient {
            client,
            creds,
            headers_cache: RwLock::new(HashMap::new()),
        }
    }

    /// Wrapper around [ReqwestClient::request] which appends authentication
    /// headers. Unlike [ReqwestClient::request], this method is async, as it
    /// may perform some authentication flow HTTP requests prior to returning
    /// the RequestBuilder
    pub async fn request<U: Clone + IntoUrl>(
        &self,
        method: Method,
        url: U,
        scope: &Scope,
    ) -> Result<RequestBuilder> {
        // there's some sneaky concurrency to look out for here, as without some special
        // care, the client might end up performing multiple authentication handshakes
        // for the same scope (which would be wasteful)
        //
        // TODO: improve concurrency when dealing with multiple disjoint scopes.
        // i.e: starting 2 image pulls at the same time results in one thread blocking
        // while the first authenticates, which doesn't need to happen if the two images
        // have disjoint scopes.
        // This could probably be solved by having a lock for each hash-map entry

        if let Some(headers) = self
            .headers_cache
            .read()
            .map_err(|_| AuthError::CacheLock)?
            .get(scope)
        {
            // TODO?: store expiration time alongside scope
            return Ok(self.client.request(method, url).headers(headers.clone()));
        };

        trace!("Not authenticated for scope {:?}", scope);
        // hold lock while authenticating
        let mut map = self
            .headers_cache
            .write()
            .map_err(|_| AuthError::CacheLock)?;
        // do one more check to be _certain_ that no other requests have already
        // performed the authentication flow
        let headers = if let Some(headers) = map.get(scope) {
            headers.clone()
        } else {
            // alright, this thread gets the responsibility of doing auth flow for this
            // scope
            let new_entries = self
                .authenticate(method.clone(), url.clone(), scope)
                .await?;
            for (scope, headers) in new_entries {
                map.insert(scope, headers);
            }
            map.get(scope).unwrap().clone()
        };
        trace!("Successfully authenticated scope {:?}", scope);

        Ok(self.client.request(method, url).headers(headers))
    }

    /// Wrapper around [ReqwestClient::get] which appends authentication
    /// headers
    pub async fn get<U: IntoUrl + Clone>(&self, url: U, scope: &Scope) -> Result<RequestBuilder> {
        self.request(Method::GET, url, scope).await
    }

    /// Wrapper around [ReqwestClient::head] which appends authentication
    /// headers
    pub async fn head<U: IntoUrl + Clone>(&self, url: U, scope: &Scope) -> Result<RequestBuilder> {
        self.request(Method::HEAD, url, scope).await
    }

    /// Perform authentication flow for the given request
    async fn authenticate<U: IntoUrl>(
        &self,
        method: Method,
        url: U,
        expect_scope: &Scope,
    ) -> Result<impl IntoIterator<Item = (Scope, HeaderMap)>> {
        // perform unauthenticated request to check what scope is required
        let unauth_res = self
            .client
            .request(method, url)
            .send()
            .await
            .context(AuthError::EndpointNoResponse)?;
        trace!("Unauth res: {:#?}", unauth_res);

        if unauth_res.status() != StatusCode::UNAUTHORIZED {
            // that's weird, but okay. Just pass up an empty auth header
            warn!("Attempted to authenticate with a URI that didn't require authentication");
            return Ok(vec![(expect_scope.clone(), HeaderMap::new())]);
        }

        // extract info from WWW-Authenticate header
        let www_auth = unauth_res
            .headers()
            .get(header::WWW_AUTHENTICATE)
            .ok_or_else(|| AuthError::EndpointMissingWWWAuth)?
            .to_str()
            .context(AuthError::EndpointMalformedWWWAuth)?
            .parse::<WWWAuthenticate>()
            .context(AuthError::EndpointMalformedWWWAuth)?;

        trace!("Parsed WWW-Authenticate header: {:#?}", www_auth);

        // TODO: once scope parsing is implemented, check that returned scopes match
        // expected scope

        let mut auth_headers = Vec::new();
        for challenge in www_auth.into_iter() {
            let auth_header = match challenge.scheme() {
                ChallengeScheme::Bearer => {
                    let parameters = challenge.into_parameters();

                    let oauth2_result =
                        oauth2_userpass::auth_flow(&self.client, &self.creds, parameters.clone())
                            .await;

                    match oauth2_result {
                        Ok(auth_header) => auth_header,
                        // Fall back to docker-specific auth flow on faliure
                        Err(e) => {
                            let e = failure::Error::from(e);
                            warn!("OAuth2 User-Pass {}", e);
                            for cause in e.iter_causes() {
                                warn!("\tcaused by: {}", cause);
                            }
                            warn!("Attempting Docker-specific auth flow");

                            docker::auth_flow(&self.client, &self.creds, parameters.clone()).await?
                        }
                    }
                }
                m => return Err(AuthError::UnimplementedChallengeScheme(m.to_owned()).into()),
            };
            auth_headers.push((expect_scope.clone(), auth_header))
        }

        Ok(auth_headers)
    }
}
