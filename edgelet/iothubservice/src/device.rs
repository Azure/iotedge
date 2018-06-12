// Copyright (c) Microsoft. All rights reserved.

use futures::future::{self, Either};
use futures::Future;
use hyper::client::Service;
use hyper::{Error as HyperError, Method, Request, Response, StatusCode};

use edgelet_http::client::{Client, TokenSource};
use edgelet_http::error::{Error as HttpError, ErrorKind as HttpErrorKind};
use error::{Error, ErrorKind};
use model::{AuthMechanism, Module};

pub struct DeviceClient<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: TokenSource + Clone,
{
    client: Client<S, T>,
    device_id: String,
}

impl<S, T> DeviceClient<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: 'static + TokenSource + Clone,
    T::Error: Into<HttpError>,
{
    pub fn new(client: Client<S, T>, device_id: &str) -> Result<DeviceClient<S, T>, Error> {
        Ok(DeviceClient {
            client,
            device_id: ensure_not_empty!(device_id).to_string(),
        })
    }

    pub fn device_id(&self) -> &str {
        self.device_id.as_ref()
    }

    pub fn create_module(
        &self,
        module_id: &str,
        authentication: Option<AuthMechanism>,
        managed_by: Option<&String>,
    ) -> impl Future<Item = Module, Error = Error> {
        self.upsert_module(module_id, authentication, managed_by, false)
    }

    pub fn update_module(
        &self,
        module_id: &str,
        authentication: Option<AuthMechanism>,
        managed_by: Option<&String>,
    ) -> impl Future<Item = Module, Error = Error> {
        self.upsert_module(module_id, authentication, managed_by, true)
    }

    fn upsert_module(
        &self,
        module_id: &str,
        authentication: Option<AuthMechanism>,
        managed_by: Option<&String>,
        add_if_match: bool,
    ) -> impl Future<Item = Module, Error = Error> {
        if module_id.trim().is_empty() {
            Either::B(future::err(Error::from(ErrorKind::EmptyModuleId)))
        } else {
            let mut module = Module::default()
                .with_device_id(self.device_id.clone())
                .with_module_id(module_id.to_string());

            if let Some(authentication) = authentication {
                module = module.with_authentication(authentication);
            }

            if let Some(managed_by) = managed_by {
                module = module.with_managed_by(managed_by.to_string());
            }

            let res = self.client
                .request::<Module, Module>(
                    Method::Put,
                    &format!("/devices/{}/modules/{}", &self.device_id, module_id),
                    None,
                    Some(module),
                    add_if_match,
                )
                .map_err(Error::from)
                .and_then(|module| module.ok_or_else(|| Error::from(ErrorKind::ModuleNotFound)));

            Either::A(res)
        }
    }

    pub fn get_module_by_id(&self, module_id: &str) -> impl Future<Item = Module, Error = Error> {
        if module_id.trim().is_empty() {
            Either::B(future::err(Error::from(ErrorKind::EmptyModuleId)))
        } else {
            let res = self.client
                .request::<(), Module>(
                    Method::Get,
                    &format!("/devices/{}/modules/{}", &self.device_id, module_id),
                    None,
                    None,
                    false,
                )
                .map_err(|err| {
                    if let HttpErrorKind::ServiceError(code, _) = err.kind() {
                        if *code == StatusCode::NotFound {
                            return Error::from(ErrorKind::ModuleNotFound);
                        }
                    }

                    Error::from(err)
                })
                .and_then(|module| module.ok_or_else(|| Error::from(ErrorKind::ModuleNotFound)));

            Either::A(res)
        }
    }

    pub fn list_modules(&self) -> impl Future<Item = Vec<Module>, Error = Error> {
        self.client
            .request::<(), Vec<Module>>(
                Method::Get,
                &format!("/devices/{}/modules", &self.device_id),
                None,
                None,
                false,
            )
            .map_err(Error::from)
            .and_then(|modules| modules.ok_or_else(|| Error::from(ErrorKind::EmptyResponse)))
    }

    pub fn delete_module(&self, module_id: &str) -> impl Future<Item = (), Error = Error> {
        if module_id.trim().is_empty() {
            Either::B(future::err(Error::from(ErrorKind::EmptyModuleId)))
        } else {
            let res = self.client
                .request::<(), ()>(
                    Method::Delete,
                    &format!("/devices/{}/modules/{}", self.device_id, module_id),
                    None,
                    None,
                    true,
                )
                .map_err(Error::from)
                .and_then(|_| Ok(()));

            Either::A(res)
        }
    }
}

