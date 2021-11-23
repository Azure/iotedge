// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;
use std::{borrow::Cow, collections::BTreeMap};

use serde_json;
use typed_headers::{self, http};

use crate::models::ContainerCreateBody;

impl FromStr for ContainerCreateBody {
    type Err = serde_json::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        serde_json::from_str(s)
    }
}

#[derive(Clone, Debug, PartialEq)]
pub(crate) struct UserAgent<'a>(pub Cow<'a, str>);

impl<'a> typed_headers::Header for UserAgent<'a> {
    fn name() -> &'static http::header::HeaderName {
        &http::header::USER_AGENT
    }

    fn from_values<'b>(
        values: &mut http::header::ValueIter<'b, http::header::HeaderValue>,
    ) -> Result<Option<Self>, typed_headers::Error>
    where
        Self: Sized,
    {
        match values.next() {
            Some(value) => {
                let value = value
                    .to_str()
                    .map_err(|_| typed_headers::Error::invalid_value())?
                    .trim()
                    .to_string()
                    .into();
                Ok(Some(UserAgent(value)))
            }

            None => Ok(None),
        }
    }

    fn to_values(&self, values: &mut typed_headers::ToValues<'_>) {
        typed_headers::util::encode_single_value(&self.0, values);
    }
}

pub fn parse_docker_env(docker_env: Option<&[String]>) -> BTreeMap<&str, &str> {
    let mut result = BTreeMap::new();
    if let Some(env) = docker_env {
        // extend merged_env with variables in cur_env (these are
        // only string slices pointing into strings inside cur_env)
        result.extend(env.iter().filter_map(|s| {
            let mut tokens = s.splitn(2, '=');
            tokens.next().map(|key| (key, tokens.next().unwrap_or("")))
        }));
    }

    result
}
