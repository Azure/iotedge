use config::{Config, ConfigError, File, FileFormat};
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
pub struct BrokerSettings {
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

impl BrokerSettings {
    pub fn new(path: Option<&Path>) -> Result<Self, ConfigError> {
        let mut s = Config::new();
        s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;

        if let Some(path) = path {
            s.merge(File::from(path))?;
        }

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
    use serde::Deserialize;
    use serde_json::json;
    use test_case::test_case;

    use super::*;

    #[test]
    fn it_loads_defaults() {
        let settings =
            BrokerSettings::new(None).expect("should be able to create default instance");

        assert_eq!(
            settings.retained_messages.expiration,
            Some(Duration::from_secs(60 * 24 * 60 * 60))
        );
    }

    #[test]
    fn it_overrides_defaults() {
        let settings = BrokerSettings::new(Some(Path::new("test/config_correct.json")))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.retained_messages.expiration,
            Some(Duration::from_secs(90 * 24 * 60 * 60))
        );
    }

    #[test]
    fn it_refuses_persistence_with_no_file_path() {
        let settings = BrokerSettings::new(Some(Path::new("test/config_no_file_path.json")));

        assert_matches!(settings, Err(_err));
    }

    #[test]
    fn it_type_mismatch_fails() {
        let settings = BrokerSettings::new(Some(Path::new("test/config_bad_value_type.json")));

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

    #[test_case( "123tb" ; "when using unknown metric")]
    #[test_case( "123" ; "when missing metric")]
    #[test_case( "12a3 kb" ; "when invalid number")]
    fn it_fails_deserializing_unknown_metric(input: &str) {
        let container_json = json!({ "size": input }).to_string();
        let result = serde_json::from_str::<Container>(&container_json);

        assert_matches!(result, Err(_err));
    }
}
