// Copyright (c) Microsoft. All rights reserved.

use std::str;

use hex::{decode, encode};
use hyper::Uri as HyperUri;
use url::Url;

use super::*;
use error::{Error, ErrorKind, Result};

#[derive(Debug)]
pub struct Uri {
    url: Url,
}

impl Uri {
    pub fn new(base_path: &str, path: &str) -> Result<Uri> {
        // parse base_path as url and extract host and path from it;
        // "host" is the name of the machine which should be "." for localhost
        // and "path" will be "/pipe/<name>" where <name> is the pipe name
        let url = Url::parse(ensure_not_empty!(base_path))?;
        if url.scheme() != NAMED_PIPE_SCHEME {
            Err(ErrorKind::InvalidUrlScheme)?
        } else if url.host_str().map(|h| h.trim()).unwrap_or("") == "" {
            Err(ErrorKind::MissingUrlHost)?
        } else if !url.path().starts_with("/pipe/") || url.path().len() < "/pipe/".len() + 1 {
            Err(ErrorKind::MalformedNamedPipeUrl)?
        } else {
            let pipe_path = format!(
                r"\\{}{}",
                url.host().unwrap(),
                url.path().replace("/", "\\")
            );
            Ok(Uri {
                url: Url::parse(&format!("npipe://{}", encode(pipe_path)))?.join(path)?,
            })
        }
    }

    pub fn get_pipe_path(uri: &HyperUri) -> Result<String> {
        if uri.scheme() != Some(NAMED_PIPE_SCHEME) {
            Err(ErrorKind::InvalidUrlScheme)?
        } else {
            uri.host()
                .map(|h| h.trim())
                .and_then(|h| if h.is_empty() { None } else { Some(h) })
                .ok_or_else(|| Error::from(ErrorKind::MissingUrlHost))
                .and_then(|h| decode(h).map_err(Error::from))
                .and_then(|bytes| {
                    str::from_utf8(bytes.as_slice())
                        .map_err(Error::from)
                        .map(|s| s.to_owned())
                })
        }
    }
}

impl Into<HyperUri> for Uri {
    fn into(self) -> HyperUri {
        // self.url is a valid URL; so we can safely unwrap
        format!("{}", self.url)
            .parse()
            .expect("Failed to convert Url to HyperUri")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn empty_base_path() {
        assert!(Uri::new("", "").is_err());
    }

    #[test]
    fn no_scheme_in_path() {
        assert!(Uri::new("boo", "").is_err());
    }

    #[test]
    fn invalid_scheme_in_path() {
        assert!(Uri::new("bad.scheme://boo", "").is_err());
    }

    #[test]
    fn missing_host_in_path() {
        assert!(Uri::new("npipe://   /boo", "").is_err());
    }

    #[test]
    fn missing_pipe_in_path() {
        assert!(Uri::new("npipe://./boo", "").is_err());
    }

    #[test]
    fn missing_pipe_name() {
        assert!(Uri::new("npipe://./pipe/", "").is_err());
    }

    #[test]
    fn valid_url() {
        assert!(Uri::new("npipe://./pipe/boo", "").is_ok());
    }

    #[test]
    fn url_path() {
        let url = Uri::new("npipe://./pipe/docker_engine", "/containers/json?all=true").unwrap();
        assert_eq!(url.url.path(), "/containers/json");
        assert_eq!(url.url.query(), Some("all=true"));
    }

    #[test]
    fn hyper_uri() {
        let uri: HyperUri = Uri::new("npipe://./pipe/docker_engine", "/containers/json?all=true")
            .unwrap()
            .into();
        let expected =
            "npipe://5c5c2e5c706970655c646f636b65725f656e67696e65/containers/json?all=true";
        assert_eq!(uri, expected.parse::<HyperUri>().unwrap());
    }

    #[test]
    fn uri_host_scheme() {
        let uri = "foo://boo".parse().unwrap();
        assert!(Uri::get_pipe_path(&uri).is_err());
    }

    #[test]
    fn uri_host_empty() {
        let uri = "npipe://   /".parse().unwrap();
        assert!(Uri::get_pipe_path(&uri).is_err());
    }

    #[test]
    fn uri_host_decode() {
        let uri = "npipe://123/".parse().unwrap();
        assert!(Uri::get_pipe_path(&uri).is_err());
    }

    #[test]
    fn uri_host() {
        let uri = "npipe://5c5c2e5c706970655c646f636b65725f656e67696e65/containers/json?all=true"
            .parse()
            .unwrap();
        assert_eq!(
            &Uri::get_pipe_path(&uri).unwrap(),
            "\\\\.\\pipe\\docker_engine"
        );
    }
}
