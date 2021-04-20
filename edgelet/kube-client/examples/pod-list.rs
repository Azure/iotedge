// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
use std::result::Result;

use futures::prelude::*;

use kube_client::{get_config, Client};
use tokio::runtime::Runtime;

fn main() -> Result<(), ()> {
    env_logger::init();

    let config = get_config().map_err(|_| ())?;
    let mut client = Client::new(config);
    let fut = client.list_pods("default", None).map(|pods| {
        for p in pods.items {
            println!("{:#?}", p);
        }
    });

    let mut runtime = Runtime::new().map_err(|_| ())?;
    runtime.block_on(fut).map_err(|_| ())?;
    Ok(())
}
