use std::collections::HashMap;

use failure::ResultExt;
use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{HeaderMap, Request, Response, StatusCode};
use hyper::Client as HyperClient;
use hyper::Uri;
use log::*;

use crate::error::*;

mod docker;
mod error;
mod oauth2_userpass;

pub use error::AuthError;

use www_authenticate::{ChallengeScheme, WWWAuthenticate};

/// Credentials used to authenticate with server
#[derive(Debug)]
pub enum Credentials {
    Anonymous,
    UserPass(String, String),
    AzureActiveDirectory, // Parameters TBD (once I learn more about how AAD works)
}

/// Wrapper around hyper::Client providing transparent authentication
#[derive(Debug)]
pub struct AuthClient<C> {
    client: HyperClient<C>,
    creds: Credentials,
    store: HashMap<Uri, HeaderMap>,
}

impl<C: Connect + 'static> AuthClient<C> {
    /// Construct a new AuthClient with given `creds`
    pub fn new(client: HyperClient<C>, creds: Credentials) -> AuthClient<C> {
        AuthClient {
            client,
            creds,
            store: HashMap::new(),
        }
    }

    /// Wrapper around [HyperClient::request] which authenticates outbound
    /// requests
    pub async fn request(&mut self, mut req: Request<Body>) -> Result<Response<Body>> {
        let headers = match self.store.get(req.uri()) {
            Some(h) => h.clone(),
            None => {
                let new_headers = self
                    .authenticate(&req)
                    .await
                    .context(ErrorKind::AuthClientRequest)?;
                debug!("Authenticated successfully");
                let h = new_headers.clone();
                self.store.insert(req.uri().clone(), new_headers);
                h
            }
        };

        req.headers_mut().extend(headers);
        debug!("{:#?}", req);
        let res = self
            .client
            .request(req)
            .await
            .context(ErrorKind::AuthClientRequest)?;
        debug!("{:#?}", res);
        Ok(res)
    }

    /// Wrapper around [HyperClient::get] which authenticates outbound requests
    pub async fn get(&mut self, uri: Uri) -> Result<Response<Body>> {
        self.request(
            Request::get(uri)
                .body(Body::default())
                .context(ErrorKind::AuthClientRequest)?,
        )
        .await
    }

    /// Retrieve authentication headers for the given Request
    async fn authenticate(&mut self, orig_req: &Request<Body>) -> Result<HeaderMap> {
        debug!("Not authenticated yet");

        // start by pinging the URL without authorization
        let unauth_req = Request::builder()
            .uri(orig_req.uri())
            .method(orig_req.method())
            .body(Body::empty())
            .unwrap(); // won't panic, as it's being built from a known-valid request
        debug!("Unauth req: {:#?}", unauth_req);
        let unauth_res = self
            .client
            .request(unauth_req)
            .await
            .context(AuthError::EndpointNoResponse)?;
        debug!("Unauth res: {:#?}", unauth_res);

        if unauth_res.status() != StatusCode::UNAUTHORIZED {
            // that's wierd, but okay. Just pass up an empty auth header
            warn!(
                "Attempted to authenticate with a URI that didn't require authentication: {:?}",
                orig_req.uri()
            );
            return Ok(HeaderMap::new());
        }

        // extract info from WWW-Authenticate header
        // Syntax of WWW-Authenticate: <type> realm=<realm>
        let www_auth = unauth_res
            .headers()
            .get(header::WWW_AUTHENTICATE)
            .ok_or_else(|| AuthError::EndpointMissingHeader)?
            .to_str()
            .context(AuthError::EndpointMalformedHeader)?
            .parse::<WWWAuthenticate>()
            .context(AuthError::EndpointMalformedHeader)?;

        debug!("Parsed WWW-Authenticate header: {:#?}", www_auth);

        let mut headers = HeaderMap::new();
        for challenge in www_auth.into_iter() {
            let auth_header = match challenge.scheme() {
                ChallengeScheme::Bearer => {
                    let parameters = challenge.into_parameters();

                    let oauth2_result = oauth2_userpass::auth_flow(
                        &mut self.client,
                        &self.creds,
                        parameters.clone(),
                    )
                    .await;

                    match oauth2_result {
                        Ok(auth_header) => auth_header,
                        // Fall back to docker-specific auth flow on faliure
                        Err(e) => {
                            let e = failure::Error::from(e);
                            warn!("{}", e);
                            for cause in e.iter_causes() {
                                warn!("\tcaused by: {}", cause);
                            }
                            warn!("Attempting Docker-specific auth flow");

                            docker::auth_flow(&mut self.client, &self.creds, parameters.clone())
                                .await?
                        }
                    }
                }
                m => return Err(AuthError::UnimplementedChallengeScheme(m.to_owned()).into()),
            };
            headers.extend(auth_header)
        }
        Ok(headers)
    }
}
