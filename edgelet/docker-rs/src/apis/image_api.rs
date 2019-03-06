/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use std::borrow::Borrow;
use std::sync::Arc;

use futures;
use futures::{Future, Stream};
use hyper;
use serde_json;
use typed_headers::{self, http, mime, HeaderMapExt};

use super::{configuration, Error};

use crate::models::ImageDeleteResponseItem;

pub struct ImageApiClient<C: hyper::client::connect::Connect> {
    configuration: Arc<configuration::Configuration<C>>,
}

impl<C: hyper::client::connect::Connect> ImageApiClient<C> {
    pub fn new(configuration: Arc<configuration::Configuration<C>>) -> Self {
        ImageApiClient {
            configuration: configuration,
        }
    }
}

pub trait ImageApi: Send + Sync {
    fn build_prune(
        &self,
    ) -> Box<dyn Future<Item = crate::models::InlineResponse2006, Error = Error<serde_json::Value>>>;
    fn image_build(
        &self,
        input_stream: Vec<u8>,
        dockerfile: &str,
        t: &str,
        extrahosts: &str,
        remote: &str,
        q: bool,
        nocache: bool,
        cachefrom: &str,
        pull: &str,
        rm: bool,
        forcerm: bool,
        memory: i32,
        memswap: i32,
        cpushares: i32,
        cpusetcpus: &str,
        cpuperiod: i32,
        cpuquota: i32,
        buildargs: i32,
        shmsize: i32,
        squash: bool,
        labels: &str,
        networkmode: &str,
        content_type: &str,
        x_registry_config: &str,
        platform: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>>;
    fn image_commit(
        &self,
        container_config: crate::models::ContainerConfig,
        container: &str,
        repo: &str,
        tag: &str,
        comment: &str,
        author: &str,
        pause: bool,
        changes: &str,
    ) -> Box<dyn Future<Item = crate::models::IdResponse, Error = Error<serde_json::Value>>>;
    fn image_create(
        &self,
        from_image: &str,
        from_src: &str,
        repo: &str,
        tag: &str,
        input_image: &str,
        x_registry_auth: &str,
        platform: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn image_delete(
        &self,
        name: &str,
        force: bool,
        noprune: bool,
    ) -> Box<dyn Future<Item = Vec<ImageDeleteResponseItem>, Error = Error<serde_json::Value>>>;
    fn image_get(
        &self,
        name: &str,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = Error<serde_json::Value>>>;
    fn image_get_all(
        &self,
        names: Vec<String>,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = Error<serde_json::Value>>>;
    fn image_history(
        &self,
        name: &str,
    ) -> Box<
        dyn Future<Item = Vec<crate::models::InlineResponse2007>, Error = Error<serde_json::Value>>,
    >;
    fn image_inspect(
        &self,
        name: &str,
    ) -> Box<dyn Future<Item = crate::models::Image, Error = Error<serde_json::Value>>>;
    fn image_list(
        &self,
        all: bool,
        filters: &str,
        digests: bool,
    ) -> Box<dyn Future<Item = Vec<crate::models::ImageSummary>, Error = Error<serde_json::Value>>>;
    fn image_load(
        &self,
        images_tarball: Vec<u8>,
        quiet: bool,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>>;
    fn image_prune(
        &self,
        filters: &str,
    ) -> Box<dyn Future<Item = crate::models::InlineResponse2009, Error = Error<serde_json::Value>>>;
    fn image_push(
        &self,
        name: &str,
        x_registry_auth: &str,
        tag: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>>;
    fn image_search(
        &self,
        term: &str,
        limit: i32,
        filters: &str,
    ) -> Box<
        dyn Future<Item = Vec<crate::models::InlineResponse2008>, Error = Error<serde_json::Value>>,
    >;
    fn image_tag(
        &self,
        name: &str,
        repo: &str,
        tag: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>>;
}

impl<C> ImageApi for ImageApiClient<C>
where
    C: hyper::client::connect::Connect + 'static,
    <C as hyper::client::connect::Connect>::Transport: 'static,
    <C as hyper::client::connect::Connect>::Future: 'static,
{
    fn build_prune(
        &self,
    ) -> Box<dyn Future<Item = crate::models::InlineResponse2006, Error = Error<serde_json::Value>>>
    {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let uri_str = format!("/build/prune");

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<crate::models::InlineResponse2006, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_build(
        &self,
        input_stream: Vec<u8>,
        dockerfile: &str,
        t: &str,
        extrahosts: &str,
        remote: &str,
        q: bool,
        nocache: bool,
        cachefrom: &str,
        pull: &str,
        rm: bool,
        forcerm: bool,
        memory: i32,
        memswap: i32,
        cpushares: i32,
        cpusetcpus: &str,
        cpuperiod: i32,
        cpuquota: i32,
        buildargs: i32,
        shmsize: i32,
        squash: bool,
        labels: &str,
        networkmode: &str,
        content_type: &str,
        x_registry_config: &str,
        platform: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("dockerfile", &dockerfile.to_string())
            .append_pair("t", &t.to_string())
            .append_pair("extrahosts", &extrahosts.to_string())
            .append_pair("remote", &remote.to_string())
            .append_pair("q", &q.to_string())
            .append_pair("nocache", &nocache.to_string())
            .append_pair("cachefrom", &cachefrom.to_string())
            .append_pair("pull", &pull.to_string())
            .append_pair("rm", &rm.to_string())
            .append_pair("forcerm", &forcerm.to_string())
            .append_pair("memory", &memory.to_string())
            .append_pair("memswap", &memswap.to_string())
            .append_pair("cpushares", &cpushares.to_string())
            .append_pair("cpusetcpus", &cpusetcpus.to_string())
            .append_pair("cpuperiod", &cpuperiod.to_string())
            .append_pair("cpuquota", &cpuquota.to_string())
            .append_pair("buildargs", &buildargs.to_string())
            .append_pair("shmsize", &shmsize.to_string())
            .append_pair("squash", &squash.to_string())
            .append_pair("labels", &labels.to_string())
            .append_pair("networkmode", &networkmode.to_string())
            .append_pair("platform", &platform.to_string())
            .finish();
        let uri_str = format!("/build?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let serialized = serde_json::to_string(&input_stream).unwrap();
        let serialized_len = serialized.len();

        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let mut req = req
            .header(http::header::CONTENT_TYPE, content_type)
            .header("X-Registry-Config", x_registry_config)
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");
        req.headers_mut()
            .typed_insert(&typed_headers::ContentType(mime::APPLICATION_JSON));
        req.headers_mut()
            .typed_insert(&typed_headers::ContentLength(serialized_len as u64));

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|_| futures::future::ok(())),
        )
    }

    fn image_commit(
        &self,
        container_config: crate::models::ContainerConfig,
        container: &str,
        repo: &str,
        tag: &str,
        comment: &str,
        author: &str,
        pause: bool,
        changes: &str,
    ) -> Box<dyn Future<Item = crate::models::IdResponse, Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("container", &container.to_string())
            .append_pair("repo", &repo.to_string())
            .append_pair("tag", &tag.to_string())
            .append_pair("comment", &comment.to_string())
            .append_pair("author", &author.to_string())
            .append_pair("pause", &pause.to_string())
            .append_pair("changes", &changes.to_string())
            .finish();
        let uri_str = format!("/commit?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let serialized = serde_json::to_string(&container_config).unwrap();
        let serialized_len = serialized.len();

        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let mut req = req
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");
        req.headers_mut()
            .typed_insert(&typed_headers::ContentType(mime::APPLICATION_JSON));
        req.headers_mut()
            .typed_insert(&typed_headers::ContentLength(serialized_len as u64));

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<crate::models::IdResponse, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_create(
        &self,
        from_image: &str,
        from_src: &str,
        repo: &str,
        tag: &str,
        input_image: &str,
        x_registry_auth: &str,
        platform: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("fromImage", &from_image.to_string())
            .append_pair("fromSrc", &from_src.to_string())
            .append_pair("repo", &repo.to_string())
            .append_pair("tag", &tag.to_string())
            .append_pair("platform", &platform.to_string())
            .finish();
        let uri_str = format!("/images/create?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let serialized = serde_json::to_string(&input_image).unwrap();
        let serialized_len = serialized.len();

        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let mut req = req
            .header("X-Registry-Auth", x_registry_auth)
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");
        req.headers_mut()
            .typed_insert(&typed_headers::ContentType(mime::APPLICATION_JSON));
        req.headers_mut()
            .typed_insert(&typed_headers::ContentLength(serialized_len as u64));

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| -> Result<(), Error<serde_json::Value>> {
                    if !status.is_success() {
                        return Err(Error::from((status, &*body)));
                    }

                    // Response body is a sequence of JSON objects.
                    // Each object is either a `{ "status": ... }` or an `{ "errorDetail": ... }`
                    //
                    // The overall success or failure of the operation is determined by which one
                    // the last object is.

                    let mut deserializer = serde_json::Deserializer::from_slice(&body).into_iter();
                    let mut last_response: serde_json::Map<String, serde_json::Value> =
                        deserializer.last().ok_or_else(|| {
                            Error::Serde(serde::de::Error::custom(
                                "empty response from container runtime",
                            ))
                        })??;
                    if let Some(error_detail) = last_response.remove("errorDetail") {
                        Err(Error::from((status, error_detail)))
                    } else {
                        Ok(())
                    }
                }),
        )
    }

    fn image_delete(
        &self,
        name: &str,
        force: bool,
        noprune: bool,
    ) -> Box<dyn Future<Item = Vec<ImageDeleteResponseItem>, Error = Error<serde_json::Value>>>
    {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::DELETE;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("force", &force.to_string())
            .append_pair("noprune", &noprune.to_string())
            .finish();
        let uri_str = format!("/images/{name}?{}", query, name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<crate::models::ImageDeleteResponseItem>, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_get(
        &self,
        name: &str,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let uri_str = format!("/images/{name}/get", name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<u8>, _> = serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_get_all(
        &self,
        names: Vec<String>,
    ) -> Box<dyn Future<Item = Vec<u8>, Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("names", &names.join(",").to_string())
            .finish();
        let uri_str = format!("/images/get?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<u8>, _> = serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_history(
        &self,
        name: &str,
    ) -> Box<
        dyn Future<Item = Vec<crate::models::InlineResponse2007>, Error = Error<serde_json::Value>>,
    > {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let uri_str = format!("/images/{name}/history", name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<crate::models::InlineResponse2007>, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_inspect(
        &self,
        name: &str,
    ) -> Box<dyn Future<Item = crate::models::Image, Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let uri_str = format!("/images/{name}/json", name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<crate::models::Image, _> = serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_list(
        &self,
        all: bool,
        filters: &str,
        digests: bool,
    ) -> Box<dyn Future<Item = Vec<crate::models::ImageSummary>, Error = Error<serde_json::Value>>>
    {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("all", &all.to_string())
            .append_pair("filters", &filters.to_string())
            .append_pair("digests", &digests.to_string())
            .finish();
        let uri_str = format!("/images/json?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<crate::models::ImageSummary>, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_load(
        &self,
        images_tarball: Vec<u8>,
        quiet: bool,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("quiet", &quiet.to_string())
            .finish();
        let uri_str = format!("/images/load?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let serialized = serde_json::to_string(&images_tarball).unwrap();
        let serialized_len = serialized.len();

        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let mut req = req
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");
        req.headers_mut()
            .typed_insert(&typed_headers::ContentType(mime::APPLICATION_JSON));
        req.headers_mut()
            .typed_insert(&typed_headers::ContentLength(serialized_len as u64));

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|_| futures::future::ok(())),
        )
    }

    fn image_prune(
        &self,
        filters: &str,
    ) -> Box<dyn Future<Item = crate::models::InlineResponse2009, Error = Error<serde_json::Value>>>
    {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/images/prune?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<crate::models::InlineResponse2009, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_push(
        &self,
        name: &str,
        x_registry_auth: &str,
        tag: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("tag", &tag.to_string())
            .finish();
        let uri_str = format!("/images/{name}/push?{}", query, name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .header("X-Registry-Auth", x_registry_auth)
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|_| futures::future::ok(())),
        )
    }

    fn image_search(
        &self,
        term: &str,
        limit: i32,
        filters: &str,
    ) -> Box<
        dyn Future<Item = Vec<crate::models::InlineResponse2008>, Error = Error<serde_json::Value>>,
    > {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("term", &term.to_string())
            .append_pair("limit", &limit.to_string())
            .append_pair("filters", &filters.to_string())
            .finish();
        let uri_str = format!("/images/search?{}", query);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|body| {
                    let parsed: Result<Vec<crate::models::InlineResponse2008>, _> =
                        serde_json::from_slice(&body);
                    parsed.map_err(|e| Error::from(e))
                }),
        )
    }

    fn image_tag(
        &self,
        name: &str,
        repo: &str,
        tag: &str,
    ) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>>> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("repo", &repo.to_string())
            .append_pair("tag", &tag.to_string())
            .finish();
        let uri_str = format!("/images/{name}/tag?{}", query, name = name);

        let uri = (configuration.uri_composer)(&configuration.base_path, &uri_str);
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut req = hyper::Request::builder();
        req.method(method).uri(uri.unwrap());
        if let Some(ref user_agent) = configuration.user_agent {
            req.header(http::header::USER_AGENT, &**user_agent);
        }
        let req = req
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
            configuration
                .client
                .request(req)
                .map_err(|e| Error::from(e))
                .and_then(|resp| {
                    let (http::response::Parts { status, .. }, body) = resp.into_parts();
                    body.concat2()
                        .and_then(move |body| Ok((status, body)))
                        .map_err(|e| Error::from(e))
                })
                .and_then(|(status, body)| {
                    if status.is_success() {
                        Ok(body)
                    } else {
                        Err(Error::from((status, &*body)))
                    }
                })
                .and_then(|_| futures::future::ok(())),
        )
    }
}
