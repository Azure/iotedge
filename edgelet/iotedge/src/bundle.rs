// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use futures::future::Future;
use futures::prelude::*;
use std::io::{self};

use std::str;

use crate::error::{Error, ErrorKind};
use crate::Command;

use edgelet_core::{Chunked, LogChunk, LogDecode, LogOptions, ModuleRuntime};

pub struct Bundle<M> {
    runtime: M,
}

impl<M> Bundle<M> {
    pub fn new(runtime: M) -> Self {
        Bundle { runtime }
    }
}

impl<M> Command for Bundle<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    // type Future = FutureResult<(), Error>;
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
        println!("Test!");
        let id = "edgeAgent".to_string();
        let log_options = LogOptions::new();

        let log1 = self
            .runtime
            .logs(&id, &log_options)
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .and_then(move |logs| {
                let chunked =
                    Chunked::new(logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")));
                LogDecode::new(chunked)
                    .for_each(|chunk| {
                        match chunk {
                            LogChunk::Stdin(b)
                            | LogChunk::Stdout(b)
                            | LogChunk::Stderr(b)
                            | LogChunk::Unknown(b) => {
                                let test = str::from_utf8(&b).unwrap().to_string();
                                println!("{}", test);
                            }
                        };
                        Ok(())
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            });

        Box::new(log1)
    }
}
