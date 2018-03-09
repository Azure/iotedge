// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;
use serde::Deserialize;
use serde_json;

use models::ContainerCreateBody;

impl FromStr for ContainerCreateBody {
    type Err = serde_json::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        serde_json::from_str(s)
    }
}
