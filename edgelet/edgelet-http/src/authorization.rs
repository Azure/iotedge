// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use failure::ResultExt;
use futures::{future, Future};
use hyper::{Body, Request, Response};

use edgelet_core::{AuthId, Authorization as CoreAuth, Policy};

use crate::error::{Error, ErrorKind};
use crate::route::{Handler, Parameters};
use crate::IntoResponse;

pub struct Authorization<H> {
    auth: CoreAuth,
    inner: Arc<H>,
}

impl<H> Authorization<H>
where
    H: Handler<Parameters>,
{
    pub fn new(inner: H, policy: Policy) -> Self {
        Authorization {
            auth: CoreAuth::new(policy),
            inner: Arc::new(inner),
        }
    }
}

impl<H> Handler<Parameters> for Authorization<H>
where
    H: Handler<Parameters> + Sync,
{
    fn handle(
        &self,
        req: Request<Body>,
        params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = Error> + Send> {
        let (name, auth_id) = (
            params.name("name").map(ToString::to_string),
            req.extensions()
                .get::<AuthId>()
                .cloned()
                .unwrap_or_else(|| AuthId::None),
        );
        let inner = self.inner.clone();

        let response =
            self.auth
                .authorize(name.clone(), auth_id)
                .then(|authorized| {
                    authorized
                        .context(ErrorKind::Authorization)
                        .map_err(Error::from)
                })
                .and_then(move |authorized| {
                    if authorized {
                        future::Either::A(inner.handle(req, params).then(|resp| {
                            resp.context(ErrorKind::Authorization).map_err(Error::from)
                        }))
                    } else {
                        future::Either::B(future::err(Error::from(ErrorKind::ModuleNotFound(
                            name.unwrap_or_else(String::new),
                        ))))
                    }
                })
                .or_else(|e| future::ok(e.into_response()));

        Box::new(response)
    }
}

#[cfg(test)]
mod tests {
    use futures::{Future, Stream};
    use hyper::{Body, Request, Response, StatusCode};

    use super::*;
    use crate::error::Error as HttpError;

    #[test]
    fn handler_calls_inner_handler() {
        let mut request = Request::default();
        request
            .extensions_mut()
            .insert(AuthId::Value("abc".to_string()));
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller);
        let response = auth.handle(request, params).wait().unwrap();
        let body = response
            .into_body()
            .concat2()
            .and_then(|body| Ok(String::from_utf8(body.to_vec()).unwrap()))
            .wait()
            .unwrap();

        assert_eq!("from TestHandler", body);
    }

    #[test]
    fn handler_responds_with_not_found_when_not_authorized() {
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "xyz".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(AuthId::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_name_is_omitted() {
        let params = Parameters::with_captures(vec![]);
        let mut request = Request::default();
        request
            .extensions_mut()
            .insert(AuthId::Value("abc".to_string()));

        let auth = Authorization::new(TestHandler::new(), Policy::Caller);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[test]
    fn handler_responds_with_not_found_when_auth_id_is_omitted() {
        let params = Parameters::with_captures(vec![(Some("name".to_string()), "abc".to_string())]);
        let mut request = Request::default();
        request.extensions_mut().insert(AuthId::None);

        let auth = Authorization::new(TestHandler::new(), Policy::Caller);
        let response = auth.handle(request, params).wait().unwrap();
        assert_eq!(404, response.status());
    }

    #[derive(Clone)]
    struct TestHandler {}

    impl TestHandler {
        pub fn new() -> Self {
            TestHandler {}
        }
    }

    impl Handler<Parameters> for TestHandler {
        fn handle(
            &self,
            _req: Request<Body>,
            _params: Parameters,
        ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
            let response = Response::builder()
                .status(StatusCode::OK)
                .body("from TestHandler".into())
                .unwrap();
            Box::new(future::ok(response))
        }
    }
}
