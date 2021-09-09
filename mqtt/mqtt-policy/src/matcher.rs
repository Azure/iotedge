use std::str::FromStr;

use mqtt_broker::{
    auth::{Activity, Operation},
    TopicFilter,
};
use policy::{Request, ResourceMatcher};

/// This is MQTT-specific resource matcher that matches topics and topic filters
/// according to MQTT spec.
///
/// # Example:
/// ```json
/// {
///     "effect": "allow",
///     "identities": ["client_1"],
///     "operations": ["mqtt:publish"],
///     "resources": ["floor1/#"]
/// }
/// ```
/// The policy statement above will allow `client_1` to publish to any topic
/// that matches "floor1/#" topic filter (like "floor1/station1/events")
#[derive(Debug)]
pub struct MqttTopicFilterMatcher;

impl ResourceMatcher for MqttTopicFilterMatcher {
    type Context = Activity;

    fn do_match(&self, context: &Request<Activity>, input: &str, policy: &str) -> bool {
        match context.context() {
            Some(context) => {
                match context.operation() {
                    // special case for Connect operation, since it doesn't really have a "resource".
                    Operation::Connect => true,
                    // for pub or sub just match the topic filter.
                    _ => {
                        if let Ok(filter) = TopicFilter::from_str(policy) {
                            filter.matches(input)
                        } else {
                            false
                        }
                    }
                }
            }
            None => false,
        }
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use policy::Request;

    use crate::tests;

    use super::*;

    #[test_case("/foo", "/foo", true; "simple topic matches")]
    #[test_case("/bar", "/foo", false; "simple topic doesn't match")]
    #[test_case("/foo/bar", "/foo/#", true; "wildcard 1")]
    #[test_case("/foo/bar", "/foo/+", true; "wildcard 2")]
    #[test_case("#invalid", "/foo/+", false; "invalid topic")]
    #[test_case("/foo", "#invalid", false; "invalid topic filter")]
    fn do_match_test(input: &str, policy: &str, result: bool) {
        let request = Request::with_context(
            "some_identity",
            "some_operation",
            "some_resource",
            tests::create_publish_activity("client_id", "auth_id"),
        )
        .unwrap();

        // connect operation should match any input value.
        assert_eq!(
            result,
            MqttTopicFilterMatcher.do_match(&request, input, policy)
        );
    }

    #[test]
    fn do_match_connect_activity_test() {
        let request = Request::with_context(
            "some_identity",
            "some_operation",
            "some_resource",
            tests::create_connect_activity("client_id", "auth_id"),
        )
        .unwrap();

        // connect operation should match any input value.
        assert!(MqttTopicFilterMatcher.do_match(&request, "any_value", "ignored_value1"));
        assert!(MqttTopicFilterMatcher.do_match(&request, "some_value", "ignored_value2"));
    }
}
