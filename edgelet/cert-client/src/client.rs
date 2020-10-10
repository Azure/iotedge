// Copyright (c) Microsoft. All rights reserved.

use failure::{Fail};
use futures::future::Future;
use futures::prelude::*;
use hyper::client::{Client as HyperClient};
use hyper::{Body, Client};
use typed_headers::{self, http};

use edgelet_http::{UrlConnector};

use crate::error::{Error, ErrorKind, RequestType};
use url::Url;

/// Ref <https://url.spec.whatwg.org/#path-percent-encode-set>
pub const PATH_SEGMENT_ENCODE_SET: &percent_encoding::AsciiSet =
	&percent_encoding::CONTROLS
	.add(b' ').add(b'"').add(b'<').add(b'>').add(b'`') // fragment percent-encode set
	.add(b'#').add(b'?').add(b'{').add(b'}'); // path percent-encode set

#[derive(Clone)]
pub struct CertificateClient {
	api_version: aziot_cert_common_http::ApiVersion,
    client: HyperClient<UrlConnector, Body>,
    host: Url,
}

impl CertificateClient {
    pub fn new(api_version: aziot_cert_common_http::ApiVersion, url: &url::Url) -> Self {
        let client = Client::builder()
            .build(UrlConnector::new(
                &url).expect("Hyper client"));
        CertificateClient {
            api_version,
            client,
            host: url.clone(),
        }
    }

    pub fn create_cert(
        &self,
        id: &str,
		csr: &[u8],
		issuer: Option<(&str, &aziot_key_common::KeyHandle)>,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = Error> + Send>
    {
        let uri = format!("{}certificates?api-version={}", self.host.as_str(), self.api_version);
        let body = aziot_cert_common_http::create_cert::Request {
			cert_id: id.to_owned(),
			csr: aziot_cert_common_http::Pem(csr.to_owned()),
			issuer: issuer.map(|(cert_id, private_key_handle)| aziot_cert_common_http::create_cert::Issuer {
				cert_id: cert_id.to_owned(),
				private_key_handle: private_key_handle.clone(),
			}),
		};

        let res = request::<_, aziot_cert_common_http::create_cert::Request, aziot_cert_common_http::create_cert::Response>(
            &self.client,
            hyper::Method::POST,
            &uri,
            Some(&body),
        )
        .and_then(|res| Ok(res.pem.0))
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::CreateCertificate))));
        Box::new(res)
    }
    
    pub fn import_cert(
		&self,
		id: &str,
		pem: &[u8],
	) -> Box<dyn Future<Item = Vec<u8>, Error = Error> + Send> 
    {
        let uri = format!("{}certificates/{}?api-version={}", self.host.as_str(), percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET), self.api_version);
        let body = aziot_cert_common_http::import_cert::Request {
			pem: aziot_cert_common_http::Pem(pem.to_owned()),
		};

        let res = request::<_, aziot_cert_common_http::import_cert::Request, aziot_cert_common_http::import_cert::Response>(
            &self.client,
            hyper::Method::POST,
            &uri,
            Some(&body),
        )
        .and_then(|res| Ok(res.pem.0))
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::ImportCertificate))));
        Box::new(res)
    }

    pub fn get_cert(
		&self,
		id: &str,
    ) ->  Box<dyn Future<Item = Vec<u8>, Error = Error> + Send>
    {
		let uri = format!("{}certificates/{}?api-version={}", self.host.as_str(), percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET), self.api_version);

		let res = request::<_, (), aziot_cert_common_http::get_cert::Response>(
			&self.client,
			http::Method::GET,
			&uri,
			None,
        )
        .and_then(|res| Ok(res.pem.0))
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::GetCertificate))));
        Box::new(res)
	}

	pub fn delete_cert(
		&self,
		id: &str,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send> 
    {
		let uri = format!("{}certificates/{}?api-version={}", self.host.as_str(), percent_encoding::percent_encode(id.as_bytes(), PATH_SEGMENT_ENCODE_SET), self.api_version);

		let res = request::<_, (), _>(
			&self.client,
			http::Method::DELETE,
			&uri,
			None,
        )
        .map_err(|e| Error::from(e.context(ErrorKind::JsonParse(RequestType::DeleteCertificate))));
        Box::new(res)
	}
}

fn request<TConnect, TRequest, TResponse>(
    client: &hyper::Client<TConnect, hyper::Body>,
    method: http::Method,
    uri: &str,
    body: Option<&TRequest>,
) -> Box<dyn Future<Item = TResponse, Error = Error> + Send>
where
    TConnect: hyper::client::connect::Connect + Clone + Send + Sync + 'static,
    TRequest: serde::Serialize,
    TResponse: serde::de::DeserializeOwned + Send + 'static,
{
    let mut builder = hyper::Request::builder();
    builder.method(method).uri(uri);
    
    let builder =
    if let Some(body) = body {
        let body = serde_json::to_vec(body).expect("serializing request body to JSON cannot fail").into();
        builder
            .header(hyper::header::CONTENT_TYPE, "application/json")
            .body(body)
    }
    else {
        builder.body(Default::default())
    };
    
    let req = builder.expect("cannot fail to create hyper request");
    
    Box::new(
        client
        .request(req)
        .map_err(|e| Error::from(e.context(ErrorKind::Request)))
        .and_then(|resp| {
            let (http::response::Parts { status, .. }, body) = resp.into_parts();
            body.concat2()
                .and_then(move |body| Ok((status, body)))
                .map_err(|e| Error::from(e.context(ErrorKind::Hyper)))
        })
        .and_then(|(status, body)| {
            if status.is_success() {
                Ok(body)
            } else {
                Err(Error::http_with_error_response(status, &*body))
            }
        })
        .and_then(|body| {
            let parsed: Result<TResponse, _> =
                serde_json::from_slice(&body);
            parsed.map_err(|e| Error::from(ErrorKind::Serde(e)))
        })
    )
}
