// Copyright (c) Microsoft. All rights reserved.

use serde_json;
use std::borrow::Cow;
use std::str::FromStr;
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
