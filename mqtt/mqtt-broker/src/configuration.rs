use std::convert::{From, TryFrom};
use std::ops::Mul;
use std::path::{Path, PathBuf};
use std::str::FromStr;
use std::time::Duration;

use crate::{transport::TransportBuilder, Error, ErrorKind, InitializeBrokerReason};
use config::{Config, ConfigError, File, FileFormat};
use failure::ResultExt;
use lazy_static::lazy_static;
use native_tls::Identity;
use regex::Regex;
use serde::{Deserialize, Deserializer};
use tracing::info;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Transport {
    Tcp {
        address: String,
    },
    Tls {
        address: String,
        certificate: PathBuf,
    },
}

impl TryFrom<Transport> for TransportBuilder<String> {
    type Error = Error;

    fn try_from(transport: Transport) -> Result<Self, Self::Error> {
        match transport {
            Transport::Tcp { address } => Ok(Self::Tcp(address)),
            Transport::Tls {
                address,
                certificate,
            } => load_identity(certificate.as_path()).map(|identity| Self::Tls(address, identity)),
        }
    }
}

fn load_identity(path: &Path) -> Result<Identity, Error> {
    info!("Loading identity from {:?}", path);
    let cert_buffer = std::fs::read(&path).context(ErrorKind::InitializeBroker(
        InitializeBrokerReason::LoadIdentity(path.to_path_buf()),
    ))?;
    let cert = Identity::from_pkcs12(cert_buffer.as_slice(), "").context(
        ErrorKind::InitializeBroker(InitializeBrokerReason::DecodeIdentity),
    )?;

    Ok(cert)
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QueueFullAction {
    DropNew,
    DropOld,
    Disconnect,
}

#[derive(Debug, Deserialize)]
pub struct InflightMessages {
    max_count: u32,
}

#[derive(Debug, Deserialize)]
pub struct RetainedMessages {
    max_count: u32,
    #[serde(with = "humantime_serde")]
    expiration: Duration,
}

#[derive(Debug, Deserialize)]
pub struct SessionMessages {
    #[serde(deserialize_with = "humansize")]
    max_message_size: u64,
    max_count: u32,
    #[serde(deserialize_with = "humansize")]
    max_total_space: u64,
    when_full: QueueFullAction,
}

#[derive(Debug, Deserialize)]
pub struct SessionPersistence {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
    unsaved_message_count: u32,
}

#[derive(Debug, Deserialize)]
pub struct Session {
    #[serde(with = "humantime_serde")]
    expiration: Duration,
    messages: SessionMessages,
}

#[derive(Debug, Deserialize)]
pub struct BrokerConfig {
    transports: Vec<Transport>,
    inflight_messages: InflightMessages,
    retained_messages: RetainedMessages,
    session: Session,
    persistence: Option<SessionPersistence>,
}

impl BrokerConfig {
    pub fn transports(&self) -> &Vec<Transport> {
        &self.transports
    }
}

pub fn humansize<'de, T, D>(deserializer: D) -> Result<T, D::Error>
where
    T: FromStr + Mul<Output = T> + From<u32>,
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
        .parse::<T>()
        .or_else(|_| Err(error::<D>(&captures[1], &"256")))?;

    let multiplier = captures[2].to_lowercase();
    let multiplier = get_multiplier::<T, D>(multiplier.as_ref())?;

    Ok(base * multiplier)
}

fn get_multiplier<'de, T, D>(str: &str) -> Result<T, D::Error>
where
    T: From<u32>,
    D: Deserializer<'de>,
{
    let result = match str {
        "b" => 1,
        "kb" => 1024,
        "mb" => 1024 * 1024,
        "gb" => 1024 * 1024 * 1024,
        _ => return Err(error::<D>(str, &"'b', 'kb', 'mb' or 'gb'")),
    };

    Ok(result.into())
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
            Duration::from_secs(60 * 24 * 60 * 60)
        );
    }

    #[test]
    fn it_overrides_defaults() {
        let settings = BrokerConfig::from_file(Path::new("test/config_correct.json"))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.retained_messages.expiration,
            Duration::from_secs(90 * 24 * 60 * 60)
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
        size: u64,
    }

    #[test_case( "123b",  123 ; "when using bytes")]
    #[test_case( "123kb",  123*1024 ; "when using kilobytes")]
    #[test_case( "123mb",  123*1024*1024 ; "when using megabytes")]
    #[test_case( "123gb",  123*1024*1024*1024 ; "when using gigabytes")]
    fn it_deserializes_different_multipliers(input: &str, expected: u64) {
        let container_json = json!({ "size": input }).to_string();

        let container: Container = serde_json::from_str(&container_json).unwrap();
        assert_eq!(container.size, expected);
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
        assert_eq!(container.size, expected);
    }

    #[test_case( "123tb" ; "when using unknown unit")]
    #[test_case( "123" ; "when missing unit")]
    #[test_case( "12a3 kb" ; "when invalid number")]
    fn it_fails_deserializing_unknown_unit(input: &str) {
        let container_json = json!({ "size": input }).to_string();
        let result = serde_json::from_str::<Container>(&container_json);

        assert_matches!(result, Err(_err));
    }

    const WSPACE: &str = r"\s*";
    const UNIT: &str = r"(k|K|m|M|g|G)?(b|B)";

    #[derive(Debug)]
    struct HumanUnitSize {
        lead: String,
        num: u64,
        sep: String,
        unit: String,
        trail: String,
    }

    impl ToString for HumanUnitSize {
        fn to_string(&self) -> String {
            let max_value = max_num_for_unit(self.num, &self.unit).to_string();
            format!(
                "{}{}{}{}{}",
                &self.lead, &max_value, &self.sep, &self.unit, &self.trail
            )
        }
    }

    impl From<HumanUnitSize> for u64 {
        fn from(size: HumanUnitSize) -> u64 {
            expected_result_for_number_and_unit(size.num, &size.unit)
        }
    }

    proptest! {
        #[test]
        fn it_does_not_panic(s in "\\PC*") {
            let container_json = json!({ "size": s }).to_string();
            let _result = serde_json::from_str::<Container>(&container_json);
        }
    }

    proptest! {
        #[test]
        fn it_parses_valid_input(size in arbitrary_size()) {
            let container_json = json!({ "size": size.to_string() }).to_string();
            let result = serde_json::from_str::<Container>(&container_json).unwrap();
            let expected: u64 = size.into();

            assert_eq!(result.size, expected);
        }
    }

    prop_compose! {
        fn arbitrary_size()(
            lead in WSPACE, num in any::<u64>(), sep in WSPACE, unit in UNIT, trail in WSPACE
        ) -> HumanUnitSize {
            HumanUnitSize{ lead, num, sep, unit, trail }
        }
    }

    fn expected_result_for_number_and_unit(num: u64, unit: &str) -> u64 {
        let num = max_num_for_unit(num, &unit);
        match unit.to_lowercase().chars().next() {
            Some('k') => num << 10,
            Some('m') => num << 20,
            Some('g') => num << 30,
            Some('b') => num,
            _ => panic!("unknown unit generated"),
        }
    }

    // as the number will be multiplied by a unit, there is a maximum
    // number for every unit that still can fit in u64
    fn max_num_for_unit(num: u64, unit: &str) -> u64 {
        match unit.to_lowercase().chars().next() {
            Some('k') => num % 0x3F_FFFF_FFFF_FFFF,
            Some('m') => num % 0xFFF_FFFF_FFFF,
            Some('g') => num % 0x3_FFFF_FFFF,
            Some('b') => num,
            _ => panic!("unknown unit generated"),
        }
    }
}
