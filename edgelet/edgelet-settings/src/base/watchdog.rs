// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryInto;

#[derive(Clone, Debug, Default, serde::Deserialize, serde::Serialize)]
pub struct Settings {
    #[serde(default)]
    pub max_retries: MaxRetries,
}

impl Settings {
    pub fn max_retries(&self) -> MaxRetries {
        self.max_retries
    }
}

#[derive(Clone, Copy, Debug)]
pub enum MaxRetries {
    Infinite,
    Num(u32),
}

impl Default for MaxRetries {
    fn default() -> Self {
        MaxRetries::Infinite
    }
}

impl std::cmp::PartialEq<u32> for MaxRetries {
    fn eq(&self, other: &u32) -> bool {
        match self {
            MaxRetries::Infinite => false,
            MaxRetries::Num(num) => num == other,
        }
    }
}

impl std::cmp::PartialOrd<u32> for MaxRetries {
    fn partial_cmp(&self, other: &u32) -> Option<std::cmp::Ordering> {
        match self {
            MaxRetries::Infinite => Some(std::cmp::Ordering::Greater),
            MaxRetries::Num(num) => num.partial_cmp(other),
        }
    }
}

impl<'de> serde::Deserialize<'de> for MaxRetries {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::de::Deserializer<'de>,
    {
        struct Visitor;

        impl<'de> serde::de::Visitor<'de> for Visitor {
            type Value = MaxRetries;

            fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.write_str(r#""infinite" or u32"#)
            }

            fn visit_str<E>(self, s: &str) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                if s.eq_ignore_ascii_case("infinite") {
                    Ok(MaxRetries::Infinite)
                } else {
                    Err(serde::de::Error::invalid_value(
                        serde::de::Unexpected::Str(s),
                        &self,
                    ))
                }
            }

            fn visit_i64<E>(self, v: i64) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(MaxRetries::Num(
                    v.try_into().map_err(serde::de::Error::custom)?,
                ))
            }

            fn visit_u8<E>(self, v: u8) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(MaxRetries::Num(v.into()))
            }

            fn visit_u16<E>(self, v: u16) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(MaxRetries::Num(v.into()))
            }

            fn visit_u32<E>(self, v: u32) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(MaxRetries::Num(v))
            }

            fn visit_u64<E>(self, v: u64) -> Result<Self::Value, E>
            where
                E: serde::de::Error,
            {
                Ok(MaxRetries::Num(
                    v.try_into().map_err(serde::de::Error::custom)?,
                ))
            }
        }

        deserializer.deserialize_any(Visitor)
    }
}

impl serde::Serialize for MaxRetries {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::ser::Serializer,
    {
        match *self {
            MaxRetries::Infinite => serializer.serialize_str("infinite"),
            MaxRetries::Num(num) => serializer.serialize_u32(num),
        }
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn max_retries_cmp() {
        let max_retries = super::MaxRetries::Infinite;
        assert!(max_retries > 0);
        assert!(max_retries > u32::MAX);

        let max_retries = super::MaxRetries::Num(10);
        assert!(max_retries > 9);
        assert!(max_retries == 10);
        assert!(max_retries < 11);
    }
}
