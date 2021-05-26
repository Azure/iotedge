// Copyright (c) Microsoft. All rights reserved.

#[allow(clippy::module_name_repetitions)]
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd)]
pub enum ApiVersion {
    V2018_06_28,
    V2019_01_30,
    V2019_10_22,
    V2019_11_05,
    V2020_07_07,
    V2020_10_10,
}

impl std::fmt::Display for ApiVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(match self {
            ApiVersion::V2018_06_28 => "2018-06-28",
            ApiVersion::V2019_01_30 => "2019-01-30",
            ApiVersion::V2019_10_22 => "2019-10-22",
            ApiVersion::V2019_11_05 => "2019-11-05",
            ApiVersion::V2020_07_07 => "2020-07-07",
            ApiVersion::V2020_10_10 => "2020-10-10",
        })
    }
}

impl std::str::FromStr for ApiVersion {
    type Err = ();

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match s {
            "2018-06-28" => Ok(ApiVersion::V2018_06_28),
            "2019-01-30" => Ok(ApiVersion::V2019_01_30),
            "2019-10-22" => Ok(ApiVersion::V2019_10_22),
            "2019-11-05" => Ok(ApiVersion::V2019_11_05),
            "2020-07-07" => Ok(ApiVersion::V2020_07_07),
            "2020-10-10" => Ok(ApiVersion::V2020_10_10),
            _ => Err(()),
        }
    }
}
