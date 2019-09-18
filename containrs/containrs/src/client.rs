use hyper::body::Body;
use hyper::client::connect::Connect;
use hyper::header;
use hyper::http::{Method, Request, StatusCode, Uri};
use hyper::Client as HyperClient;
// use log::*;
use serde::{Deserialize, Serialize};

use oci_distribution::v2::Catalog;
use oci_image::{v1::Manifest, MediaType};

use crate::auth::{AuthClient, Credentials};
use crate::reference::Reference;
use crate::util::hyper::BodyExt;
use crate::Result;

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
    domain: String,
}

impl<C: Connect + 'static> Client<C> {
    /// Construct a new Client pointing to the given `domain` using the given
    /// `creds`
    pub fn new(hyper_client: HyperClient<C>, domain: &str, creds: Credentials) -> Client<C> {
        Client {
            client: AuthClient::new(hyper_client, creds),
            domain: domain.to_owned(),
        }
    }

    /// Utility method to cut down on the boilerplate required to poke URIs.
    fn base_uri(&self, endpoint: &str) -> std::result::Result<Uri, hyper::http::Error> {
        Uri::builder()
            .scheme("https")
            .authority(self.domain.as_str())
            .path_and_query(endpoint)
            .build()
    }

    /// Utility method to check if we can access a certain endpoint
    async fn check_authentication(&mut self, endpoint: &str, method: Method) -> Result<bool> {
        let uri = self.base_uri(endpoint)?;
        let req = Request::builder()
            .method(method)
            .uri(uri)
            .body(Body::empty())?;
        Ok(self.client.request(req).await?.status() != StatusCode::UNAUTHORIZED)
    }

    /// Check that basic authentication works (by pinging the base URL:
    /// <domain>/v2/)
    pub async fn check_basic_auth(&mut self) -> Result<bool> {
        self.check_authentication("/v2/", Method::GET).await
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

        Ok(match res.status() {
            StatusCode::NOT_FOUND => None,
            StatusCode::OK => Some((res.into_body().json::<Catalog>().await?, None)),
            other_status => panic!(
                "Recieved unexpected status ({:?}) from server",
                other_status
            ),
        })
    }

    /// Fetch the manifest identified by name and reference where reference can
    /// be a tag or digest. A HEAD request can also be issued to this endpoint
    /// to obtain resource information without receiving all data.
    pub async fn get_manifest(&mut self, reference: &Reference) -> Result<Manifest> {
        let uri = self.base_uri(
            format!(
                "/v2/{}/manifests/{}",
                reference.name(),
                reference.reference_kind()
            )
            .as_str(),
        )?;

        let mut req = Request::get(uri);
        req.header(header::ACCEPT, Manifest::MEDIA_TYPE);
        for &similar_mime in Manifest::SIMILAR_MEDIA_TYPES {
            // Docker compatibility
            req.header(header::ACCEPT, similar_mime);
        }
        let req = req.body(Body::empty())?;
        let res = self.client.request(req).await?;

        match res.status() {
            StatusCode::OK => {
                let manifest = res.into_body().json::<Manifest>().await?;
                Ok(manifest)
            }
            StatusCode::BAD_REQUEST => unimplemented!("get_manifest: BAD_REQUEST"),
            StatusCode::NOT_FOUND => unimplemented!("get_manifest: NOT_FOUND"),
            StatusCode::FORBIDDEN => unimplemented!("get_manifest: FORBIDDEN"),
            StatusCode::TOO_MANY_REQUESTS => unimplemented!("get_manifest: TOO_MANY_REQUESTS"),
            StatusCode::UNAUTHORIZED => unimplemented!("get_manifest: UNAUTHORIZED"),
            other_status => panic!(
                "Recieved unexpected status ({:?}) from server",
                other_status
            ),
        }
    }
}
