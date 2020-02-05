use std::sync::Arc;

use failure::ResultExt;
use futures::lock::Mutex;
use log::*;
use reqwest::header::HeaderMap;
use reqwest::{Client as ReqwestClient, IntoUrl, Method, RequestBuilder, StatusCode};

use docker_scope::{Scope, Scopes};
use www_authenticate::{Challenge, ChallengeScheme, WWWAuthenticate};

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
/// registries, and caching authentication headers
#[derive(Debug)]
pub struct AuthClient {
    client: ReqwestClient,
    creds: Credentials,
    // TODO?: explore other approach to cache auth headers
    scope_cache: Mutex<ScopeCache>,
}

impl AuthClient {
    /// Construct a new AuthClient with given `creds`
    pub fn new(client: ReqwestClient, creds: Credentials) -> AuthClient {
        AuthClient {
            client,
            creds,
            scope_cache: Mutex::new(ScopeCache::new()),
        }
    }

    /// Return reference to underlying unauthenticated reqwest client.
    pub fn raw_client(&self) -> &ReqwestClient {
        &self.client
    }

    /// Wrapper around [ReqwestClient::request] which appends authentication
    /// headers. Unlike [ReqwestClient::request], this method is async, as it
    /// _may_ perform some authentication flow HTTP requests prior to returning
    /// the RequestBuilder.
    ///
    /// The `scope` variable is the caller's "best guess" as to what the scope
    /// for the request should be. If the guess is incorrect, the normal
    /// authentication flow will still proceed, though the discrepancy will
    /// be noted in the logs.
    // TODO: Infer best-guess scopes from URLs.
    // e.g: using regex to infer /v2/ endpoint from URL. This would be slower than
    // the current approach, but would enable dynamically updating scope guesses,
    // and better encapsulation.
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
        // since multiple requests can be fired off at the same time, special care has
        // to be taken when accessing / updating the authentication headers cache to
        // prevent multiple threads from performing authentication requests for the same
        // scope (wasting time and bandwidth).

        // first, acquire a lock on the entire cache
        let mut scope_cache = self.scope_cache.lock().await;

        // check if the expected_scope already exists in the cache
        if let Some(headers) = scope_cache.get(expected_scope) {
            // if it exists, that means the scope is either already authenticated, or a
            // task is currently authenticating it.
            //
            // In the latter case, this task should block until the authentication flow is
            // completed, _without_ blocking other tasks from querying the cache. As such,
            // the outer cache lock is dropped prior to waiting on the inner lock.
            std::mem::drop(scope_cache);
            let headers = headers.lock().await;

            // TODO: store expiration time alongside scope, checking for expiry
            return Ok(headers.clone());
        };

        // alright, look like this task gets to authenticate this request
        debug!("Not authenticated for scope {:?}", expected_scope);

        // insert a new entry into the scope cache, and immediately acquire it's inner
        // lock.
        //
        // This call to .lock() will not block, as this task holds the outer cache lock,
        // preventing any other tasks from calling .get() in the brief period of
        // time between insertion and lock acquisition.
        scope_cache.insert(expected_scope.clone(), HeaderMap::new());
        let headers = scope_cache
            .get(expected_scope)
            .expect("this should not fail");
        let mut headers = headers.lock().await;
        std::mem::drop(scope_cache);

        // once auth flow is completed, this value will be updated, both the inner mutex
        // will automatically release.

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

        let challenge = extract_challenge_from_headers(unauth_res.headers())?;
        let scope_str = challenge.parameters().get("scope").cloned();

        let auth_headers = match challenge.scheme() {
            ChallengeScheme::Bearer => self.authenticate_bearer(expected_scope, challenge).await?,
            _ => unreachable!(),
        };

        // Parse scopes from the scope_str
        let scopes = match scope_str {
            Some(scopes) => match scopes.parse::<Scopes>() {
                Ok(scopes) => Some(scopes),
                Err(_) => {
                    warn!("Returned scope doesn't conform to docker scope style");
                    debug!("Returned scope: {:?}", scopes);
                    None
                }
            },
            None => {
                warn!("WWW-Authenticate header did not include a scope attribute");
                None
            }
        };

