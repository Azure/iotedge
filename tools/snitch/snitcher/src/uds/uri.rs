// Copyright (c) Microsoft. All rights reserved.

use std::path::PathBuf;
use std::str;

use hex::{decode, encode};
use url::Url;

use error::{Error, Result};

#[derive(Clone, Debug)]
pub struct Uri {
    url: Url,
}

impl Uri {
    pub fn from_url(url: &Url) -> Result<Uri> {
        if url.scheme() != "unix" {
            Err(Error::InvalidUrlScheme)
        } else if url.path().trim() == "" {
            Err(Error::MissingPath)
        } else {
            Ok(Uri {
                url: Url::parse(&format!("unix://{}", encode(url.path().as_bytes())))?,
            })
        }
    }

    pub fn get_uds_path(host: &str) -> Result<PathBuf> {
        decode(host)
            .map_err(Error::from)
            .and_then(|bytes| {
                str::from_utf8(bytes.as_slice())
                    .map_err(Error::from)
                    .map(|s| s.to_owned())
            })
            .map(PathBuf::from)
    }
}

impl Into<Url> for Uri {
    fn into(self) -> Url {
        self.url.clone()
    }
}
