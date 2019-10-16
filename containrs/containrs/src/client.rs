use std::ops::{Bound, RangeBounds};

use bytes::Bytes;
use failure::{Fail, ResultExt};
use futures::lock::Mutex;
use headers::HeaderMapExt as TypedHeaderMapExt;
use headers::Range as RangeHeader;
use log::*;
use reqwest::header::{self, HeaderMap};
use reqwest::{Client as ReqwestClient, Method, Response, StatusCode, Url};

use docker_reference::{Reference, ReferenceKind};
use docker_scope::{Action, Resource, Scope};
use oci_digest::Digest;
use oci_image::{
    v1::{Descriptor, Manifest},
    MediaType,
};

use crate::auth::{AuthClient, Credentials};
use crate::blob::Blob;
use crate::error::*;
use crate::paginate::Paginate;

/// Client for interacting with container registries that conform to the OCI
/// distribution specification (i.e: Docker Registry HTTP API V2 protocol)
#[derive(Debug)]
pub struct Client {
    client: AuthClient,
    registry_base: Url,
    supports_range_header: Mutex<Option<bool>>,
}

impl Client {
    /// Construct a new Client to communicate with container registries.
    /// The `registry_url` must have an authority component (i.e: domain),
    /// and can optionally include a base path as well (for container registries
    /// that are not at "/"). Returns an error if the registry Url is malformed.
    pub fn new(scheme: &str, registry_url: &str, creds: Credentials) -> Result<Client> {
        let registry_url = scheme.to_string() + "://" + registry_url;

        let registry_base = registry_url
            .parse::<Url>()
            .context(ErrorKind::ClientRegistryUrlMalformed)?;

        if !registry_base.has_host() {
            return Err(ErrorKind::ClientRegistryUrlMissingAuthority.into());
        }
        if registry_base.query().is_some() {
            return Err(ErrorKind::ClientRegistryUrlIncludesQuery.into());
        }

        Ok(Client {
            client: AuthClient::new(ReqwestClient::new(), creds),
            registry_base,
            supports_range_header: Mutex::new(None),
        })
    }

