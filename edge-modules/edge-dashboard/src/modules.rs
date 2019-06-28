// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use actix_web::Error as ActixError;
use actix_web::*;
use futures::future::ok;
use futures::Future;

// use edgelet_core::{Module, ModuleRuntime};
// use edgelet_http_mgmt::*;
// use futures::future::Future;
// use serde::{Deserialize, Serialize};
// use url::Url;

// #[derive(Debug, Deserialize, Serialize)]
// pub struct SparseModule {
//     name: String,
// }

// pub fn get_management_uri(contents: &str) -> Option<String> {
//     let start_pattern = "management_uri: ";
//     let start = contents.find(start_pattern)? + start_pattern.len();
//     let end = contents.find("workload_uri: ")?;
//     let pre = contents[start..end].trim();
//     Some(pre[1..pre.len() - 1].to_string())
// }

// pub fn get_list(uri: &str) -> Result<Vec<SparseModule>, url::ParseError> {
//     let contents = Url::parse("http://127.0.0.1:6000/modules/?api-version=2018-06-28")?;
//     let mod_client = ModuleClient::new(&contents).unwrap();

//     let mod_list = mod_client.list(); // <box<future<vec<modules>, error>>

//     // match mod_list.poll() {
//     //     Ok(Async::Ready(mods)) => mods.map(|m| SparseModule {name: m.name().to_string()}).collect(),
//     //     Ok(Async::NotReady)    => ,
//     //     Err(e)                 => Err(e),
//     // }

//     let ret = mod_list
//         .wait()
//         .unwrap()
//         .iter()
//         .map(|m| SparseModule {
//             name: m.name().to_owned(),
//         })
//         .collect();

//     Ok(ret)
// }
