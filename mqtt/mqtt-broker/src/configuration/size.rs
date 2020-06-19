use std::str::FromStr;

use lazy_static::lazy_static;
use regex::Regex;
use serde::{Deserialize, Deserializer};

#[derive(Debug, Copy, Clone, PartialEq)]
pub struct HumanSize(usize);

impl HumanSize {
    pub const fn new(bytes: usize) -> Self {
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

    pub const fn get(&self) -> usize {
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
            .map_err(|_| ParseHumanSizeError::Value(captures[1].to_string()))?;

        match captures[2].to_lowercase().as_str() {
            "b" => Ok(Some(Self::new(value))),
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
        let input = String::deserialize(deserializer)?;
        input.parse().map_err(serde::de::Error::custom)
    }
}

#[derive(Debug, PartialEq, thiserror::Error)]
pub enum ParseHumanSizeError {
    #[error("Found size: {0} but expected size value in the following format '256kb'")]
    InvalidFormat(String),

    #[error("Found unit: {0} but expected following units: b, kb, mb, gb")]
    MeasurementUnit(String),

    #[error("Unable to parse number: {0}")]
    Value(String),

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

    use super::{HumanSize, ParseHumanSizeError};

    #[test]
    fn it_creates_size() {
        assert_eq!(HumanSize::new(1000).get(), 1000);
        assert_eq!(HumanSize::new(usize::MAX).get(), usize::MAX);

        assert_eq!(
            HumanSize::new_kilobytes(1000),
            Some(HumanSize::new(1000 * 1024))
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

    #[test_case("123b", HumanSize::new(123); "when using bytes")]
    #[test_case("123kb", HumanSize::new_kilobytes(123).unwrap(); "when using kilobytes")]
    #[test_case("123mb", HumanSize::new_megabytes(123).unwrap(); "when using megabytes")]
    #[test_case("123gb", HumanSize::new_gigabytes(123).unwrap(); "when using gigabytes")]
    fn it_parses_with_supported_unit(input: &str, expected: HumanSize) {
        let size = input.parse();
        assert_eq!(size, Ok(expected));
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
    #[test_case("123" ; "when missing unit")]
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
    fn it_deserializes_from_json() {
        let json = json!({ "size": "256kb" }).to_string();
        let size = serde_json::from_str::<Container>(&json).expect("container");
        assert_eq!(
            size,
            Container {
                size: HumanSize::new_kilobytes(256).unwrap()
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
        fn it_can_parse_input(input in r".*[0-9]{9}\s*(k|K|m|M|g|G)?(b|B).*") {
            let size = input.parse::<HumanSize>();
            if let Err(err) = size {
                prop_assert_eq!(err, ParseHumanSizeError::InvalidFormat(input));
            }
        }
    }
}
