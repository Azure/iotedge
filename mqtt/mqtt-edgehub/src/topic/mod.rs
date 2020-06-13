pub mod translation;

use lazy_static::lazy_static;
use regex::Regex;

lazy_static! {
    static ref TELEMETRY_TOPIC_MATCHERS: Vec<Regex> = vec![
        Regex::new(r"^devices/[^/]*/messages/(events|devicebound)(\?[^/]*)*$").unwrap(),
        Regex::new(r"^devices/[^/]*/modules/[^/]*/messages/(events|devicebound)(\?[^/]*)*$")
            .unwrap()
    ];
}

pub fn is_iothub(topic: &str) -> bool {
    if topic.starts_with("$edgehub/") {
        return true;
    }

    if topic.starts_with("$iothub/") {
        return true;
    }

    if topic.starts_with("devices/") {
        return TELEMETRY_TOPIC_MATCHERS
            .iter()
            .any(|matcher| matcher.is_match(topic));
    }

    false
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::is_iothub;

    #[test_case("$iothub/anythig"; "iothub prefixed")]
    #[test_case("$edgehub/anythig"; "edgehub prefixed")]
    #[test_case("devices/device_1/messages/events"; "device telemetry D2C")]
    #[test_case("devices/device_1/messages/devicebound"; "device telemetry C2D")]
    #[test_case("devices/device_1/messages/events?path=value"; "device telemetry D2C with path")]
    #[test_case("devices/+/messages/events"; "any device telemetry D2C")]
    #[test_case("devices/device_1/modules/module_id/messages/events"; "module telemetry D2C")]
    #[test_case("devices/device_1/modules/module_id/messages/devicebound"; "module telemetry C2D")]
    #[test_case("devices/device_1/modules/module_id/messages/events?path=value"; "module telemetry D2C with path")]
    #[test_case("devices/+/modules/+/messages/events"; "any module telemetry D2C")]
    fn it_returns_true_for_iothub_topics(topic: &str) {
        assert_eq!(is_iothub(topic), true)
    }

    #[test_case(""; "empty")]
    #[test_case("#"; "any topic multi level wildcard ")]
    #[test_case("+"; "any topic single level wildcard ")]
    #[test_case("generic/topic"; "iothub prefixed")]
    #[test_case("devices/#"; "devices multi level wildcard")]
    fn it_returns_false_for_non_iothub_topics(topic: &str) {
        assert_eq!(is_iothub(topic), false)
    }
}
