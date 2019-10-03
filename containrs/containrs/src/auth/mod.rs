use std::sync::RwLock;

use failure::ResultExt;
use log::*;
use reqwest::header::{self, HeaderMap};
use reqwest::{Client as ReqwestClient, IntoUrl, Method, RequestBuilder, StatusCode};

use docker_scope::{Scope, Scopes};
use www_authenticate::{ChallengeScheme, WWWAuthenticate};

use crate::error::*;

mod error;

mod docker;
mod oauth2_userpass;

mod scopecache;
use scopecache::ScopeCache;

pub use error::AuthError;

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
    // TODO?: explore more generic approach to cache auth tokens
    scope_cache: RwLock<ScopeCache>,
}

impl AuthClient {
    /// Construct a new AuthClient with given `creds`
    pub fn new(client: ReqwestClient, creds: Credentials) -> AuthClient {
        AuthClient {
            client,
            creds,
            scope_cache: RwLock::new(ScopeCache::new()),
        }
    }

    /// Wrapper around [ReqwestClient::request] which appends authentication
    /// headers. Unlike [ReqwestClient::request], this method is async, as it
    /// may perform some authentication flow HTTP requests prior to returning
    /// the RequestBuilder.
    ///
    /// The `scope` variable is the caller's "best guess" as to what the scope
    /// for the request should be. If the guess is incorrect, the normal
    /// authentication flow will still proceed, though the discrepancy will
    /// be noted in the logs.
    pub async fn request<U: Clone + IntoUrl>(
        &self,
        method: Method,
        url: U,
        scope: &Scope,
    ) -> Result<RequestBuilder> {
        let headers = self
            .authenticate(method.clone(), url.clone(), scope)
            .await?;
        Ok(self.client.request(method, url).headers(headers.clone()))
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

    /// Perform authentication flow for the given request, stashing the
    /// resulting authentication headers in the cache. As a convenience, it
    /// returns a valid authentication header.
    async fn authenticate<U: IntoUrl>(
        &self,
        method: Method,
        url: U,
        expected_scope: &Scope,
    ) -> Result<HeaderMap> {
        // there's some sneaky concurrency to look out for here, as without some special
        // care, the client might end up performing multiple authentication handshakes
        // for the same scope (which would be wasteful)

        if let Some(headers) = self
            .scope_cache
            .read()
            .expect("cache lock was poisoned")
            .get(expected_scope)
        {
            // TODO?: store expiration time alongside scope, and check for expiry
            return Ok(headers.clone());
        };

        debug!("Not authenticated for scope {:?}", expected_scope);

        // TODO: improve concurrency when dealing with multiple disjoint scopes.
        // i.e: starting 2 image pulls at the same time results in one thread blocking
        // while the first authenticates, which doesn't need to happen if the two images
        // have disjoint scopes.

        // hold lock while authenticating
        let mut scope_cache = self.scope_cache.write().expect("cache lock was poisoned");

        // do one more check to be _certain_ that no other requests have already
        // performed the authentication flow in the time between the first check and
        // acquiring the write lock
        if let Some(headers) = scope_cache.get(expected_scope) {
            return Ok(headers.clone());
        }

        // alright, look like this thread gets to authenticate this request

        // perform unauthenticated request to see what sort of authorization is required
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
            return Ok(HeaderMap::new());
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

        for challenge in www_auth.into_iter() {
            let (auth_header, scopes) = match challenge.scheme() {
                ChallengeScheme::Bearer => {
                    let parameters = challenge.into_parameters();

                    let scopes = match parameters
                        .get("scope")
                        .map(|scope_str| scope_str.parse::<Scopes>())
                    {
                        Some(Ok(mut scopes)) => {
                            if scopes.is_disjoint(expected_scope) {
                                warn!("The expected scope did not overlap with the server's returned scopes. Tell a programmer to check the logs.");
                                debug!("Expected {:?}", expected_scope);
                                debug!("Returned {:?}", scopes);
                            }
                            // nevertheless, add the expected scope to the scope list for some basic
                            // caching
                            scopes.add(expected_scope.clone());

                            Some(scopes)
                        }
                        Some(Err(_)) => {
                            warn!("Returned scope doesn't conform to docker scope style");
                            debug!("Returned scope: {}", parameters.get("scope").unwrap());
                            None
                        }
                        _ => {
                            warn!("WWW-Authenticate header did not include a scope attribute");
                            None
                        }
                    };

                    let oauth2_result =
                        oauth2_userpass::auth_flow(&self.client, &self.creds, parameters.clone())
                            .await;

                    let auth_header = match oauth2_result {
                        Ok(auth_header) => auth_header,
                        // Fall back to docker-specific auth flow on failure
                        Err(e) => {
                            let e = failure::Error::from(e);
                            warn!("OAuth2 User-Pass {}", e);
                            for cause in e.iter_causes() {
                                warn!("\tcaused by: {}", cause);
                            }
                            warn!("Attempting Docker-specific auth flow");

                            docker::auth_flow(&self.client, &self.creds, parameters).await?
                        }
                    };

                    debug!("Successfully authenticated for scope {:?}", expected_scope);

                    (auth_header, scopes)
                }
                m => return Err(AuthError::UnimplementedChallengeScheme(m.to_owned()).into()),
            };

            match scopes {
                None => {
                    // This happens when the server isn't using docker-style scopes, or didn't
                    // return any scopes at all.
                    // In this case, use the expected scope as the cache entry directly.
                    scope_cache.insert(expected_scope.clone(), auth_header);
                }
                Some(scopes) => {
                    for scope in scopes {
                        scope_cache.insert(scope, auth_header.clone());
                    }
                }
            }
        }

        Ok(scope_cache.get(expected_scope).unwrap().clone())
    }
}
