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

pub fn merge_env(cur_env: Option<&[String]>, new_env: &BTreeMap<String, String>) -> Vec<String> {
    let mut merged_env = BTreeMap::new();
    // build a new merged map containing string slices for keys and values
    // pointing into String instances in new_env
    merged_env.extend(new_env.iter().map(|(k, v)| (k.as_str(), v.as_str())));

    if let Some(env) = cur_env {
        // extend merged_env with variables in cur_env (these are
        // only string slices pointing into strings inside cur_env)
        merged_env.extend(env.iter().filter_map(|s| {
            let mut tokens = s.splitn(2, '=');
            tokens.next().map(|key| (key, tokens.next().unwrap_or("")))
        }));
    }

    // finally build a new Vec<String>; we alloc new strings here
    merged_env
        .iter()
        .map(|(key, value)| format!("{}={}", key, value))
        .collect()
}
