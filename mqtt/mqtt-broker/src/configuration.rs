use config::{Config, ConfigError, File, FileFormat};
use lazy_static::lazy_static;
use regex::Regex;
use serde::{Deserialize, Deserializer};
use std::path::Path;
use std::time::Duration;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Deserialize)]
pub struct InflightMessages {
    max_count: u32,
}

#[derive(Debug, Deserialize)]
pub struct RetainedMessages {
    max_count: Option<u32>,
    #[serde(with = "humantime_serde")]
    expiration: Option<Duration>,
}

#[derive(Debug, Deserialize)]
pub struct SessionMessages {
    #[serde(deserialize_with = "humansize")]
    max_message_size: Option<u64>,
    max_count: Option<u32>,
    #[serde(deserialize_with = "humansize")]
    max_total_space: Option<u64>,
    when_full: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct SessionPersistence {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Option<Duration>,
    unsaved_message_count: Option<u32>,
}

#[derive(Debug, Deserialize)]
pub struct Session {
    #[serde(with = "humantime_serde")]
    expiration: Option<Duration>,
    messages: SessionMessages,
}

#[derive(Debug, Deserialize)]
pub struct BrokerConfig {
    inflight_messages: InflightMessages,
    retained_messages: RetainedMessages,
    session: Session,
    persistence: Option<SessionPersistence>,
}

pub fn humansize<'de, D>(deserializer: D) -> Result<Option<u64>, D::Error>
where
    D: Deserializer<'de>,
{
    lazy_static! {
        static ref SIZE_PATTERN: Regex = Regex::new(r"\s*(\d+)\s*([a-zA-Z]+)\s*")
            .expect("failed to create new Regex from pattern");
    }

    let s = String::deserialize(deserializer)?;

    let captures = SIZE_PATTERN
        .captures(&s.as_str())
        .ok_or_else(|| error::<D>(&s, &"256kb"))?;
    let base = captures[1]
        .parse::<u64>()
        .or_else(|_| Err(error::<D>(&captures[1], &"256")))?;

    let multiplier = captures[2].to_lowercase();
    let multiplier = get_multiplier::<D>(multiplier.as_ref())?;

    Ok(Some(base * multiplier))
}

fn get_multiplier<'de, D>(str: &str) -> Result<u64, D::Error>
where
    D: Deserializer<'de>,
{
    let result = match str {
        "b" => 1,
        "kb" => 1024,
        "mb" => 1024 * 1024,
        "gb" => 1024 * 1024 * 1024,
        _ => return Err(error::<D>(str, &"'b', 'kb', 'mb' or 'gb'")),
    };

    Ok(result)
}

fn error<'de, D>(unexpected: &str, expected: &str) -> D::Error
where
    D: Deserializer<'de>,
{
    serde::de::Error::invalid_value(serde::de::Unexpected::Str(&unexpected), &expected)
}

impl BrokerConfig {
    pub fn new() -> Result<Self, ConfigError> {
        let mut s = Config::new();
        s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;

        s.try_into()
    }

    pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self, ConfigError> {
        let mut s = Config::new();
        s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        s.merge(File::from(path.as_ref()))?;

        s.try_into()
    }

    pub fn persistence(&self) -> Option<&SessionPersistence> {
        self.persistence.as_ref()
    }
}

#[cfg(test)]
mod tests {
    use std::path::Path;
    use std::time::Duration;

    use matches::assert_matches;
    use proptest::prelude::*;
    use serde::Deserialize;
    use serde_json::json;
    use test_case::test_case;

    use super::*;

    #[test]
    fn it_loads_defaults() {
        let settings = BrokerConfig::new().expect("should be able to create default instance");

        assert_eq!(
            settings.retained_messages.expiration,
            Some(Duration::from_secs(60 * 24 * 60 * 60))
        );
    }

    #[test]
    fn it_overrides_defaults() {
        let settings = BrokerConfig::from_file(Path::new("test/config_correct.json"))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.retained_messages.expiration,
            Some(Duration::from_secs(90 * 24 * 60 * 60))
        );
    }

    #[test]
    fn it_refuses_persistence_with_no_file_path() {
        let settings = BrokerConfig::from_file(Path::new("test/config_no_file_path.json"));

        assert_matches!(settings, Err(_err));
    }

    #[test]
    fn it_type_mismatch_fails() {
        let settings = BrokerConfig::from_file(Path::new("test/config_bad_value_type.json"));

        assert_matches!(settings, Err(_err));
    }

