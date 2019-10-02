use std::collections::HashMap;
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
    /// the RequestBuilder.
    ///
    /// The `scope` variable is the caller's "best guess" as to what the scope
    /// for the request should be. If the guess is incorrect, the normal
    /// authentication flow will still proceed, though the discrepency will
    /// be noted in the logs.
    pub async fn request<U: Clone + IntoUrl>(
        &self,
        method: Method,
        url: U,
        scope: &Scope,
    ) -> Result<RequestBuilder> {
        // there's some sneaky concurrency to look out for here, as without some special
        // care, the client might end up performing multiple authentication handshakes
        // for the same scope (which would be wasteful)

        // TODO: improve concurrency when dealing with multiple disjoint scopes.
        // i.e: starting 2 image pulls at the same time results in one thread blocking
        // while the first authenticates, which doesn't need to happen if the two images
        // have disjoint scopes.
        // This could probably be solved by having a lock for each hash-map entry

        // Individual API requests _should_ only specify a single action in
        // their scope. If this turns out not be the case, then the cache lookup and
        // insert code (in authenticate()) will have to be modified (as it won't be as
        // simple as checking if the expected scope was in the HashMap or not)
        debug_assert!(scope.actions().size_hint().1 == Some(1));

        if let Some(headers) = self
            .headers_cache
            .read()
            .map_err(|_| AuthError::CacheLock)?
            .get(scope)
        {
            // TODO?: store expiration time alongside scope, and check for expiry
            return Ok(self.client.request(method, url).headers(headers.clone()));
        };

        trace!("Not authenticated for scope {:?}", scope);
        // hold lock while authenticating
        let mut cache = self
            .headers_cache
            .write()
            .map_err(|_| AuthError::CacheLock)?;
        // do one more check to be _certain_ that no other requests have already
        // performed the authentication flow in the time between the first check and
        // acquiring the write lock
        let headers = if let Some(headers) = cache.get(scope) {
            headers.clone()
        } else {
            // this thread gets the responsibility of authenticating this scope
            let mut new_cache_entries = self
                .authenticate(method.clone(), url.clone(), scope)
                .await?
                .into_iter()
                .peekable();

            // guaranteed to return at least one entry
            let headers = new_cache_entries.peek().map(|(_s, h)| h.clone()).unwrap();

            for (scope, headers) in new_cache_entries {
                cache.insert(scope, headers);
            }
            headers
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

    /// Perform authentication flow for the given request. Guaranteed to return
    /// at least one (Scope, HeaderMap) pair, though may return multiple if the
    /// server returns a more permissive scope.
    async fn authenticate<U: IntoUrl>(
        &self,
        method: Method,
        url: U,
        expected_scope: &Scope,
    ) -> Result<impl IntoIterator<Item = (Scope, HeaderMap)>> {
        // perform unauthenticated request to see what sort of authroization is required
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
            return Ok(vec![(expected_scope.clone(), HeaderMap::new())]);
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

        let mut auth_headers: Vec<(Scope, HeaderMap)> = Vec::new();

        for challenge in www_auth.into_iter() {
            let mut associated_scopes = Vec::new();

            let auth_header = match challenge.scheme() {
                ChallengeScheme::Bearer => {
                    let parameters = challenge.into_parameters();

                    let scopes = match parameters
                        .get("scope")
                        .map(|scope_str| scope_str.parse::<Scopes>())
                    {
                        Some(Ok(scopes)) => Some(scopes.into_vec()),
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

                    if let Some(scopes) = scopes {
                        // If docker-style scopes are being used, double check that the expected
                        // scope matches with the returned required scope. It's not a fatal error if
                        // they don't match, but it is something to look into.
                        let mut good_guess = false;

                        for scope in scopes.iter() {
                            // flatten scopes (to make looking them up in the cache easier)
                            let mut flat_scopes = scope.actions().map(|action| {
                                Scope::new(scope.resource().clone(), &[action.clone()])
                            });

                            // check guess
                            good_guess |= flat_scopes.any(|s| s == *expected_scope);

                            associated_scopes.extend(flat_scopes);
                        }

                        if !good_guess {
                            warn!("The expected scope did not overlap with the server's returned scopes. Tell a programmer to check the logs.");
                            debug!("Expected {:?}", expected_scope);
                            debug!("Returned {:?}", scopes);
                        }
                    }

                    let oauth2_result =
                        oauth2_userpass::auth_flow(&self.client, &self.creds, parameters.clone())
                            .await;

                    match oauth2_result {
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
                    }
                }
                m => return Err(AuthError::UnimplementedChallengeScheme(m.to_owned()).into()),
            };

            if associated_scopes.is_empty() {
                // This happens when the server isn't using docker-style scopes, or didn't
                // return any scopes at all.
                // In this case, use the expected scope as the cache entry directly.
                auth_headers.push((expected_scope.clone(), auth_header))
            } else {
                auth_headers.extend(
                    associated_scopes
                        .into_iter()
                        .map(|s| (s, auth_header.clone())),
                )
            }
        }

        Ok(auth_headers)
    }
}
