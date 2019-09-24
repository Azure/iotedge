use failure::{Fail, ResultExt};
use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{uri, Method, Request, Response, StatusCode, Uri};
use hyper::Client as HyperClient;
// use log::*;
use serde::{Deserialize, Serialize};

use docker_reference::Reference;
use oci_distribution::v2::Catalog;
use oci_image::{v1::Manifest, MediaType};

use crate::auth::{AuthClient, Credentials};
use crate::error::*;
use crate::util::hyper::{BodyExt, ResponseExt};

/// Basic struct to indicate pagination options
#[derive(Debug, Serialize, Deserialize)]
pub struct Paginate {
    #[serde(rename = "n")]
    n: usize,
    #[serde(rename = "last", skip_serializing_if = "Option::is_none")]
    last: Option<String>,
}

impl Paginate {
    /// Create a new pagination definition
    /// - `n` - Limit the number of entries in each response.
    /// - `last` - Result set will include values lexically after last.
    pub fn new(n: usize, last: Option<String>) -> Paginate {
        Paginate { n, last }
    }
}

/// Client for interacting with container registries which conform to the
/// OCI distribution specification (i.e: Docker Registry HTTP API V2 protocol)
#[derive(Debug)]
pub struct Client<C> {
    client: AuthClient<C>,
    registry_base: Uri,
}

impl<C: Connect + 'static> Client<C> {
    /// Construct a new Client to communicate with container registries.
    /// The `registry_uri` must have a scheme (http[s]) and authority (domain),
    /// and can optionally include a base path as well (for container registries
    /// that are not at "/"). Returns an error if the registry Uri is malformed.
    pub fn new(
        hyper_client: HyperClient<C>,
        registry_uri: &str,
        creds: Credentials,
    ) -> Result<Client<C>> {
        let registry = registry_uri
            .parse::<Uri>()
            .context(ErrorKind::RegistryUriMalformed)?;

        let mut parts = registry.into_parts();
        if parts.scheme.is_none() {
            return Err(ErrorKind::RegistryUriMissingScheme.into());
        }
        if parts.authority.is_none() {
            return Err(ErrorKind::RegistryUriMissingAuthority.into());
        }
        if parts.path_and_query.is_none() {
            parts.path_and_query = Some(uri::PathAndQuery::from_static("/"))
        }

        Ok(Client {
            client: AuthClient::new(hyper_client, creds),
            registry_base: Uri::from_parts(parts).unwrap(), // guaranteed to work
        })
    }

    /// Utility method to construct endpoint URIs
    fn base_uri(&self, endpoint: &str) -> Result<Uri> {
        let uri = format!("{}{}", self.registry_base, endpoint.trim_start_matches('/'))
            .parse::<Uri>()
            .context(ErrorKind::InvalidApiEndpoint);
        Ok(uri?)
    }

    /// Utility method to check if we can access a certain endpoint
    pub async fn check_authentication<T>(&mut self, endpoint: &str, method: T) -> Result<bool>
    where
        Method: hyper::http::HttpTryFrom<T>,
    {
        let uri = self.base_uri(endpoint)?;
        let req = Request::builder()
            .method(method)
            .uri(uri)
            .body(Body::empty())
            .context(ErrorKind::InvalidApiEndpoint)?;
        Ok(self.client.request(req).await?.status().is_success())
    }

    /// Retrieve a sorted, json list of repositories available in the registry.
    /// If the _catalog API is not available, this method returns None.
    /// Otherwise, returns a tuple of the Catalog, and the next Pagination range
    /// (if pagination was initially specified)
    pub async fn get_catalog(
        &mut self,
        paginate: Option<Paginate>,
    ) -> Result<Option<(Catalog, Option<Paginate>)>> {
        if let Some(_paginate) = paginate {
            unimplemented!("implement _catalog pagination")
        }

        let uri = self.base_uri("/v2/_catalog")?;
        let res = self.client.get(uri).await?;

        match res.status() {
            StatusCode::NOT_FOUND => Ok(None),
            StatusCode::OK => Ok(Some((
                res.into_body()
                    .json::<Catalog>()
                    .await
                    .context(ErrorKind::ApiMalformedJSON)?,
                None,
            ))),
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to reauthenticate
                unimplemented!("get_manifest: UNAUTHORIZED")
            }
            status => {
                res.dump_to_debug().await;
                Err(ErrorKind::ApiUnexpectedStatus(status).into())
            }
        }
    }

    /// Fetch the manifest identified by name and reference where reference can
    /// be a tag or digest. A HEAD request can also be issued to this endpoint
    /// to obtain resource information without receiving all data.
    pub async fn get_manifest(&mut self, reference: &Reference) -> Result<Manifest> {
        let uri = self.base_uri(
            format!("/v2/{}/manifests/{}", reference.repo(), reference.kind()).as_str(),
        )?;

        let mut req = Request::get(uri);
        req.header(header::ACCEPT, Manifest::MEDIA_TYPE);
        for &similar_mime in Manifest::SIMILAR_MEDIA_TYPES {
            // Docker compatibility
            req.header(header::ACCEPT, similar_mime);
        }
        let req = req.body(Body::empty()).unwrap();
        let res = self.client.request(req).await?;

        match res.status() {
            StatusCode::OK => {
                let manifest = res
                    .into_body()
                    .json::<Manifest>()
                    .await
                    .context(ErrorKind::ApiMalformedJSON)?;
                Ok(manifest)
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to reauthenticate
                unimplemented!("get_manifest: UNAUTHORIZED")
            }
            status => match status {
                StatusCode::BAD_REQUEST
                | StatusCode::NOT_FOUND
                | StatusCode::TOO_MANY_REQUESTS
                | StatusCode::FORBIDDEN => Err(new_api_error(res).await),
                _ => {
                    res.dump_to_debug().await;
                    Err(ErrorKind::ApiUnexpectedStatus(status).into())
                }
            },
        }
    }
}

/// Given a response that _should_ contain a well-structured ApiErrors JSON
/// value, returns a Error(ErrorKind::ApiError) who's context is either the
/// ApiErrors JSON itself, or, if the JSON was malformed, a
/// ErrorKind::ApiMalformedJSON.
async fn new_api_error(res: Response<Body>) -> Error {
    let status = res.status();
    let error = res.into_body().json::<ApiErrors>().await;
    match error {
        Err(parse_err) => parse_err
            .context(ErrorKind::ApiMalformedJSON)
            .context(ErrorKind::ApiError(status))
            .into(),
        Ok(errors) => errors.context(ErrorKind::ApiError(status)).into(),
    }
}
