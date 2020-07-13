use bytes::buf::BufExt;
use chrono::{DateTime, Utc};
use hyper::{body, Body, Client};

use crate::edgelet::{
    make_hyper_uri, ApiError, CertificateResponse, Connector, Scheme, ServerCertificateRequest,
};
use http::{Request, StatusCode};

pub struct WorkloadClient {
    client: Client<Connector, Body>,
    scheme: Scheme,
}

impl WorkloadClient {
    pub(crate) fn new(client: Client<Connector, Body>, scheme: Scheme) -> Self {
        Self { client, scheme }
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
        let body = serde_json::to_string(&req).map_err(ApiError::SerializeRequestBody)?;
        let req = Request::post(uri)
            .body(Body::from(body))
            .map_err(ApiError::ConstructRequest)?;

        let res = self
            .client
            .request(req)
            .await
            .map_err(ApiError::ExecuteRequest)?;

        if res.status() != StatusCode::OK {
            return Err(ApiError::UnsuccessfulResponse(res.status()).into());
        }

        let body = body::aggregate(res).await.map_err(ApiError::ReadResponse)?;

        let cert = serde_json::from_reader(body.reader()).map_err(ApiError::ParseResponseBody)?;

        Ok(cert)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum WorkloadError {
    #[error("could not make workload API call")]
    Api(#[from] ApiError),
}

#[cfg(test)]
mod tests {
    use chrono::{Duration, Utc};
    use http::StatusCode;
    use matches::assert_matches;
    use mockito::mock;
    use serde_json::json;

    use super::{make_hyper_uri, ApiError, Scheme, WorkloadError};
    use crate::edgelet::workload;

    #[test]
    fn it_makes_hyper_uri() {
        let scheme = Scheme::Unix("unix:///var/iotedge/workload.sock".into());
        let path = "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30";

        let uri = make_hyper_uri(&scheme, &path).unwrap();
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
        .with_status(200)
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
            WorkloadError::Api(ApiError::UnsuccessfulResponse(StatusCode::BAD_REQUEST))
        )
    }
}
