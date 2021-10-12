use std::{io::Read, str};

use bytes::buf::Buf;
use chrono::{DateTime, Utc};
use http::{Request, StatusCode, Uri};
use hyper::{body, Body, Client};
use percent_encoding::percent_encode;
use serde::{de::DeserializeOwned, Serialize};

use crate::{
    make_hyper_uri, ApiError, CertificateResponse, Connector, IdentityCertificateRequest, Scheme,
    ServerCertificateRequest, SignRequest, SignResponse, TrustBundleResponse, IOTHUB_ENCODE_SET,
    PATH_SEGMENT_ENCODE_SET,
};

#[derive(Debug)]
pub struct WorkloadClient {
    client: Client<Connector>,
    scheme: Scheme,
}

impl WorkloadClient {
    pub(crate) fn new(client: Client<Connector>, scheme: Scheme) -> Self {
        Self { client, scheme }
    }

    pub async fn create_identity_cert(
        &self,
        module_id: &str,
        expiration: DateTime<Utc>,
    ) -> Result<CertificateResponse, WorkloadError> {
        let path = format!(
            "/modules/{}/certificate/identity?api-version=2019-01-30",
            module_id,
        );

        let uri =
            make_hyper_uri(&self.scheme, &path).map_err(|e| ApiError::ConstructRequestUrl(e))?;

        let req = IdentityCertificateRequest::new(Some(expiration.to_rfc3339()));
        self.post(uri, req, StatusCode::CREATED).await
    }

    pub async fn create_server_cert(
        &self,
        module_id: &str,
        generation_id: &str,
        hostname: &str,
        expiration: DateTime<Utc>,
    ) -> Result<CertificateResponse, WorkloadError> {
        let path = format!(
            "/modules/{}/genid/{}/certificate/server?api-version=2019-01-30",
            module_id, generation_id
        );
        let uri =
            make_hyper_uri(&self.scheme, &path).map_err(|e| ApiError::ConstructRequestUrl(e))?;

        let req = ServerCertificateRequest::new(hostname.to_string(), expiration.to_rfc3339());
        self.post(uri, req, StatusCode::CREATED).await
    }

    pub async fn sign(
        &self,
        module_id: &str,
        generation_id: &str,
        data: &str,
    ) -> Result<SignResponse, WorkloadError> {
        let path = format!(
            "/modules/{name}/genid/{genid}/sign?api-version=2019-01-30",
            name = percent_encode(module_id.as_bytes(), IOTHUB_ENCODE_SET),
            genid = percent_encode(generation_id.as_bytes(), PATH_SEGMENT_ENCODE_SET),
        );

        let uri =
            make_hyper_uri(&self.scheme, &path).map_err(|e| ApiError::ConstructRequestUrl(e))?;

        let req = SignRequest::new(base64::encode(data.to_string()));
        self.post(uri, req, StatusCode::OK).await
    }

    pub async fn trust_bundle(&self) -> Result<TrustBundleResponse, WorkloadError> {
        let uri = make_hyper_uri(&self.scheme, "/trust-bundle?api-version=2019-01-30")
            .map_err(|e| ApiError::ConstructRequestUrl(e))?;

        self.get(uri, StatusCode::OK).await
    }

    async fn post<Req: Serialize, Resp: DeserializeOwned>(
        &self,
        uri: Uri,
        req: Req,
        expected_status: StatusCode,
    ) -> Result<Resp, WorkloadError> {
        let body = serde_json::to_string(&req).map_err(ApiError::SerializeRequestBody)?;

        let req = Request::post(uri)
            .body(Body::from(body))
            .map_err(ApiError::ConstructRequest)?;

        self.read_response(req, expected_status).await
    }

    async fn get<Resp: DeserializeOwned>(
        &self,
        uri: Uri,
        expected_status: StatusCode,
    ) -> Result<Resp, WorkloadError> {
        let req = Request::get(uri)
            .body(Body::default())
            .map_err(ApiError::ConstructRequest)?;

        self.read_response(req, expected_status).await
    }