    #[derive(Debug, Deserialize)]
    struct Container {
        #[serde(deserialize_with = "humansize")]
        size: Option<u64>,
    }

    #[test_case( "123b",  123 ; "when using bytes")]
    #[test_case( "123kb",  123*1024 ; "when using kilobytes")]
    #[test_case( "123mb",  123*1024*1024 ; "when using megabytes")]
    #[test_case( "123gb",  123*1024*1024*1024 ; "when using gigabytes")]
    fn it_deserializes_different_multipliers(input: &str, expected: u64) {
        let container_json = json!({ "size": input }).to_string();

        let container: Container = serde_json::from_str(&container_json).unwrap();
        assert_eq!(container.size, Some(expected));
    }

    #[test_case( "123kb",  123*1024 ; "when using all lowercase")]
    #[test_case( "123Kb",  123*1024 ; "when using mixed case")]
    #[test_case( "123KB",  123*1024 ; "when using all capitals")]
    #[test_case( "  123kb",  123*1024 ; "when using leading spaces")]
    #[test_case( "123kb  ",  123*1024 ; "when using trailing spaces")]
    #[test_case( "123  kb",  123*1024 ; "when using separator spaces")]
    #[test_case( "  123  kb  ",  123*1024 ; "when using spaces at multiple positions")]
    fn it_deserializes_different_writing_modes(input: &str, expected: u64) {
        let container_json = json!({ "size": input }).to_string();

        let container: Container = serde_json::from_str(&container_json).unwrap();
        assert_eq!(container.size, Some(expected));
    }

    #[test_case( "123tb" ; "when using unknown unit")]
    #[test_case( "123" ; "when missing unit")]
    #[test_case( "12a3 kb" ; "when invalid number")]
    fn it_fails_deserializing_unknown_unit(input: &str) {
        let container_json = json!({ "size": input }).to_string();
        let result = serde_json::from_str::<Container>(&container_json);

        assert_matches!(result, Err(_err));
    }

    proptest! {
        #[test]
        fn doesnt_crash(s in "\\PC*") {
            parse_size_anything(&s);
        }
    }

    proptest! {
        #[test]
        fn it_parses_valid_input(p in (r"\s*",             // leading spaces if any
                                       any::<u64>(),       // the numeric value
                                       r"\s*",             // spaces between the number and unit
                                       r"(k|K|m|M|g|G)?(b|B)", // the unit
                                       r"\s*")             // trailing spaces if any
                                    .prop_map(|(lead, num, sep, unit, trail)| get_input_and_expected(&lead, num, &sep, &unit, &trail))) {
            let (input, expected) = p;
            parse_size_valid(&input, expected);
        }
    }

    // as the number will be multiplied by a unit, there is a maximum
    // number for every unit that still can fit in u64
    fn max_num_for_unit(num: u64, unit: &str) -> u64 {
        match unit.to_lowercase().chars().nth(0) {
            Some('k') => num % 0x3F_FFFF_FFFF_FFFF,
            Some('m') => num % 0xFFF_FFFF_FFFF,
            Some('g') => num % 0x3_FFFF_FFFF,
            Some('b') => num,
            _ => panic!("unknown unit generated"),
        }
    }

    fn expected_result_for_number_and_unit(num: u64, unit: &str) -> u64 {
        let num = max_num_for_unit(num, &unit);
        match unit.to_lowercase().chars().nth(0) {
            Some('k') => num << 10,
            Some('m') => num << 20,
            Some('g') => num << 30,
            Some('b') => num,
            _ => panic!("unknown unit generated"),
        }
    }

    fn get_input_and_expected(
        lead: &str,
        num: u64,
        sep: &str,
        unit: &str,
        trail: &str,
    ) -> (String, u64) {
        (
            [
                lead,
                &max_num_for_unit(num, &unit).to_string(),
                sep,
                unit,
                trail,
            ]
            .concat(),
            expected_result_for_number_and_unit(num, &unit),
        )
    }

    fn parse_size_anything(input: &str) {
        let container_json = json!({ "size": input }).to_string();
        let _result = serde_json::from_str::<Container>(&container_json);
    }

    fn parse_size_valid(input: &str, expected: u64) {
        let container_json = json!({ "size": input }).to_string();
        let result = serde_json::from_str::<Container>(&container_json).unwrap();

        assert_eq!(result.size, Some(expected));
    }
}
