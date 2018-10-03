// Copyright (c) Microsoft. All rights reserved.

use std::str;

use hex::{decode, encode};
use hyper::client::connect::Destination;
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

    pub fn get_pipe_path(dst: &Destination) -> Result<String> {
        Uri::get_pipe_path_from_parts(dst.scheme(), dst.host())
    }

    fn get_pipe_path_from_parts(scheme: &str, host: &str) -> Result<String> {
        if scheme != NAMED_PIPE_SCHEME {
            Err(ErrorKind::InvalidUrlScheme)?
        } else {
            let host = host.trim();
            if host.is_empty() {
                return Err(Error::from(ErrorKind::MissingUrlHost));
            }

            let bytes = decode(host).map_err(Error::from)?;

            let s = str::from_utf8(bytes.as_slice()).map_err(Error::from)?;
            Ok(s.to_owned())
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
        let uri: HyperUri = "foo://boo".parse().unwrap();
        assert!(
            Uri::get_pipe_path_from_parts(uri.scheme_part().unwrap().as_str(), uri.host().unwrap())
                .is_err()
        );
    }

    #[test]
    fn uri_host_decode() {
        let uri: HyperUri = "npipe://123/".parse().unwrap();
        assert!(
            Uri::get_pipe_path_from_parts(uri.scheme_part().unwrap().as_str(), uri.host().unwrap())
                .is_err()
        );
    }

    #[test]
    fn uri_host() {
        let uri: HyperUri =
            "npipe://5c5c2e5c706970655c646f636b65725f656e67696e65/containers/json?all=true"
                .parse()
                .unwrap();
        assert_eq!(
            &Uri::get_pipe_path_from_parts(
                uri.scheme_part().unwrap().as_str(),
                uri.host().unwrap()
            ).unwrap(),
            "\\\\.\\pipe\\docker_engine"
        );
    }
}
