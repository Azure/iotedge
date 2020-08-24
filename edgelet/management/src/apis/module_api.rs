/* 
 * IoT Edge Management API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2020-07-22
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use std::borrow::Borrow;
use std::sync::Arc;

use hyper;
use serde_json;
use futures;
use futures::{Future, Stream};

use super::{Error, configuration};

pub struct ModuleApiClient<C: hyper::client::connect::Connect> {
    configuration: Arc<configuration::Configuration<C>>,
}

impl<C: hyper::client::connect::Connect> ModuleApiClient<C> {
    pub fn new(configuration: Arc<configuration::Configuration<C>>) -> ModuleApiClient<C> {
        ModuleApiClient {
            configuration: configuration,
        }
    }
}

pub trait ModuleApi: Send + Sync {
    fn create_module(&self, api_version: &str, module: ::models::ModuleSpec) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send>;
    fn delete_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn get_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send>;
    fn list_modules(&self, api_version: &str) -> Box<dyn Future<Item = ::models::ModuleList, Error = Error<serde_json::Value>> + Send>;
    fn module_logs(&self, api_version: &str, name: &str, follow: bool, tail: &str, since: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn prepare_update_module(&self, api_version: &str, name: &str, module: ::models::ModuleSpec) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn restart_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn start_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn stop_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send>;
    fn update_module(&self, api_version: &str, name: &str, module: ::models::ModuleSpec, start: bool) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send>;
}


impl<C: hyper::client::connect::Connect + 'static> ModuleApi for ModuleApiClient<C> {
    fn create_module(&self, api_version: &str, module: ::models::ModuleSpec) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules{}", configuration.base_path, query);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let serialized = serde_json::to_string(&module).unwrap();
        builder.header(hyper::header::CONTENT_TYPE, hyper::header::HeaderValue::from_str("application/json").unwrap());
        builder.header(hyper::header::CONTENT_LENGTH, serialized.len());
        let req = builder
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
                let parsed: Result<::models::ModuleDetails, _> = serde_json::from_slice(&body);
                parsed.map_err(|e| Error::from(e))
            })
        )
    }

    fn delete_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::DELETE;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn get_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
                let parsed: Result<::models::ModuleDetails, _> = serde_json::from_slice(&body);
                parsed.map_err(|e| Error::from(e))
            })
        )
    }

    fn list_modules(&self, api_version: &str) -> Box<dyn Future<Item = ::models::ModuleList, Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules{}", configuration.base_path, query);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
                let parsed: Result<::models::ModuleList, _> = serde_json::from_slice(&body);
                parsed.map_err(|e| Error::from(e))
            })
        )
    }

    fn module_logs(&self, api_version: &str, name: &str, follow: bool, tail: &str, since: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::GET;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .append_pair("follow", &follow.to_string())
            .append_pair("tail", &tail.to_string())
            .append_pair("since", &since.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}/logs{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn prepare_update_module(&self, api_version: &str, name: &str, module: ::models::ModuleSpec) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}/prepareupdate{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let serialized = serde_json::to_string(&module).unwrap();
        builder.header(hyper::header::CONTENT_TYPE, hyper::header::HeaderValue::from_str("application/json").unwrap());
        builder.header(hyper::header::CONTENT_LENGTH, serialized.len());
        let req = builder
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn restart_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}/restart{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn start_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}/start{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn stop_module(&self, api_version: &str, name: &str) -> Box<dyn Future<Item = (), Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::POST;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}/stop{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let req = builder
            .body(hyper::Body::empty())
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
            .and_then(|_| futures::future::ok(()))
        )
    }

    fn update_module(&self, api_version: &str, name: &str, module: ::models::ModuleSpec, start: bool) -> Box<dyn Future<Item = ::models::ModuleDetails, Error = Error<serde_json::Value>> + Send> {
        let configuration: &configuration::Configuration<C> = self.configuration.borrow();

        let method = hyper::Method::PUT;

        let query = ::url::form_urlencoded::Serializer::new(String::new())
            .append_pair("api-version", &api_version.to_string())
            .append_pair("start", &start.to_string())
            .finish();
        let uri_str = format!("{}/modules/{name}{}", configuration.base_path, query, name=name);

        let uri: hyper::Uri = uri_str.parse().unwrap();
        // TODO(farcaller): handle error
        // if let Err(e) = uri {
        //     return Box::new(futures::future::err(e));
        // }
        let mut builder = hyper::Request::builder();

        builder.method(method);
        builder.uri(uri);

        if let Some(ref user_agent) = configuration.user_agent {
            builder.header(hyper::header::USER_AGENT, hyper::header::HeaderValue::from_str(user_agent).unwrap());
        }


        let serialized = serde_json::to_string(&module).unwrap();
        builder.header(hyper::header::CONTENT_TYPE, hyper::header::HeaderValue::from_str("application/json").unwrap());
        builder.header(hyper::header::CONTENT_LENGTH, serialized.len());
        let req = builder
            .body(hyper::Body::from(serialized))
            .expect("could not build hyper::Request");

        // send request
        Box::new(
        configuration.client.request(req)
            .map_err(|e| Error::from(e))
            .and_then(|resp| {
                let status = resp.status();
                resp.into_body().concat2()
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
                let parsed: Result<::models::ModuleDetails, _> = serde_json::from_slice(&body);
                parsed.map_err(|e| Error::from(e))
            })
        )
    }

}