    /// Retrieve a sorted, JSON list of repositories available in the registry.
    /// If the _catalog API is not available, this method returns None.
    /// If the _catalog API is available, returns a tuple [Bytes]
    /// and the next [Paginate] range (if pagination is being used)
    ///
    /// Returned data should deserialize into [`oci_distribution::v2::Catalog`]
    pub async fn get_raw_catalog(
        &self,
        paginate: Option<Paginate>,
    ) -> Result<Option<(Bytes, Option<Paginate>)>> {
        let scope = Scope::new(Resource::registry("catalog"), &[Action::Wildcard]);

        let mut url = self.registry_base.join("/v2/_catalog/").unwrap();
        if let Some(Paginate { n, last }) = paginate {
            url.query_pairs_mut()
                .append_pair("n", &n.to_string())
                .append_pair("last", &last.to_string());
        }

        let res = self.client.get(url, &scope).await?.send().await?;

        match res.status() {
            StatusCode::NOT_FOUND => Ok(None),
            StatusCode::OK => {
                let next_paginate = res
                    .headers()
                    .get(header::LINK)
                    .map(|header| Paginate::from_link_header(header, &self.registry_base))
                    .transpose()?;
                Ok(Some((
                    res.bytes().await.context(ErrorKind::ApiMalformedBody)?,
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
    /// Returns a tuple of [Bytes] and the next [Paginate] range
    /// (if pagination is being used)
    ///
    /// Returned data should deserialize into [`oci_distribution::v2::Tags`]
    pub async fn get_raw_tags(
        &self,
        repo: &str,
        paginate: Option<Paginate>,
    ) -> Result<(Bytes, Option<Paginate>)> {
        let scope = Scope::new(Resource::repo(repo), &[Action::Pull, Action::MetadataRead]);

        let mut url = self
            .registry_base
            .join(format!("/v2/{}/tags/list", repo).as_str())
            .context(ErrorKind::InvalidApiEndpoint)?;

        if let Some(Paginate { n, last }) = paginate {
            url.query_pairs_mut()
                .append_pair("n", &n.to_string())
                .append_pair("last", &last.to_string());
        }

        let res = self.client.get(url, &scope).await?.send().await?;

        match res.status() {
            StatusCode::OK => {
                let next_paginate = res
                    .headers()
                    .get(header::LINK)
                    .map(|header| Paginate::from_link_header(header, &self.registry_base))
                    .transpose()?;
                Ok((
                    res.bytes().await.context(ErrorKind::ApiMalformedBody)?,
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
    pub async fn get_raw_manifest(&self, reference: &Reference) -> Result<Blob> {
        // TODO: double check this scope
        let scope = Scope::new(Resource::repo(reference.repo()), &[Action::Pull]);

        let url = self
            .registry_base
            .join(
                format!(
                    "/v2/{}/manifests/{}",
                    reference.repo(),
                    reference.kind().as_str()
                )
                .as_str(),
            )
            .context(ErrorKind::InvalidApiEndpoint)?;

        let mut req = self.client.request(Method::GET, url, &scope).await?;
        req = req.header(header::ACCEPT, Manifest::MEDIA_TYPE);
        for &similar_mime in Manifest::SIMILAR_MEDIA_TYPES {
            req = req.header(header::ACCEPT, similar_mime);
        }
        let res = req.send().await?;

        match res.status() {
            StatusCode::OK => {
                let content_type = res.headers().get_required("Content-Type")?;
                let server_digest = res.headers().get_required("Docker-Content-Digest")?;
                let content_length = res.headers().get_required("Content-Length")?;

                // If we already know the expected digest, make sure the server returned the
                // same digest header
                if let ReferenceKind::Digest(ref d) = reference.kind() {
                    if d != &server_digest {
                        return Err(ErrorKind::ApiMismatchedDigest.into());
                    }
                }

                Ok(Blob::new_streaming(
                    res,
                    Descriptor::new_base(content_type, server_digest, content_length),
                ))
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to re-authenticate
                unimplemented!("get_raw_manifest: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }

    /// Retrieve the blob with given `digest` from the specified `repo`.
    pub async fn get_raw_blob(&self, repo: &str, digest: &Digest) -> Result<Blob> {
        self.get_raw_blob_part(repo, digest, ..).await
    }

    /// Retrieve a slice of the blob with specified `digest` from the `repo`.
    ///
    /// If the server doesn't support the use of a  Range header, this function
    /// will return a [ErrorKind::ApiRangeHeaderNotSupported]
    pub async fn get_raw_blob_part(
        &self,
        repo: &str,
        digest: &Digest,
        range: impl RangeBounds<u64>,
    ) -> Result<Blob> {
        // TODO: double check this scope
        let scope = Scope::new(Resource::repo(repo), &[Action::Pull]);

        let url = self
            .registry_base
            .join(format!("/v2/{}/blobs/{}", repo, digest).as_str())
            .context(ErrorKind::InvalidApiEndpoint)?;

        let range_header = match (range.start_bound(), range.end_bound()) {
            (Bound::Unbounded, Bound::Unbounded) | (Bound::Included(0), Bound::Unbounded) => {
                // This is an unbounded range over the whole file, which is equivalent to not
                // sending a range header at all. Just in case, don't send the range
                // header in these cases.
                None
            }
            _ => {
                // TODO: this would be slightly more efficient if it was a rwlock
                // unfortunately, there doesn't seem to be a futures-aware rwlock yet...
                let mut supports_range_header = self.supports_range_header.lock().await;

                if supports_range_header.is_none() {
                    // "This endpoint MAY also support RFC7233 compliant range requests. Support can
                    // be detected by issuing a HEAD request. If the header Accept-Range: bytes is
                    // returned, range requests can be used to fetch partial content."

                    let res = self.client.head(url.clone(), &scope).await?.send().await?;

                    // FIXME: this could be a stricter check
                    supports_range_header
                        .replace(res.headers().get(header::ACCEPT_RANGES).is_some());
                }

                // won't panic, as it's guaranteed to be set in the if statement above
                if !supports_range_header.unwrap() {
                    return Err(ErrorKind::ApiRangeHeaderNotSupported.into());
                }

                Some(RangeHeader::bytes(range).context(ErrorKind::InvalidRange)?)
            }
        };

        let mut req = self.client.get(url, &scope).await?;
        if let Some(range_header) = range_header {
            let mut m = HeaderMap::new();
            m.typed_insert(range_header);
            req = req.headers(m);
        }
        let res = req.send().await?;

        match res.status() {
            status if status.is_success() => {
                // TODO?: check if server digest matches expected digest
                //
                // It's not mission critical to implement, as the only benefit would be to
                // detect a misbehaving server.
                //
                // this is surprisingly tricky to do, since request transparently handles
                // redirects. this is usually exactly what we want, but unfortunately, the
                // initial redirect response is the one with the digest header...

                // let server_digest = res.headers().get_required("Docker-Content-Digest")?;

                // // Make sure the server returned the same digest header
                // if digest != &server_digest {
                //     return Err(ErrorKind::ApiInvalidDigestHeader.into());
                // }

                let content_type = res.headers().get_required("Content-Type")?;
                let content_length = res.headers().get_required("Content-Length")?;

                Ok(Blob::new_streaming(
                    res,
                    Descriptor::new_base(content_type, digest.clone(), content_length),
                ))
            }
            StatusCode::UNAUTHORIZED => {
                // TODO: attempt to re-authenticate
                unimplemented!("get_raw_blob: UNAUTHORIZED")
            }
            _ => Err(new_api_error(res).await),
        }
    }
}

trait HeaderMapExt {
    /// Utility method to simplify extracting information from required headers
    fn get_required<T>(&self, header_key: &'static str) -> Result<T>
    where
        T: std::str::FromStr,
        T::Err: Fail;
}

impl HeaderMapExt for HeaderMap {
    fn get_required<T>(&self, header_key: &'static str) -> Result<T>
    where
        T: std::str::FromStr,
        T::Err: Fail,
    {
        Ok(self
            .get(header_key)
            .ok_or_else(|| ErrorKind::ApiMissingHeader(header_key))?
            .to_str()
            .context(ErrorKind::ApiMalformedHeader(header_key))?
            .parse::<T>()
            .context(ErrorKind::ApiMalformedHeader(header_key))?)
    }
}

/// Given a response that _should_ contain a well-structured ApiErrors JSON
/// value, returns a descriptive Error about what went wrong
async fn new_api_error(res: Response) -> Error {
    let status = res.status();
    match status {
        StatusCode::BAD_REQUEST
        | StatusCode::NOT_FOUND
        | StatusCode::TOO_MANY_REQUESTS
        | StatusCode::FORBIDDEN => {
            let error = res.json::<ApiErrors>().await;
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
            debug!("Unexpected response: {:#?}", res);
            debug!("Unexpected response content: {:#?}", res.text().await);
            ErrorKind::ApiUnexpectedStatus(status).into()
        }
    }
}
