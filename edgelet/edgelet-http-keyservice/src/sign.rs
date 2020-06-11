// Copyright (c) Microsoft. All rights reserved.
use crate::error::{Error, ErrorKind};
use edgelet_core::crypto::{Sign, SignatureAlgorithm};
use edgelet_core::Signature;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use failure::ResultExt;
use futures::{Future, Stream};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};

use crate::IntoResponse;
use keyservice::models::{SignRequest, SignResponse};
use std::str::FromStr;

pub struct SignHandler<K> {
    key: K,
}

impl<K> SignHandler<K> {
    pub fn new(key: K) -> Self {
        SignHandler { key }
    }
}

impl<K> Handler<Parameters> for SignHandler<K>
where
    K: Sign + Clone + Send + Sync + 'static,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let key = self.key.clone();

        let response = req
            .into_body()
            .concat2()
            .then(move |b| {
                let b = b.context(ErrorKind::MalformedRequestBody)?;
                let request = serde_json::from_slice::<SignRequest>(&b)
                    .context(ErrorKind::MalformedRequestBody)?;
                let message = base64::decode(request.parameters().message())
                    .context(ErrorKind::MalformedRequestBody)?;
                let algorithm = SignatureAlgorithm::from_str(request.algorithm())
                    .context(ErrorKind::InvalidSignatureAlgorithm)?;
                let signature = key
                    .sign(algorithm, &message)
                    .map(|s| base64::encode(s.as_bytes()))
                    .context(ErrorKind::GetSignature)?;

                let sign_response = SignResponse::new(signature);

                let body =
                    serde_json::to_string(&sign_response).context(ErrorKind::GetSignature)?;

                let resp = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/json")
                    .header(CONTENT_LENGTH, body.len().to_string().as_str())
                    .body(body.into())
                    .with_context(|_| ErrorKind::GetSignature)?;

                Ok(resp)
            })
            .or_else(|e: Error| Ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use crate::sign::SignHandler;
    use edgelet_core::crypto::MemoryKey;
    use edgelet_http::route::{Handler, Parameters};
    use futures::{Future, Stream};
    use hyper::{Request, StatusCode};
    use keyservice::models::{ErrorResponse, SignParameters, SignRequest, SignResponse};
    use url::percent_encoding::{define_encode_set, percent_encode, PATH_SEGMENT_ENCODE_SET};

    #[test]
    fn bad_body_not_json() {
        // arrange
        let key = MemoryKey::new(base64::decode("primarykey").unwrap());
        let handler = SignHandler::new(key);

        let body = "not valid json";

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Request body is malformed\n\tcaused by: expected ident at line 1 column 2";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn invalid_signature_algorithm() {
        // arrange
        let key = MemoryKey::new(base64::decode("primarykey").unwrap());
        let handler = SignHandler::new(key);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "ECDSA".to_string(),
            SignParameters::new(base64::encode("12345")),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected =
                    "Invalid signature algorithm\n\tcaused by: Signature algorithm \"ECDSA\" is unsupported.";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn bad_body_invalid_encoding() {
        // arrange
        let key = MemoryKey::new(base64::decode("primarykey").unwrap());
        let handler = SignHandler::new(key);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "HMAC-SHA256".to_string(),
            SignParameters::new("not_base64_encoded".to_string()),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        // assert
        assert_eq!(StatusCode::BAD_REQUEST, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let error_response: ErrorResponse = serde_json::from_slice(&b).unwrap();
                let expected = "Request body is malformed\n\tcaused by: Invalid byte 95, offset 3.";
                assert_eq!(expected, error_response.message());
                Ok(())
            })
            .wait()
            .unwrap();
    }

    #[test]
    fn success() {
        define_encode_set! {
            pub IOTHUB_ENCODE_SET = [PATH_SEGMENT_ENCODE_SET] | { '=' }
        }

        // arrange
        let key = MemoryKey::new(base64::decode("primarykey").unwrap());
        let handler = SignHandler::new(key);

        let expiry = "1593707071";
        let audience = format!("{}/devices/{}", "hubname.azure-devices.net", "test-device");

        let resource_uri =
            percent_encode(audience.to_lowercase().as_bytes(), IOTHUB_ENCODE_SET).to_string();
        let sig_data = format!("{}\n{}", &resource_uri, expiry);

        let sign_request = SignRequest::new(
            "primary".to_string(),
            "HMAC-SHA256".to_string(),
            SignParameters::new(base64::encode(&sig_data)),
        );

        let body = serde_json::to_string(&sign_request).unwrap();

        let request = Request::post("http://localhost/sign")
            .body(body.into())
            .unwrap();

        // act
        let response = handler
            .handle(request, Parameters::default())
            .wait()
            .unwrap();

        // assert
        let expected = "viM8puBDserBSVa0vIrXVP5QGj2x1x7an9WeQytsYwE=";
        assert_eq!(StatusCode::OK, response.status());
        response
            .into_body()
            .concat2()
            .and_then(|b| {
                let sign_response: SignResponse = serde_json::from_slice(&b).unwrap();
                assert_eq!(expected, sign_response.signature());
                Ok(())
            })
            .wait()
            .unwrap();
    }
}