        // update the scope cache appropriately
        match scopes {
            None => {
                // This happens when the server isn't using docker-style scopes, or didn't
                // return any scopes at all (e.g: when using Basic auth)
                // In this case, use the expected scope as the cache entry directly.
                *headers = auth_headers.clone()
            }
            Some(mut scopes) => {
                // Check that expected scopes used in containrs line up with the returned
                // scopes. If they don't, it's not a critical mission failure, but it is a
                // cache miss which could have been avoided.
                //
                // TODO: Infer best-guess scopes from URLs.
                // if such a system were put in place, this wouldn't be an warning. Instead, the
                // system would dynamically update it's guess for the expected scope.
                if scopes.is_disjoint(expected_scope) {
                    warn!("The expected scope did not overlap with the server's returned scopes.");
                    debug!("Expected {:?}", expected_scope);
                    debug!("Returned {:?}", scopes);
                    // Add the expected scope to the scope list to get some caching
                    scopes.add(expected_scope.clone());
                }

                // Associate the auth_headers with whatever scopes they correspond to
                let mut scope_cache = self.scope_cache.lock().await;
                for scope in scopes {
                    if scope.resource() != expected_scope.resource() {
                        scope_cache.insert(scope, auth_headers.clone());
                    } else {
                        *headers = auth_headers.clone()
                    }
                }
            }
        }

        Ok(headers.clone())
    }

    async fn authenticate_bearer(
        &self,
        expected_scope: &Scope,
        challenge: Challenge,
    ) -> Result<HeaderMap> {
        let parameters = challenge.into_parameters();

        let oauth2_result =
            oauth2_userpass::auth_flow(&self.client, &self.creds, parameters.clone()).await;

        let auth_headers = match oauth2_result {
            Ok(auth_headers) => auth_headers,
            // Fall back to docker-specific auth flow on failure
            Err(e) => {
                let e = failure::Error::from(e);
                warn!("OAuth2 User-Pass {}", e);
                for cause in e.iter_causes() {
                    warn!("\tcaused by: {}", cause);
                }
                warn!("Falling back to Docker-specific auth flow");

                docker::auth_flow(&self.client, &self.creds, parameters).await?
            }
        };

        debug!("Successfully authenticated for scope {:?}", expected_scope);

        Ok(auth_headers)
    }
}

/// Extracts the strongest implemented challenge scheme from a HeaderMap
fn extract_challenge_from_headers(header: &HeaderMap) -> Result<Challenge> {
    let www_auths = header
        .get_all("WWW-Authenticate")
        .into_iter()
        .map(|val| {
            Ok(val
                .to_str()
                .context(ErrorKind::ApiMalformedHeader("WWW-Authenticate"))?
                .parse::<WWWAuthenticate>()
                .context(ErrorKind::ApiMalformedHeader("WWW-Authenticate"))?)
        })
        .collect::<Result<Vec<_>>>()?;

    if www_auths.is_empty() {
        return Err(ErrorKind::ApiMissingHeader("WWW-Authenticate").into());
    }

    trace!("Parsed WWW-Authenticate header(s): {:#?}", www_auths);

    let mut best_challenge = None;
    let mut unsupported_schemes = Vec::new();
    for challenge in www_auths
        .into_iter()
        .flat_map(|www_auth| www_auth.into_iter())
    {
        match challenge.scheme() {
            ChallengeScheme::Bearer => best_challenge = Some(challenge),
            // ... add schemes in order here ...
            _ => unsupported_schemes.push(challenge.scheme().clone()),
        }
    }
    best_challenge.ok_or_else(|| AuthError::UnsupportedAuth(unsupported_schemes).into())
}

/// A cache for associating Scopes with their corresponding auth headers.
/// Uses per-entry locking to improve concurrency when performing multiple
/// requests with disjoint scopes.
//
// FIXME: ScopeCache lookup needs to be made much, _much_ more efficient
#[derive(Debug)]
struct ScopeCache {
    map: Vec<(Scope, Arc<Mutex<HeaderMap>>)>,
}

impl ScopeCache {
    /// Create a new, empty ScopeCache
    pub fn new() -> ScopeCache {
        ScopeCache { map: Vec::new() }
    }

    /// Check if a given scope is already present in the cache.
    pub fn get(&self, scope: &Scope) -> Option<Arc<Mutex<HeaderMap>>> {
        for (s, headers) in self.map.iter() {
            if s.is_superset(scope) {
                return Some(Arc::clone(headers));
            }
        }
        None
    }

    /// Insert a new scope-headers pair into the cache.
    pub fn insert(&mut self, scope: Scope, headers: HeaderMap) {
        self.map.push((scope, Arc::new(Mutex::new(headers))))
    }
}
