use std::str::FromStr;

use lazy_static::lazy_static;
use regex::Regex;
use serde::{
    de::{Error as SerdeError, Visitor},
    Deserialize, Deserializer,
};

#[derive(Debug, Copy, Clone, PartialEq)]
pub struct HumanSize(usize);

impl HumanSize {
    pub const fn new_bytes(bytes: usize) -> Self {
        Self(bytes)
    }

    pub fn new_kilobytes(kilobytes: usize) -> Option<Self> {
        kilobytes.checked_mul(1024).map(Self)
    }

    pub fn new_megabytes(megabytes: usize) -> Option<Self> {
        megabytes.checked_mul(1024 * 1024).map(Self)
    }

    pub fn new_gigabytes(gigabytes: usize) -> Option<Self> {
        gigabytes.checked_mul(1024 * 1024 * 1024).map(Self)
    }

    pub const fn get(self) -> usize {
        self.0
    }
}

impl FromStr for HumanSize {
    type Err = ParseHumanSizeError;

    fn from_str(src: &str) -> Result<Self, Self::Err> {
        lazy_static! {
            static ref SIZE_PATTERN: Regex = Regex::new(r"^(\d+)\s*([a-zA-Z]*)$")
                .expect("failed to create new Regex from pattern");
        }

        let captures = SIZE_PATTERN
            .captures(src)
            .ok_or_else(|| ParseHumanSizeError::InvalidFormat(src.to_string()))?;

        let value = captures[1]
            .parse()
            .map_err(|e| ParseHumanSizeError::Value(captures[1].to_string(), e))?;

        match captures[2].to_lowercase().as_str() {
            "" | "b" => Ok(Some(Self::new_bytes(value))),
            "kb" => Ok(Self::new_kilobytes(value)),
            "mb" => Ok(Self::new_megabytes(value)),
            "gb" => Ok(Self::new_gigabytes(value)),
            _ => Err(ParseHumanSizeError::MeasurementUnit(
                captures[2].to_string(),
            )),
        }
        .and_then(|size| size.ok_or_else(|| ParseHumanSizeError::TooBig(src.to_string())))
    }
}

impl<'de> Deserialize<'de> for HumanSize {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct StringOrUsize;

        impl<'de> Visitor<'de> for StringOrUsize {
            type Value = HumanSize;

            fn expecting(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                formatter.write_str("human size string or non-negative number")
            }

            fn visit_u64<E>(self, value: u64) -> Result<Self::Value, E>
            where
                E: SerdeError,
            {
                // it is ok to truncate value on 32bit systems.
                // impossible to allocate more than usize::MAX memory anyway
                #[allow(clippy::cast_possible_truncation)]
                Ok(HumanSize::new_bytes(value as usize))
            }

            fn visit_i64<E>(self, value: i64) -> Result<Self::Value, E>
            where
                E: SerdeError,
            {
                if value < 0 {
                    Err(E::custom(format!(
                        "non-negative number expected: {}",
                        value
                    )))
                } else {
                    // it is ok to truncate value on 32bit systems.
                    // impossible to allocate more than usize::MAX memory anyway
                    #[allow(clippy::cast_sign_loss, clippy::cast_possible_truncation)]
                    Ok(HumanSize::new_bytes(value as usize))
                }
            }

            fn visit_str<E>(self, value: &str) -> Result<Self::Value, E>
            where
                E: SerdeError,
            {
                value.parse().map_err(SerdeError::custom)
            }
        }

        deserializer.deserialize_any(StringOrUsize)
    }
}

#[derive(Debug, PartialEq, thiserror::Error)]
pub enum ParseHumanSizeError {
    #[error("Found size: {0} but expected size value in the following format '256kb'")]
    InvalidFormat(String),

    #[error("Found unit: {0} but expected following units: b, kb, mb, gb")]
    MeasurementUnit(String),

    #[error("Unable to parse number: {0}")]
    Value(String, std::num::ParseIntError),

    #[error("Size value is too big: {0}")]
    TooBig(String),
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use proptest::prelude::*;
    use serde::Deserialize;
    use serde_json::json;
    use test_case::test_case;

    use super::HumanSize;