    async fn read_response<Resp: DeserializeOwned>(
        &self,
        req: Request<Body>,
        expected_status: StatusCode,
    ) -> Result<Resp, WorkloadError> {
        let res = self
            .client
            .request(req)
            .await
            .map_err(ApiError::ExecuteRequest)?;

        let status = res.status();
        let body = body::aggregate(res)
            .await
            .map_err(|e| ApiError::ReadResponse(Box::new(e)))?;

        if status != expected_status {
            let mut text = String::new();
            body.reader()
                .read_to_string(&mut text)
                .map_err(|e| ApiError::ReadResponse(Box::new(e)))?;
            return Err(ApiError::UnsuccessfulResponse(status, text).into());
        }

        let response =
            serde_json::from_reader(body.reader()).map_err(ApiError::ParseResponseBody)?;
        Ok(response)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum WorkloadError {
    #[error("could not make workload API call")]
    Api(#[from] ApiError),
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use chrono::{Duration, Utc};
    use http::StatusCode;
    use matches::assert_matches;
    use mockito::mock;
    use serde_json::json;

    use super::{make_hyper_uri, ApiError, Scheme, WorkloadError};
    use crate::workload;

    #[test]
    fn it_makes_hyper_uri() {
        let scheme = Scheme::Unix("unix:///var/iotedge/aziot-edged.workload.sock".into());
        let path = "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30";

        let uri = make_hyper_uri(&scheme, path).unwrap();
        assert!(uri.to_string().ends_with(path));
    }

    #[tokio::test]
    async fn it_downloads_server_certificate() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "PRIVATE KEY" },
                "certificate": "CERTIFICATE",
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/broker/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client
            .create_server_cert("broker", "12345678", "localhost", expiration)
            .await
            .unwrap();

        assert_eq!(res.private_key().bytes(), Some("PRIVATE KEY"));
        assert_eq!(res.certificate(), "CERTIFICATE");
        assert_eq!(res.expiration(), &expiration.to_rfc3339());
    }

    #[tokio::test]
    async fn it_downloads_identity_certificate() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "PRIVATE KEY" },
                "certificate": "CERTIFICATE",
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/broker/certificate/identity?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client
            .create_identity_cert("broker", expiration)
            .await
            .unwrap();

        assert_eq!(res.private_key().bytes(), Some("PRIVATE KEY"));
        assert_eq!(res.certificate(), "CERTIFICATE");
    }

    #[tokio::test]
    async fn it_handles_incorrect_status_for_create_server_cert() {
        let expiration = Utc::now() + Duration::days(90);
        let _m = mock(
            "POST",
            "/modules/broker/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(400)
        .with_body(r#"{"message":"Something went wrong"}"#)
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client
            .create_server_cert("broker", "12345678", "locahost", expiration)
            .await
            .unwrap_err();

        assert_matches!(
            res,
            WorkloadError::Api(ApiError::UnsuccessfulResponse(StatusCode::BAD_REQUEST, _))
        )
    }

    #[tokio::test]
    async fn it_downloads_trust_bundle() {
        let res = json!( { "certificate": "CERTIFICATE" } );

        let _m = mock("GET", "/trust-bundle?api-version=2019-01-30")
            .with_status(200)
            .with_body(serde_json::to_string(&res).unwrap())
            .create();
        let client = workload(&mockito::server_url()).expect("client");
        let res = client.trust_bundle().await.unwrap();

        assert_eq!(res.certificate(), "CERTIFICATE");
    }

    #[tokio::test]
    async fn it_signs_request() {
        let res = json!( { "digest": "signed-digest" } );

        let body = format!(
            "{}{}{}",
            "{\"keyId\":\"primary\",\"algo\":\"HMACSHA256\",\"data\":\"",
            base64::encode("digest"),
            "\"}"
        );

        let _m = mock(
            "POST",
            "/modules/%24edgeHub/genid/12345678/sign?api-version=2019-01-30",
        )
        .match_body(body.as_str())
        .with_status(200)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client.sign("$edgeHub", "12345678", "digest").await.unwrap();

        assert_eq!(res.digest(), "signed-digest");
    }
}
