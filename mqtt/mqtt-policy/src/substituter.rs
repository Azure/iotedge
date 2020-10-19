use mqtt_broker::auth::Activity;
use policy::{Request, Result, Substituter};

#[allow(clippy::doc_markdown)]
/// MQTT-specific implementation of `Substituter`. It replaces MQTT and IoT Hub specific variables:
/// * `iot:identity`
/// * `iot:device_id`
/// * `iot:module_id`
/// * `iot:client_id`
/// * `iot:topic`
#[derive(Debug)]
pub struct MqttSubstituter {
    device_id: String,
}

impl MqttSubstituter {
    pub fn new(device_id: impl Into<String>) -> Self {
        Self {
            device_id: device_id.into(),
        }
    }

    fn device_id(&self) -> &str {
        &self.device_id
    }

    fn replace_variable(&self, value: &str, context: &Request<Activity>) -> String {
        if let Some(context) = context.context() {
            if let Some(variable) = extract_variable(value) {
                return match variable {
                    crate::CLIENT_ID_VAR => {
                        replace(value, variable, context.client_info().client_id().as_str())
                    }
                    crate::IDENTITY_VAR => {
                        replace(value, variable, context.client_info().auth_id().as_str())
                    }
                    crate::DEVICE_ID_VAR => replace(value, variable, extract_device_id(&context)),
                    crate::MODULE_ID_VAR => replace(value, variable, extract_module_id(&context)),
                    crate::EDGEHUB_ID_VAR => replace(value, variable, self.device_id()),
                    _ => value.to_string(),
                };
            }
        }
        value.to_string()
    }
}

impl Substituter for MqttSubstituter {
    type Context = Activity;

    fn visit_identity(&self, value: &str, context: &Request<Self::Context>) -> Result<String> {
        Ok(self.replace_variable(value, context))
    }

    fn visit_resource(&self, value: &str, context: &Request<Self::Context>) -> Result<String> {
        Ok(self.replace_variable(value, context))
    }
}

pub(super) fn extract_variable(value: &str) -> Option<&str> {
    if let Some(start) = value.find("{{") {
        if let Some(end) = value.find("}}") {
            return Some(&value[start..end + 2]);
        }
    }
    None
}

fn replace(value: &str, variable: &str, substitution: &str) -> String {
    value.replace(variable, substitution)
}

fn extract_device_id(activity: &Activity) -> &str {
    let auth_id = activity.client_info().auth_id().as_str();
    auth_id.split('/').next().unwrap_or_default()
}

fn extract_module_id(activity: &Activity) -> &str {
    let auth_id = activity.client_info().auth_id().as_str();
    auth_id.split('/').nth(1).unwrap_or_default()
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use crate::tests;

    use super::*;

    #[test_case("{{iot:identity}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_auth_id"; 
        "iot:identity variable")]
    #[test_case("namespace-{{iot:identity}}-suffix", 
        "test_device_auth_id",
        "test_device_client_id", 
        "namespace-test_device_auth_id-suffix"; 
        "iot:identity variable substring")]
    #[test_case("{{mqtt:client_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_client_id"; 
        "mqtt:client_id variable")]
    #[test_case("namespace-{{mqtt:client_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-test_device_client_id-suffix"; 
        "mqtt:client_id variable substring")]
    #[test_case("{{iot:device_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_auth_id"; 
        "iot:device_id variable")]
    #[test_case("namespace-{{iot:device_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-test_device_auth_id-suffix"; 
        "iot:device_id variable substring")]
    #[test_case("{{iot:module_id}}", 
        "test_device_id/test_module_id", 
        "test_device_client_id", 
        "test_module_id"; 
        "iot:module_id variable")]
    #[test_case("namespace-{{iot:module_id}}-suffix", 
        "test_device_id/test_module_id", 
        "test_device_client_id", 
        "namespace-test_module_id-suffix"; 
        "iot:module_id variable substring")]
    #[test_case("{{iot:this_device_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "edge_device"; 
        "iot:this_device_id variable")]
    #[test_case("namespace-{{iot:this_device_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-edge_device-suffix"; 
        "iot:this_device_id variable substring")]
    #[test_case("{{invalid}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "{{invalid}}"; 
        "invalid variable")]
    #[test_case("{{{}bad}}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "{{{}bad}}}"; 
        "bad variable")]
    fn visit_identity_test(input: &str, auth_id: &str, client_id: &str, expected: &str) {
        let request = Request::with_context(
            "some_identity",
            "some_operation",
            "some_resource",
            tests::create_connect_activity(client_id, auth_id),
        )
        .unwrap();

        assert_eq!(
            expected,
            MqttSubstituter::new("edge_device")
                .visit_identity(input, &request)
                .unwrap()
        );
    }

    #[test_case("{{iot:identity}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_auth_id"; 
        "iot:identity variable")]
    #[test_case("namespace-{{iot:identity}}-suffix", 
        "test_device_auth_id",
        "test_device_client_id", 
        "namespace-test_device_auth_id-suffix"; 
        "iot:identity variable substring")]
    #[test_case("{{mqtt:client_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_client_id"; 
        "mqtt:client_id variable")]
    #[test_case("namespace-{{mqtt:client_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-test_device_client_id-suffix"; 
        "mqtt:client_id variable substring")]
    #[test_case("{{iot:device_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "test_device_auth_id"; 
        "iot:device_id variable")]
    #[test_case("namespace-{{iot:device_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-test_device_auth_id-suffix"; 
        "iot:device_id variable substring")]
    #[test_case("{{iot:module_id}}", 
        "test_device_id/test_module_id", 
        "test_device_client_id", 
        "test_module_id"; 
        "iot:module_id variable")]
    #[test_case("namespace-{{iot:module_id}}-suffix", 
        "test_device_id/test_module_id", 
        "test_device_client_id", 
        "namespace-test_module_id-suffix"; 
        "iot:module_id variable substring")]
    #[test_case("{{iot:this_device_id}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "edge_device"; 
        "iot:this_device_id variable")]
    #[test_case("namespace-{{iot:this_device_id}}-suffix", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "namespace-edge_device-suffix"; 
        "iot:this_device_id variable substring")]
    #[test_case("{{invalid}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "{{invalid}}"; 
        "invalid variable")]
    #[test_case("{{{}bad}}}", 
        "test_device_auth_id", 
        "test_device_client_id", 
        "{{{}bad}}}"; 
        "bad variable")]
    fn visit_resource_test(input: &str, auth_id: &str, client_id: &str, expected: &str) {
        let request = Request::with_context(
            "some_identity",
            "some_operation",
            "some_resource",
            tests::create_publish_activity(client_id, auth_id),
        )
        .unwrap();

        assert_eq!(
            expected,
            MqttSubstituter::new("edge_device")
                .visit_resource(input, &request)
                .unwrap()
        );
    }
}
