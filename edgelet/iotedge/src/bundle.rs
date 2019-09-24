// Copyright (c) Microsoft. All rights reserved.

use futures::future::{self, FutureResult};

use crate::error::Error;
use crate::Command;

#[derive(Default)]
pub struct Bundle;

impl Bundle {
    pub fn new() -> Self {
        Bundle
    }
}

impl Command for Bundle {
    type Future = FutureResult<(), Error>;

    #[allow(clippy::print_literal)]
    fn execute(&mut self) -> Self::Future {
        println!("Test!");
        future::ok(())
    }
}
