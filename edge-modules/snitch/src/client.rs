// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::collections::HashMap;
use std::rc::Rc;

use futures::future::{self, Either};
use futures::{Future, Stream};
use http::Uri;
use hyper::header::{HeaderValue, CONTENT_LENGTH, CONTENT_TYPE, IF_MATCH};
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Method, Request};
use serde::{de::DeserializeOwned, Serialize};
use serde_json;
use url::{form_urlencoded::Serializer as UrlSerializer, Url};

use error::Error;

pub struct Client<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError>,
{
    service: Rc<RefCell<S>>,
    host_name: Url,
}

impl<S> Client<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError>,
{
    pub fn new(service: S, host_name: Url) -> Client<S> {
        Client {
            service: Rc::new(RefCell::new(service)),
            host_name,
        }
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
        let query = query
            .unwrap_or_else(HashMap::new)
            .iter()
            .fold(&mut UrlSerializer::new(String::new()), |ser, (key, val)| {
                ser.append_pair(key, val)
            })
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
                let mut builder = Request::builder();
                let req = builder
                    .method(method)
                    .uri(url.as_str().parse::<Uri>().expect("Unexpected Url to Uri conversion failure"));

                // add an `If-Match: "*"` header if we've been asked to
                if add_if_match {
                    req.header(IF_MATCH, HeaderValue::from_static("Any"));
                }

                // add request body if there is any
                if let Some(body) = body {
                    let serialized = serde_json::to_string(&body)?;
                    req.header(CONTENT_TYPE, "text/json");
                    req.header(CONTENT_LENGTH, format!("{}", serialized.len()).as_str());

                    Ok(req.body(Body::from(serialized))?)
                } else {
                    Ok(req.body(Body::empty())?)
                }
            })
            .map(move |req| {
                let res = self.service.borrow_mut()
                    .call(req)
                    .map_err(Error::from)
                    .and_then(|resp| {
                        let status = resp.status();
                        let (_, body) = resp.into_parts();
                        body
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

impl<S> Clone for Client<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError>,
{
    fn clone(&self) -> Self {
        Client {
            service: self.service.clone(),
            host_name: self.host_name.clone(),
        }
    }
}
