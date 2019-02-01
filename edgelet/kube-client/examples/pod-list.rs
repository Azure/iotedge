// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

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