    #[test]
    fn it_creates_size() {
        assert_eq!(HumanSize::new_bytes(1000).get(), 1000);
        assert_eq!(HumanSize::new_bytes(usize::MAX).get(), usize::MAX);

        assert_eq!(
            HumanSize::new_kilobytes(1000),
            Some(HumanSize::new_bytes(1000 * 1024))
        );
        assert_eq!(HumanSize::new_kilobytes(usize::MAX), None);
        assert_eq!(
            HumanSize::new_megabytes(1000),
            HumanSize::new_kilobytes(1000 * 1024)
        );
        assert_eq!(HumanSize::new_megabytes(usize::MAX), None);
        assert_eq!(
            HumanSize::new_gigabytes(1000),
            HumanSize::new_megabytes(1000 * 1024)
        );
        assert_eq!(HumanSize::new_gigabytes(usize::MAX), None);
    }

    #[test_case("123", HumanSize::new_bytes(123); "when missing unit")]
    #[test_case("123b", HumanSize::new_bytes(123); "when using b")]
    #[test_case("123kb", HumanSize::new_kilobytes(123).unwrap(); "when using kb")]
    #[test_case("123mb", HumanSize::new_megabytes(123).unwrap(); "when using mb")]
    fn it_parses_with_supported_unit(input: &str, expected: HumanSize) {
        let size = input.parse();
        assert_eq!(size, Ok(expected));
    }

    #[cfg(target_pointer_width = "64")]
    #[test_case("123gb", HumanSize::new_gigabytes(123).unwrap(); "when using gb")]
    fn it_parses_with_supported_unit_x64(input: &str, expected: HumanSize) {
        let size = input.parse();
        assert_eq!(size, Ok(expected));
    }

    #[cfg(target_pointer_width = "32")]
    #[test_case("123gb"; "when using gb")]
    fn it_fails_with_too_big_for_x32(input: &str) {
        use crate::settings::size::ParseHumanSizeError;

        let size: Result<HumanSize, ParseHumanSizeError> = input.parse();
        assert_matches!(size, Err(ParseHumanSizeError::TooBig(_)));
    }

    #[test_case("123kb"; "when using all lowercase")]
    #[test_case("123Kb"; "when using mixed case")]
    #[test_case("123KB"; "when using all capitals")]
    #[test_case("123 kb"; "when using separator spaces")]
    fn it_parses(input: &str) {
        let size = input.parse::<HumanSize>();
        assert_eq!(size, Ok(HumanSize::new_kilobytes(123).unwrap()));
    }

    #[test_case("123tb" ; "when using unknown unit")]
    #[test_case("12a3 kb" ; "when invalid number")]
    #[test_case("-123kb" ; "when negative")]
    #[test_case(" 123kb"; "when using leading spaces")]
    #[test_case("123kb "; "when using trailing spaces")]
    #[test_case(" 123 kb "; "when using spaces at multiple positions")]
    fn it_cannot_parse(input: &str) {
        let size = input.parse::<HumanSize>();
        assert_matches!(size, Err(_));
    }

    #[test]
    fn it_deserializes_from_json_string_value() {
        let json = json!({ "size": "256b" }).to_string();
        let size = serde_json::from_str::<Container>(&json).expect("container");
        assert_eq!(
            size,
            Container {
                size: HumanSize::new_bytes(256)
            }
        );
    }

    #[test]
    fn it_deserializes_from_json_number() {
        let json = json!({ "size": 256 }).to_string();
        let size = serde_json::from_str::<Container>(&json).expect("container");
        assert_eq!(
            size,
            Container {
                size: HumanSize::new_bytes(256)
            }
        );
    }

    #[test]
    fn it_cannot_deserialize_from_json_when_invalid_input() {
        let json = json!({ "size": "2560000000000000gb" }).to_string();
        let size = serde_json::from_str::<Container>(&json);
        assert_matches!(size, Err(_));
    }

    #[derive(Debug, Deserialize, PartialEq)]
    struct Container {
        size: HumanSize,
    }

    proptest! {
        #[test]
        fn it_does_not_panic(input in "\\PC*") {
            let _ = input.parse::<HumanSize>();
        }

        #[test]
        #[cfg(target_pointer_width = "64")]
        fn it_can_parse_input(input in r"[0-9]{9}\s*(k|K|m|M|g|G)?(b|B)") {
            let size = input.parse::<HumanSize>();
            prop_assert!(size.is_ok())
        }

        #[test]
        fn it_cannot_parse_input(input in r"[^0-9]+[0-9]{9}[^0-9]+(k|K|m|M|g|G)?.*") {
            let size = input.parse::<HumanSize>();
            prop_assert!(size.is_err())
        }
    }
}