impl<S, T> Clone for DeviceClient<S, T>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    T: TokenSource + Clone,
{
    fn clone(&self) -> Self {
        DeviceClient {
            client: self.client.clone(),
            device_id: self.device_id.clone(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::mem;

    use chrono::{DateTime, Utc};
    use futures::Stream;
    use hyper::header::{ContentType, IfMatch};
    use hyper::server::service_fn;
    use hyper::{Client as HyperClient, Method};
    use serde_json;
    use tokio_core::reactor::Core;
    use url::Url;

    use error::ErrorKind;
    use model::{AuthType, SymmetricKey};

    struct NullTokenSource;

    impl TokenSource for NullTokenSource {
        type Error = Error;
        fn get(&self, _expiry: &DateTime<Utc>) -> Result<String, Error> {
            Ok("token".to_string())
        }
    }

    impl Clone for NullTokenSource {
        fn clone(&self) -> Self {
            NullTokenSource {}
        }
    }

    #[test]
    fn device_client_create_empty_id_fails() {
        let core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        match DeviceClient::new(client, "") {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Utils) {
                    panic!("Wrong error kind. Expected `ArgumentEmpty` found {:?}", err);
                }
            }
        };
    }

    #[test]
    fn device_client_create_white_space_id_fails() {
        let core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        match DeviceClient::new(client, "       ") {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::Utils) {
                    panic!("Wrong error kind. Expected `ArgumentEmpty` found {:?}", err);
                }
            }
        };
    }

    #[test]
    fn module_upsert_empty_module_id_fails() {
        let mut core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let task = device_client
            .upsert_module("", None, None, false)
            .then(|result| match result {
                Ok(_) => panic!("Expected error but got a result."),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::EmptyModuleId)
                    {
                        panic!("Wrong error kind. Expected `EmptyModuleId` found {:?}", err);
                    }

                    Ok(()) as Result<(), Error>
                }
            });

        core.run(task).unwrap();
    }

    #[test]
    fn module_upsert_white_space_module_id_fails() {
        let mut core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let task = device_client
            .upsert_module("     ", None, None, false)
            .then(|result| match result {
                Ok(_) => panic!("Expected error but got a result."),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::EmptyModuleId)
                    {
                        panic!("Wrong error kind. Expected `EmptyModuleId` found {:?}", err);
                    }

                    Ok(()) as Result<(), Error>
                }
            });

        core.run(task).unwrap();
    }

    #[test]
    fn module_upsert_adds_module_body_without_if_match() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let auth = AuthMechanism::default()
            .with_type(AuthType::Sas)
            .with_symmetric_key(
                SymmetricKey::default()
                    .with_primary_key("pkey".to_string())
                    .with_secondary_key("skey".to_string()),
            );
        let module_request = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_authentication(auth.clone())
            .with_managed_by("iotedge".to_string());
        let expected_response = module_request.clone().with_generation_id("g1".to_string());

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Put);
            assert_eq!(req.path(), "/devices/d1/modules/m1");
            assert_eq!(None, req.headers().get::<IfMatch>());

            let module_request_copy = module_request.clone();
            req.body()
                .concat2()
                .and_then(|req_body| Ok(serde_json::from_slice::<Module>(&req_body).unwrap()))
                .and_then(move |module| {
                    assert_eq!(module, module_request_copy);

                    Ok(Response::new()
                        .with_status(StatusCode::Ok)
                        .with_header(ContentType::json())
                        .with_body(
                            serde_json::to_string(&module.with_generation_id("g1".to_string()))
                                .unwrap()
                                .into_bytes(),
                        ))
                })
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client
            .upsert_module("m1", Some(auth), Some(&"iotedge".to_string()), false)
            .then(|result| Ok(assert_eq!(expected_response, result.unwrap())) as Result<(), Error>);

        core.run(task).unwrap();
    }

    #[test]
    fn module_upsert_adds_module_body_with_if_match() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let auth = AuthMechanism::default()
            .with_type(AuthType::Sas)
            .with_symmetric_key(
                SymmetricKey::default()
                    .with_primary_key("pkey".to_string())
                    .with_secondary_key("skey".to_string()),
            );
        let module_request = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_authentication(auth.clone())
            .with_managed_by("iotedge".to_string());
        let expected_response = module_request.clone().with_generation_id("g1".to_string());

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Put);
            assert_eq!(req.path(), "/devices/d1/modules/m1");
            assert_eq!(req.headers().get::<IfMatch>().unwrap(), &IfMatch::Any);

            let module_request_copy = module_request.clone();
            req.body()
                .concat2()
                .and_then(|req_body| Ok(serde_json::from_slice::<Module>(&req_body).unwrap()))
                .and_then(move |module| {
                    assert_eq!(module, module_request_copy);

                    Ok(Response::new()
                        .with_status(StatusCode::Ok)
                        .with_header(ContentType::json())
                        .with_body(
                            serde_json::to_string(&module.with_generation_id("g1".to_string()))
                                .unwrap()
                                .into_bytes(),
                        ))
                })
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client
            .upsert_module("m1", Some(auth), Some(&"iotedge".to_string()), true)
            .then(|result| Ok(assert_eq!(expected_response, result.unwrap())) as Result<(), Error>);

        core.run(task).unwrap();
    }

    #[test]
    fn module_delete_empty_module_id_fails() {
        let mut core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let task = device_client.delete_module("").then(|result| match result {
            Ok(_) => panic!("Expected error but got a result."),
            Err(err) => {
                if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::EmptyModuleId) {
                    panic!("Wrong error kind. Expected `EmptyModuleId` found {:?}", err);
                }

                Ok(()) as Result<(), Error>
            }
        });

        core.run(task).unwrap();
    }

    #[test]
    fn module_delete_white_space_module_id_fails() {
        let mut core = Core::new().unwrap();
        let hyper_client = HyperClient::new(&core.handle());
        let client = Client::new(
            hyper_client,
            Some(NullTokenSource),
            "2018-04-11",
            Url::parse("http://localhost").unwrap(),
        ).unwrap();
        let device_client = DeviceClient::new(client, "d1").unwrap();

        let task = device_client
            .delete_module("     ")
            .then(|result| match result {
                Ok(_) => panic!("Expected error but got a result."),
                Err(err) => {
                    if mem::discriminant(err.kind()) != mem::discriminant(&ErrorKind::EmptyModuleId)
                    {
                        panic!("Wrong error kind. Expected `EmptyModuleId` found {:?}", err);
                    }

                    Ok(()) as Result<(), Error>
                }
            });

        core.run(task).unwrap();
    }

    #[test]
    fn module_delete_request() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Delete);
            assert_eq!(req.path(), "/devices/d1/modules/m1");
            assert_eq!(req.headers().get::<IfMatch>().unwrap(), &IfMatch::Any);

            Ok(Response::new().with_status(StatusCode::Ok))
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client
            .delete_module("m1")
            .then(|result| Ok(assert_eq!(result.unwrap(), ())) as Result<(), Error>);

        core.run(task).unwrap();
    }

    #[test]
    fn modules_list_request() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let auth = AuthMechanism::default()
            .with_type(AuthType::Sas)
            .with_symmetric_key(
                SymmetricKey::default()
                    .with_primary_key("pkey".to_string())
                    .with_secondary_key("skey".to_string()),
            );
        let modules = vec![
            Module::default()
                .with_device_id("d1".to_string())
                .with_module_id("m1".to_string())
                .with_generation_id("g1".to_string())
                .with_managed_by("iotedge".to_string())
                .with_authentication(auth.clone()),
            Module::default()
                .with_device_id("d1".to_string())
                .with_module_id("m2".to_string())
                .with_generation_id("g2".to_string())
                .with_managed_by("iotedge".to_string())
                .with_authentication(auth.clone()),
        ];
        let expected_modules = modules.clone();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Get);
            assert_eq!(req.path(), "/devices/d1/modules");
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_header(ContentType::json())
                .with_body(serde_json::to_string(&modules).unwrap().into_bytes()))
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client.list_modules().then(|modules| {
            let modules = modules.unwrap();
            assert_eq!(expected_modules.len(), modules.len());
            for i in 0..modules.len() {
                assert_eq!(expected_modules[i], modules[i])
            }
            Ok(()) as Result<(), Error>
        });

        core.run(task).unwrap();
    }

    #[test]
    fn modules_get_request() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();
        let auth = AuthMechanism::default()
            .with_type(AuthType::Sas)
            .with_symmetric_key(
                SymmetricKey::default()
                    .with_primary_key("pkey".to_string())
                    .with_secondary_key("skey".to_string()),
            );
        let module = Module::default()
            .with_device_id("d1".to_string())
            .with_module_id("m1".to_string())
            .with_generation_id("g1".to_string())
            .with_managed_by("iotedge".to_string())
            .with_authentication(auth.clone());
        let expected_module = module.clone();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Get);
            assert_eq!(req.path(), "/devices/d1/modules/m1");
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new()
                .with_status(StatusCode::Ok)
                .with_header(ContentType::json())
                .with_body(serde_json::to_string(&module).unwrap().into_bytes()))
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client.get_module_by_id("m1").then(|module| {
            let module = module.unwrap();
            assert_eq!(expected_module, module);
            Ok(()) as Result<(), Error>
        });

        core.run(task).unwrap();
    }

    #[test]
    fn modules_get_not_found() {
        let mut core = Core::new().unwrap();
        let api_version = "2018-04-10";
        let host_name = Url::parse("http://localhost").unwrap();

        let handler = move |req: Request| {
            assert_eq!(req.method(), &Method::Get);
            assert_eq!(req.path(), "/devices/d1/modules/m1");
            assert_eq!(None, req.headers().get::<IfMatch>());

            Ok(Response::new().with_status(StatusCode::NotFound))
        };
        let client = Client::new(
            service_fn(handler),
            Some(NullTokenSource),
            api_version,
            host_name,
        ).unwrap();

        let device_client = DeviceClient::new(client, "d1").unwrap();
        let task = device_client.get_module_by_id("m1").then(|module| {
            assert!(module.is_err());
            assert_eq!(ErrorKind::ModuleNotFound, *module.unwrap_err().kind());

            Ok(()) as Result<(), Error>
        });

        core.run(task).unwrap();
    }
}
