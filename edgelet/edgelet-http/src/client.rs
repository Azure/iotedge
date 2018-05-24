// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::rc::Rc;

use chrono::{DateTime, Duration, Utc};
use futures::future::{self, Either};
use futures::{Future, Stream};
use hyper::client::Service;
use hyper::header::{Authorization, ContentLength, ContentType, IfMatch, UserAgent};
use hyper::{Error as HyperError, Method, Request, Response, Uri};
use serde::{Serialize, de::DeserializeOwned};
use serde_json;
use url::{Url, form_urlencoded::Serializer as UrlSerializer};

use error::Error;

pub trait TokenSource {
    type Error;
    fn get(&self, expiry: &DateTime<Utc>) -> Result<String, Self::Error>;
}

pub struct Client<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: TokenSource + Clone,
{
    service: Rc<S>,
    token_source: Option<T>,
    api_version: String,
    host_name: Url,
    user_agent: Option<String>,
}

impl<S, T> Client<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: TokenSource + Clone,
    T::Error: Into<Error>,
{
    pub fn new(
        service: S,
        token_source: Option<T>,
        api_version: &str,
        host_name: Url,
    ) -> Result<Client<S, T>, Error> {
        let client = Client {
            service: Rc::new(service),
            token_source,
            api_version: ensure_not_empty!(api_version).to_string(),
            host_name,
            user_agent: None,
        };
        Ok(client)
    }

    pub fn with_token_source(mut self, source: T) -> Client<S, T> {
        self.token_source = Some(source);
        self
    }

    pub fn with_user_agent(mut self, user_agent: &str) -> Client<S, T> {
        self.user_agent = Some(user_agent.to_string());
        self
    }

    pub fn client(&self) -> &S {
        self.service.as_ref()
    }

    pub fn api_version(&self) -> &str {
        &self.api_version
    }

    pub fn user_agent(&self) -> Option<&String> {
        self.user_agent.as_ref()
    }

    pub fn host_name(&self) -> &Url {
        &self.host_name
    }

    fn add_sas_token(&self, req: &mut Request, path: &str) -> Result<(), Error> {
        if let Some(ref source) = self.token_source {
            let token_duration = Duration::hours(1);
            let expiry = Utc::now() + token_duration;
            let token = source.get(&expiry).map_err(|err| err.into())?;
            debug!(
                "Success generating token for request {} {}",
                req.method(),
                path,
            );
            req.headers_mut()
                .set(Authorization(format!("SharedAccessSignature {}", token)));
        } else {
            debug!("Empty token source for request {} {}", req.method(), path);
        }

        Ok(())
    }

    pub fn request<BodyT, ResponseT>(
        &self,
        method: Method,
        path: &str,
        query: Option<HashMap<&str, &str>>,
        body: Option<BodyT>,
        add_if_match: bool,
    ) -> impl Future<Item = Option<ResponseT>, Error = Error>
    where
        BodyT: Serialize,
        ResponseT: 'static + DeserializeOwned,
    {
        // append api-version to the query string and url encode it
        let query = query
            .unwrap_or_else(HashMap::new)
            .iter()
            .fold(
                UrlSerializer::new(String::new()).append_pair("api-version", &self.api_version),
                |ser, (key, val)| ser.append_pair(key, val),
            )
            .finish();

        self.host_name
            // build the full url
            .join(&format!("{}?{}", path, query))
            .map_err(Error::from)
            .and_then(|url| {
                // NOTE: 'expect' here should be OK, because this is a type
                // conversion from url::Url to hyper::Uri and not really a URL
                // parse operation. At this point the URL has already been parsed
                // and is known to be good.
                let mut req = Request::new(method.clone(),
                    url.as_str().parse::<Uri>().expect("Unexpected Url to Uri conversion failure")
                );

                // add user agent header
                if let Some(ref user_agent) = self.user_agent {
                    req.headers_mut().set(UserAgent::new(user_agent.clone()));
                }

                // add sas token
                self.add_sas_token(&mut req, path)?;

                // add an `If-Match: "*"` header if we've been asked to
                if add_if_match {
                    req.headers_mut().set(IfMatch::Any);
                }

                // add request body if there is any
                if let Some(body) = body {
                    let serialized = serde_json::to_string(&body)?;
                    req.headers_mut().set(ContentType::json());
                    req.headers_mut().set(ContentLength(serialized.len() as u64));

                    req.set_body(serialized);
                }

                Ok(req)
            })
            .map(|req| {
                let res = self.service
                    .call(req)
                    .map_err(|e| { error!("{:?}", e); Error::from(e) })
                    .and_then(|resp| {
                        let status = resp.status();
                        resp.body()
                            .concat2()
                            .and_then(move |body| Ok((status, body)))
                            .map_err(Error::from)
                    })
                    .and_then(|(status, body)| {
                        if status.is_success() {
                            Ok(body)
                        } else {
                            Err(Error::from((status, &*body)))
                        }
                    })
                    .and_then(|body| {
                        if body.len() == 0 {
                            Ok(None)
                        } else {
                            serde_json::from_slice::<ResponseT>(&body)
                                .map_err(Error::from)
                                .map(Option::Some)
                        }
                    });

                Either::A(res)
            })
            .unwrap_or_else(|e| Either::B(future::err(e)))
    }
}

