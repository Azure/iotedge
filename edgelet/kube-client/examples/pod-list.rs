// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use futures::prelude::*;

use kube_client::error::Result;
use kube_client::{get_config, Client};
use tokio::runtime::Runtime;

fn main() -> Result<()> {
    env_logger::init();

    let config = get_config()?;
    let mut client = Client::new(config);
    let fut = client.list_pods("default", None).map(|pods| {
        for p in pods.items {
            println!("{:#?}", p);
        }
    });

    Runtime::new()?.block_on(fut)?;
    Ok(())
}
