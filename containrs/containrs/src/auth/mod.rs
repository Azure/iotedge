use std::collections::HashMap;

use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{HeaderMap, Request, Response, StatusCode};
use hyper::Client as HyperClient;
use hyper::Uri;
use log::*;

use crate::Result;

mod docker;
// mod oauth2;

use www_authenticate::{ChallengeScheme, WWWAuthenticate};

/// Credentials used to authenticate with server
#[derive(Debug)]
pub enum Credentials {
    Anonymous,
    UserPass(String, String),
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
                let new_headers = self.authenticate(&req).await?;
                let h = new_headers.clone();
                self.store.insert(req.uri().clone(), new_headers);
                h
            }
        };

        req.headers_mut().extend(headers);
        debug!("{:#?}", req);
        let res = self.client.request(req).await?;
        debug!("{:#?}", res);
        Ok(res)
    }

    /// Wrapper around [HyperClient::get] which authenticates outbound requests
    pub async fn get(&mut self, uri: Uri) -> Result<Response<Body>> {
        self.request(Request::get(uri).body(Body::default())?).await
    }

    /// Retrieve authentication headers for the given Request
    async fn authenticate(&mut self, orig_req: &Request<Body>) -> Result<HeaderMap> {
        debug!("Not authenticated yet");

        // start by pinging the URL without authorization
        let unauth_req = Request::builder()
            .uri(orig_req.uri())
            .method(orig_req.method())
            .body(Body::empty())?;
        debug!("Unauth req: {:#?}", unauth_req);
        let unauth_res = self.client.request(unauth_req).await?;
        debug!("Unauth res: {:#?}", unauth_res);

        assert!(unauth_res.status() == StatusCode::UNAUTHORIZED);

        // extract info from WWW-Authenticate header
        // Syntax of WWW-Authenticate: <type> realm=<realm>
        let www_auth: WWWAuthenticate = unauth_res
            .headers()
            .get(header::WWW_AUTHENTICATE)
            .unwrap() // TODO: gracefully handle missing header
            .to_str()?
            .parse()
            .unwrap(); // TODO: gracefully handle malformed header

        debug!("{:#?}", www_auth);

        // TODO: handle multiple challenges
        let challenge = www_auth.into_iter().next().unwrap();

        match challenge.scheme() {
            ChallengeScheme::Bearer => {
                debug!("Doing Bearer Authentication");
                let parameters = challenge.into_parameters();

                // !!!!!!!!!!!!! TODO !!!!!!!!!!!!!!!!
                // Implement OAuth2 flow, falling back to current docker-specific flow only if
                // things fail. after all, the end goal is talk to an Azure Container Registry

                docker::auth_flow(&mut self.client, parameters).await
            }
            m => panic!("{:?} authentication is not implemented", m),
        }
    }
}