impl<S, T> Clone for Client<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: TokenSource + Clone,
{
    fn clone(&self) -> Self {
        Client {
            service: self.service.clone(),
            token_source: self.token_source.as_ref().cloned(),
            api_version: self.api_version.clone(),
            host_name: self.host_name.clone(),
            user_agent: self.user_agent.as_ref().cloned(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;
    use std::mem;
    use std::str;

    use chrono::{DateTime, Utc};
    use futures::future;
    use hyper::client::Client as HyperClient;
    use hyper::header::{Authorization, ContentType, UserAgent};
    use hyper::server::service_fn;
    use hyper::{Request, Response, StatusCode};
    use tokio_core::reactor::Core;
    use url::form_urlencoded::parse as parse_query;

    use error::ErrorKind;

    struct StaticTokenSource {
        token: String,
    }

    impl StaticTokenSource {
        pub fn new(token: String) -> Self {
            StaticTokenSource { token }
        }
    }

    impl TokenSource for StaticTokenSource {
        type Error = Error;
        fn get(&self, _expiry: &DateTime<Utc>) -> Result<String, Error> {
            Ok(self.token.clone())
        }
    }

    impl Clone for StaticTokenSource {
        fn clone(&self) -> Self {
            StaticTokenSource {
                token: self.token.clone(),
            }
        }
    }

    #[test]
    fn empty_api_version_fails() {
        let core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let token_source: Option<StaticTokenSource> = None;
        match Client::new(
            hyper_client,
            token_source,
            "",
            Url::parse("http://localhost").unwrap(),
        ) {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Utils) {
                    panic!("Wrong error kind. Expected `ArgumentEmpty` found {:?}", err);
                }
            }
        };
    }

    #[test]
    fn white_space_api_version_fails() {
        let core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let token_source: Option<StaticTokenSource> = None;
        match Client::new(
            hyper_client,
            token_source,
            "      ",
            Url::parse("http://localhost").unwrap(),
        ) {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Utils) {
                    panic!("Wrong error kind. Expected `ArgumentEmpty` found {:?}", err);
                }
            }
        };
    }

    #[test]
    fn request_adds_api_version() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(req.path(), "/boo");
            assert_eq!(None, req.headers().get::<IfMatch>());

            // check that the query has api version_
            let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
                .into_owned()
                .collect();
            assert_eq!(query_map.get("api-version"), Some(&api_version.to_string()));

            Box::new(future::ok(
                Response::new()
                    .with_status(StatusCode::Ok)
                    .with_body(response.clone().into_bytes()),
            ))
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::Get, "/boo", None, None, false);
        let _result: Option<String> = core.run(task).unwrap();
    }

    #[test]
    fn request_adds_api_version_with_other_query_params() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(req.path(), "/boo");
            assert_eq!(None, req.headers().get::<IfMatch>());

            // check that the query has api version
            let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
                .into_owned()
                .collect();
            assert_eq!(query_map.get("api-version"), Some(&api_version.to_string()));
            assert_eq!(query_map.get("k1"), Some(&"v1".to_string()));
            assert_eq!(
                query_map.get("k2"),
                Some(&"this value has spaces and 🐮🐮🐮".to_string())
            );

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_body(response.clone().into_bytes()))
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let mut query = HashMap::new();
        query.insert("k1", "v1");
        query.insert("k2", "this value has spaces and 🐮🐮🐮");

        let task = client.request::<String, String>(Method::Get, "/boo", Some(query), None, false);
        let _result: String = core.run(task).unwrap().unwrap();
    }

    #[test]
    fn request_adds_user_agent() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let user_agent = "edgelet/request/test";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(
                user_agent,
                &req.headers().get::<UserAgent>().unwrap().to_string()
            );
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_body(response.clone().into_bytes()))
        };
        let client = Client::new(service_fn(handler), token_source, api_version, host_name)
            .unwrap()
            .with_user_agent(user_agent);

        let task = client.request::<String, String>(Method::Get, "/boo", None, None, false);
        let _result: String = core.run(task).unwrap().unwrap();
    }

    #[test]
    fn request_adds_sas_token() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let sas_token = "super_secret_password_y'all";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();

        let handler = move |req: Request| {
            let sas_header = &req.headers()
                .get::<Authorization<String>>()
                .unwrap()
                .to_string();
            let expected_sas = format!("SharedAccessSignature {}", sas_token);
            assert_eq!(&expected_sas, sas_header);
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_body(response.clone().into_bytes()))
        };
        let token_source: Option<StaticTokenSource> =
            Some(StaticTokenSource::new(sas_token.to_string()));
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::Get, "/boo", None, None, false);
        let _result: String = core.run(task).unwrap().unwrap();
    }

    #[test]
    fn request_adds_if_match_header() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(Some(&IfMatch::Any), req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_header(ContentType::json())
                .with_body(response.clone().into_bytes()))
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, _>(Method::Get, "/boo", None, None, true);
        let _result: String = core.run(task).unwrap().unwrap();
    }

    #[test]
    fn request_adds_body() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(None, req.headers().get::<IfMatch>());

            req.body()
                .concat2()
                .and_then(|req_body| {
                    str::from_utf8(&req_body)
                        .map(move |req_body| {
                            assert_eq!("\"Here be dragons\"".to_string(), req_body)
                        })
                        .map_err(|e| panic!("Error: {:?}", e))
                })
                .and_then(|_| {
                    let response = r#""response""#.to_string();
                    Ok(Response::new()
                        .with_status(StatusCode::Ok)
                        .with_body(response.clone().into_bytes()))
                })
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(
            Method::Post,
            "/boo",
            None,
            Some("Here be dragons".to_string()),
            false,
        );
        let _result: String = core.run(task).unwrap().unwrap();
    }

    #[test]
    fn request_can_return_empty_response() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(None, req.headers().get::<IfMatch>());

            req.body()
                .concat2()
                .and_then(|req_body| {
                    str::from_utf8(&req_body)
                        .map(move |req_body| {
                            assert_eq!("\"Here be dragons\"".to_string(), req_body)
                        })
                        .map_err(|e| panic!("Error: {:?}", e))
                })
                .and_then(|_| Ok(Response::new().with_status(StatusCode::Ok)))
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, _>(
            Method::Post,
            "/boo",
            None,
            Some("Here be dragons".to_string()),
            false,
        );
        let result: Option<String> = core.run(task).unwrap();
        assert_eq!(result, None);
    }

    #[test]
    fn request_returns_response() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let response = r#""response""#.to_string();
        let token_source: Option<StaticTokenSource> = None;

        let handler = move |req: Request| {
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_body(response.clone().into_bytes()))
        };
        let client =
            Client::new(service_fn(handler), token_source, api_version, host_name).unwrap();

        let task = client.request::<String, String>(Method::Get, "/boo", None, None, false);
        let result: String = core.run(task).unwrap().unwrap();

        assert_eq!(result, "response");
    }
}
