use failure::ResultExt;
use reqwest::header::HeaderValue;
use reqwest::Url;
use serde::{Deserialize, Serialize};

use crate::error::*;

/// Basic struct to indicate pagination options
#[derive(Debug, Serialize, Deserialize)]
pub struct Paginate {
    #[serde(rename = "n")]
    pub n: usize,
    #[serde(rename = "last")]
    pub last: String,
}

impl Paginate {
    /// Create a new pagination definition
    /// - `n` - Limit the number of entries in each response.
    /// - `last` - Result set will include values lexically after last.
    pub fn new(n: usize, last: String) -> Paginate {
        Paginate { n, last }
    }

    /// Create a new pagination definition from a "Link" header
    pub fn from_link_header(header: &HeaderValue, base_url: &Url) -> Result<Paginate> {
        let url = header
            .to_str() // might be invalid utf-8
            .context(ErrorKind::ApiPaginationLink)?
            .splitn(2, ';')
            .next() // might not be well-formatted
            .ok_or_else(|| ErrorKind::ApiPaginationLink)?
            .trim_start_matches('<')
            .trim_end_matches('>');

        let options = Url::options().base_url(Some(base_url));
        let url = options.parse(url).context(ErrorKind::ApiPaginationLink)?;
        let query = url
            .query() // might not have query component
            .ok_or_else(|| ErrorKind::ApiPaginationLink)?;
        let next_paginate =
            serde_urlencoded::from_str::<Paginate>(query).context(ErrorKind::ApiPaginationLink)?;
        Ok(next_paginate)
    }
}
