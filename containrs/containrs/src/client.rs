use std::ops::{Bound, RangeBounds};

use failure::{Fail, ResultExt};
use headers::HeaderMapExt;
use headers::Range as RangeHeader;
use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{uri, HttpTryFrom, Method, Request, Response, StatusCode, Uri};
use hyper::Client as HyperClient;
// use log::*;

use docker_reference::Reference;
use oci_image::{v1::Manifest, MediaType};

use crate::auth::{AuthClient, Credentials};
use crate::error::*;
use crate::paginate::Paginate;
use crate::util::hyper::{BodyExt, ResponseExt};

/// Client for interacting with container registries that conform to the OCI
/// distribution specification (i.e: Docker Registry HTTP API V2 protocol)
#[derive(Debug)]
pub struct Client<C> {
    client: AuthClient<C>,
    registry_base: Uri,
    supports_range_header: Option<bool>, // None = Unknown
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
            supports_range_header: None,
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
    ///
    /// Returned data should deserialize into [`oci_distribution::v2::Catalog`]
    // TODO: Make this return a stream?
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
                unimplemented!("get_raw_manifest: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }

    /// Retrieve the tags under a given `repo`.
    /// Returns a tuple of [Vec<u8>] and the next [Paginate] range
    /// (if pagination is being used)
    ///
    /// Returned data should deserialize into [`oci_distribution::v2::Tags`]
    // TODO: Make this return a stream?
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
                unimplemented!("get_raw_tags: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }

    /// Retrieve the raw manifest JSON associated with the given reference,
    /// alongside the manifest's digest (as reported by the server).
    ///
    /// Returned data should deserialize into [`oci_image::v1::Manifest`]
    // TODO: use actual digest type instead of String
    // TODO: Make this return a stream?
    pub async fn get_raw_manifest(&mut self, reference: &Reference) -> Result<(Vec<u8>, String)> {
        let uri = self.base_uri(
            format!("/v2/{}/manifests/{}", reference.repo(), reference.kind()).as_str(),
        )?;

        let mut req = Request::get(uri);
        req.header(header::ACCEPT, Manifest::MEDIA_TYPE);
        for &similar_mime in Manifest::SIMILAR_MEDIA_TYPES {
            // mainly for docker compatibility
            req.header(header::ACCEPT, similar_mime);
        }
        let req = req.body(Body::empty()).unwrap();
        let res = self.client.request(req).await?;

        match res.status() {
            StatusCode::OK => {
                let digest = res
                    .headers()
                    .get("Docker-Content-Digest")
                    .ok_or_else(|| ErrorKind::ApiMissingDigestHeader)?
                    .to_str()
                    .context(ErrorKind::ApiMalformedDigestHeader)?
                    // TODO: validate string is actually a Digest
                    .to_string();
                let data = res
                    .into_body()
                    .bytes()
                    .await
                    .context(ErrorKind::ApiMalformedBody)?;
                Ok((data, digest))
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to re-authenticate
                unimplemented!("get_raw_manifest: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }

    /// Retrieve the blob with given `digest` from the specified `repo`.
    #[allow(clippy::ptr_arg)] // TODO: use proper Digest type instead of String
    pub async fn get_raw_blob(&mut self, repo: &str, digest: &String) -> Result<Body> {
        self.get_raw_blob_part(repo, digest, ..).await
    }

    /// Retrieve a byte-slice of the blob with specified `digest` from the
    /// `repo`. If the server doesn't support the use of a Range header, this
    /// function will return a [ErrorKind::ApiRangeHeaderNotSupported]
    #[allow(clippy::ptr_arg)] // TODO: use proper Digest type instead of String
    pub async fn get_raw_blob_part(
        &mut self,
        repo: &str,
        digest: &String,
        range: impl RangeBounds<u64>,
    ) -> Result<Body> {
        let uri = self.base_uri(format!("/v2/{}/blobs/{}", repo, digest).as_str())?;

        let range_header = match (range.start_bound(), range.end_bound()) {
            (Bound::Unbounded, Bound::Unbounded) | (Bound::Included(0), Bound::Unbounded) => {
                // This is an unbounded range over the whole file, which is equivalent to not
                // sending a range header at all. Just in case, don't send the range
                // header in these cases.
                None
            }
            _ => {
                if self.supports_range_header.is_none() {
                    // "This endpoint MAY also support RFC7233 compliant range requests. Support can
                    // be detected by issuing a HEAD request. If the header Accept-Range: bytes is
                    // returned, range requests can be used to fetch partial content."

                    let req = Request::head(uri.clone()).body(Body::empty()).unwrap();
                    let mut res = self.client.request(req).await?;

                    // FIXME: redirect handling needs to be more robust
                    if res.status().is_redirection() {
                        let redirect_uri = res
                            .headers()
                            .get(header::LOCATION)
                            .ok_or_else(|| ErrorKind::ApiBadRedirect)?
                            .to_str()
                            .context(ErrorKind::ApiBadRedirect)?
                            .parse::<Uri>()
                            .context(ErrorKind::ApiBadRedirect)?;

                        res = self
                            .client
                            .raw_client()
                            .get(redirect_uri)
                            .await
                            .context(ErrorKind::ApiBadRedirect)?;
                    }

                    // FIXME: this could be a stricter check
                    self.supports_range_header =
                        Some(res.headers().get(header::ACCEPT_RANGES).is_some());
                }

                if !self.supports_range_header.unwrap() {
                    return Err(ErrorKind::ApiRangeHeaderNotSupported.into());
                }

                Some(RangeHeader::bytes(range).context(ErrorKind::InvalidRange)?)
            }
        };

        let mut req = Request::get(uri);
        if let Some(range_header) = range_header.clone() {
            req.headers_mut()
                .unwrap()
                .typed_insert(range_header.clone());
        }
        let req = req.body(Body::empty()).unwrap();
        let res = self.client.request(req).await?;

        match res.status() {
            status if status.is_success() => Ok(res.into_body()),
            StatusCode::TEMPORARY_REDIRECT => {
                // FIXME: redirect handling needs to be more robust
                let redirect_uri = res
                    .headers()
                    .get(header::LOCATION)
                    .ok_or_else(|| ErrorKind::ApiBadRedirect)?
                    .to_str()
                    .context(ErrorKind::ApiBadRedirect)?
                    .parse::<Uri>()
                    .context(ErrorKind::ApiBadRedirect)?;

                let mut req = Request::get(redirect_uri);
                if let Some(range_header) = range_header.clone() {
                    req.headers_mut().unwrap().typed_insert(range_header);
                }
                let req = req.body(Body::empty()).unwrap();
                log::trace!("blob redirect req: {:#?}", req);
                let res = self
                    .client
                    .raw_client()
                    .request(req)
                    .await
                    .context(ErrorKind::ApiBadRedirect)?;
                log::trace!("blob redirect res: {:#?}", res);

                match res.status() {
                    status if status.is_success() => Ok(res.into_body()),
                    _ => Err(new_api_error(res).await),
                }
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to re-authenticate
                unimplemented!("get_raw_blob: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }
}

/// Given a response that _should_ contain a well-structured ApiErrors JSON
/// value, returns a descriptive Error about what went wrong
async fn new_api_error(res: Response<Body>) -> Error {
    let status = res.status();
    match status {
        StatusCode::BAD_REQUEST
        | StatusCode::NOT_FOUND
        | StatusCode::TOO_MANY_REQUESTS
        | StatusCode::FORBIDDEN => {
            let error = res.into_body().json::<ApiErrors>().await;
            match error {
                Err(parse_err) => parse_err
                    .context(ErrorKind::ApiMalformedJSON)
                    .context(ErrorKind::ApiError(status))
                    .into(),
                Ok(errors) => {
                    // TODO: provide better error messages if errors are valid JSON
                    errors.context(ErrorKind::ApiError(status)).into()
                }
            }
        }
        _ => {
            res.dump_to_debug().await;
            ErrorKind::ApiUnexpectedStatus(status).into()
        }
    }
}
