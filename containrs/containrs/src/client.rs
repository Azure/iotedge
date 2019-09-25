use failure::{Fail, ResultExt};
use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{uri, HttpTryFrom, Method, Request, Response, StatusCode, Uri};
use hyper::Client as HyperClient;
// use log::*;

use docker_reference::Reference;
use oci_distribution::v2::{Catalog, Tags};
use oci_image::{v1::Manifest, MediaType};

use crate::auth::{AuthClient, Credentials};
use crate::error::*;
use crate::paginate::Paginate;
use crate::util::hyper::{BodyExt, ResponseExt};

/// Client for interacting with container registries which conform to the
/// OCI distribution specification (i.e: Docker Registry HTTP API V2 protocol)
#[derive(Debug)]
pub struct Client<C> {
    client: AuthClient<C>,
    registry_base: Uri,
}

impl<C: Connect + 'static> Client<C> {
    /// Construct a new Client to communicate with container registries.
    /// The `registry_uri` must have an authority component (i.e: domain),
    /// and can optionally include a base path as well (for container registries
    /// that are not at "/"). Returns an error if the registry Uri is malformed.
    pub fn new(
        hyper_client: HyperClient<C>,
        scheme: &str,
        registry_uri: &str,
        creds: Credentials,
    ) -> Result<Client<C>> {
        let registry = registry_uri
            .parse::<Uri>()
            .context(ErrorKind::ClientRegistryUriMalformed)?;

        let mut parts = registry.into_parts();
        match parts.scheme {
            Some(_) => return Err(ErrorKind::ClientRegistryUriHasScheme.into()),
            None => {
                parts.scheme =
                    Some(uri::Scheme::try_from(scheme).context(ErrorKind::ClientMalformedScheme)?)
            }
        }

        if parts.authority.is_none() {
            return Err(ErrorKind::ClientRegistryUriMissingAuthority.into());
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

    /// Utility method to check authentication with a particular endpoint
    /// (e.g: /v2/)
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

    /// Retrieve a sorted, JSON list of repositories available in the registry.
    /// If the _catalog API is not available, this method returns None.
    /// If the _catalog API is available, returns a tuple [Vec<u8>]
    /// and the next [Paginate] range (if pagination is being used)
    pub async fn get_raw_catalog(
        &mut self,
        paginate: Option<Paginate>,
    ) -> Result<Option<(Vec<u8>, Option<Paginate>)>> {
        let uri = match paginate {
            Some(Paginate { n, last }) => {
                self.base_uri(format!("/v2/_catalog?n={}&last={}", n, last).as_str())?
            }
            None => self.base_uri("/v2/_catalog")?,
        };

        let res = self.client.get(uri).await?;

        match res.status() {
            StatusCode::NOT_FOUND => Ok(None),
            StatusCode::OK => {
                let next_paginate = res
                    .headers()
                    .get(header::LINK)
                    .map(Paginate::from_link_header)
                    .transpose()?;
                Ok(Some((
                    res.into_body()
                        .bytes()
                        .await
                        .context(ErrorKind::ApiMalformedBody)?,
                    next_paginate,
                )))
            }
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

    /// Fetch the tags under a given `repo`.
    /// Returns a tuple of [Vec<u8>] and the next [Paginate] range
    /// (if pagination is being used)
    pub async fn get_raw_tags(
        &mut self,
        repo: &str,
        paginate: Option<Paginate>,
    ) -> Result<(Vec<u8>, Option<Paginate>)> {
        let uri = match paginate {
            Some(Paginate { n, last }) => {
                self.base_uri(format!("/v2/{}/tags/list?n={}&last={}", repo, n, last).as_str())?
            }
            None => self.base_uri(format!("/v2/{}/tags/list", repo).as_str())?,
        };

        let res = self.client.get(uri).await?;

        match res.status() {
            StatusCode::OK => {
                let next_paginate = res
                    .headers()
                    .get(header::LINK)
                    .map(Paginate::from_link_header)
                    .transpose()?;
                Ok((
                    res.into_body()
                        .bytes()
                        .await
                        .context(ErrorKind::ApiMalformedBody)?,
                    next_paginate,
                ))
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to reauthenticate
                unimplemented!("get_tags: UNAUTHORIZED")
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

    /// Fetch the raw manifest JSON associated with the given reference.
    pub async fn get_raw_manifest(&mut self, reference: &Reference) -> Result<Vec<u8>> {
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
            StatusCode::OK => Ok(res
                .into_body()
                .bytes()
                .await
                .context(ErrorKind::ApiMalformedBody)?),
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to re-authenticate
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

    /// Retrieve a sorted, JSON list of repositories available in the registry.
    /// If the _catalog API is not available, this method returns None.
    /// If the _catalog API is available, returns a tuple [Catalog] and
    /// the next [Paginate] range (if pagination is being used)
    pub async fn get_catalog(
        &mut self,
        paginate: Option<Paginate>,
    ) -> Result<Option<(Catalog, Option<Paginate>)>> {
        self.get_raw_catalog(paginate)
            .await?
            .map(|(raw_catalog, paginate)| {
                Ok((
                    serde_json::from_slice::<Catalog>(&raw_catalog)
                        .context(ErrorKind::ApiMalformedJSON)?,
                    paginate,
                ))
            })
            .transpose()
    }

    /// Fetch the tags under a given `repo`, performing additional checks to
    /// ensure the JSON is spec-compliant.
    pub async fn get_tags(
        &mut self,
        repo: &str,
        paginate: Option<Paginate>,
    ) -> Result<(Tags, Option<Paginate>)> {
        let (raw_tags, paginate) = self.get_raw_tags(repo, paginate).await?;
        Ok((
            serde_json::from_slice::<Tags>(&raw_tags).context(ErrorKind::ApiMalformedJSON)?,
            paginate,
        ))
    }

    /// Fetch the Manifest associated with the given reference, performing
    /// additional checks to ensure the JSON is spec-compliant.
    pub async fn get_manifest(&mut self, reference: &Reference) -> Result<Manifest> {
        let manifest = serde_json::from_slice::<Manifest>(&self.get_raw_manifest(reference).await?)
            .context(ErrorKind::ApiMalformedJSON)?;

        if manifest.schema_version != 2 {
            return Err(ErrorKind::InvalidSchemaVersion(manifest.schema_version).into());
        }

        Ok(manifest)
    }
}

/// Given a response that _should_ contain a well-structured ApiErrors JSON
/// value, returns a Error(ErrorKind::ApiError) who's context is either the
/// ApiErrors JSON itself, or, if the JSON was malformed, a
/// ErrorKind::ApiMalformedJSON.
// TODO: provide better error messages if there is valid JSON in the body
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
