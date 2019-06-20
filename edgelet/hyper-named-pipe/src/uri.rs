// Copyright (c) Microsoft. All rights reserved.

use std::str;

use failure::ResultExt;
use hex::{decode, encode};
use hyper::client::connect::Destination;
use hyper::Uri as HyperUri;
use url::Url;

use edgelet_utils::ensure_not_empty_with_context;

use super::*;
use crate::error::{Error, ErrorKind, InvalidUrlReason, Result};

#[derive(Debug)]
pub struct Uri {
    url: Url,
}

impl Uri {
    pub fn new(base_path: &str, path: &str) -> Result<Self> {
        // parse base_path as url and extract host and path from it;
        // "host" is the name of the machine which should be "." for localhost
        // and "path" will be "/pipe/<name>" where <name> is the pipe name
        ensure_not_empty_with_context(base_path, || ErrorKind::BadBasePath(base_path.to_string()))?;
        let url = Url::parse(base_path)
            .with_context(|_| ErrorKind::BadBasePath(base_path.to_string()))?;

        if url.scheme() != NAMED_PIPE_SCHEME {
            return Err(Error::from(ErrorKind::InvalidUrl(
                url.to_string(),
                InvalidUrlReason::Scheme(url.scheme().to_string()),
            )));
        }

        if url.host_str().map_or("", |h| h.trim()) == "" {
            return Err(Error::from(ErrorKind::InvalidUrl(
                url.to_string(),
                InvalidUrlReason::MissingHost,
            )));
        }

        if !url.path().starts_with("/pipe/") || url.path().len() < "/pipe/".len() + 1 {
            return Err(Error::from(ErrorKind::InvalidUrl(
                url.to_string(),
                InvalidUrlReason::Path(url.path().to_string()),
            )));
        }

        let pipe_path = format!(
            r"\\{}{}",
            url.host().unwrap(),
            url.path().replace("/", "\\")
        );

        Ok(Uri {
            url: Url::parse(&format!("npipe://{}", encode(pipe_path)))
                .context(ErrorKind::ConstructUrlForHyper)?
                .join(path)
                .context(ErrorKind::ConstructUrlForHyper)?,
        })
    }

    pub fn get_pipe_path(dst: &Destination) -> Result<String> {
        Uri::get_pipe_path_from_parts(dst.scheme(), dst.host())
    }

    fn get_pipe_path_from_parts(scheme: &str, host: &str) -> Result<String> {
        if scheme != NAMED_PIPE_SCHEME {
            return Err(Error::from(ErrorKind::InvalidUrl(
                format!("{}://{}", scheme, host),
                InvalidUrlReason::Scheme(scheme.to_string()),
            )));
        }

        let host = host.trim();
        if host.is_empty() {
            return Err(Error::from(ErrorKind::InvalidUrl(
                format!("{}://{}", scheme, host),
                InvalidUrlReason::MissingHost,
            )));
        }

        let bytes = decode(host).with_context(|_| {
            ErrorKind::InvalidUrl(
                format!("{}://{}", scheme, host),
                InvalidUrlReason::BadHost(host.to_string()),
            )
        })?;

        let s = str::from_utf8(bytes.as_slice()).with_context(|_| {
            ErrorKind::InvalidUrl(
                format!("{}://{}", scheme, host),
                InvalidUrlReason::BadHost(host.to_string()),
            )
        })?;
        Ok(s.to_owned())
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
        let url = Uri::new("npipe://./pipe/boo", "/containers/json?all=true").unwrap();
        assert_eq!(url.url.path(), "/containers/json");
        assert_eq!(url.url.query(), Some("all=true"));
    }

    #[test]
    fn hyper_uri() {
        let uri: HyperUri = Uri::new("npipe://./pipe/boo", "/containers/json?all=true")
            .unwrap()
            .into();
        let expected = "npipe://5c5c2e5c706970655c626f6f/containers/json?all=true";
        assert_eq!(uri, expected.parse::<HyperUri>().unwrap());
    }

    #[test]
    fn uri_host_scheme() {
        let uri: HyperUri = "foo://boo".parse().unwrap();
        assert!(Uri::get_pipe_path_from_parts(
            uri.scheme_part().unwrap().as_str(),
            uri.host().unwrap()
        )
        .is_err());
    }

    #[test]
    fn uri_host_decode() {
        let uri: HyperUri = "npipe://123/".parse().unwrap();
        assert!(Uri::get_pipe_path_from_parts(
            uri.scheme_part().unwrap().as_str(),
            uri.host().unwrap()
        )
        .is_err());
    }

    #[test]
    fn uri_host() {
        let uri: HyperUri = "npipe://5c5c2e5c706970655c626f6f/containers/json?all=true"
            .parse()
            .unwrap();
        assert_eq!(
            &Uri::get_pipe_path_from_parts(
                uri.scheme_part().unwrap().as_str(),
                uri.host().unwrap()
            )
            .unwrap(),
            "\\\\.\\pipe\\boo"
        );
    }
}
