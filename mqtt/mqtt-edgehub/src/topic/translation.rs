//! This module translates between the old sdk iothub topics which lack a client id
//! into newly designed topics that include a client id.
//!
//! `IoTHub` currently relies on the connection information to identify a device.
//! This does not work well in he broker message since edgehub core doesn't hold the connection to the device/module.
//!
//! This translation allows edgehub core to subscribe to the new topics that include client id to identify requests
use lazy_static::lazy_static;
use regex::Regex;
use tracing::debug;

use mqtt3::proto;
use mqtt_broker::ClientId;

lazy_static! {
    static ref TRANSLATE_D2C: TranslateD2C =
        TranslateD2C::new().expect("Invalid regex in tranlsation.");
    static ref TRANSLATE_C2D: TranslateC2D =
        TranslateC2D::new().expect("Invalid regex in tranlsation.");
}

pub fn translate_incoming_subscribe(client_id: &ClientId, subscribe: &mut proto::Subscribe) {
    for mut sub_to in &mut subscribe.subscribe_to {
        if let Some(new_topic) = TRANSLATE_C2D.to_internal(&sub_to.topic_filter, client_id) {
            debug!(
                "Translating subscription {} to {}",
                sub_to.topic_filter, new_topic
            );
            sub_to.topic_filter = new_topic;
        }
    }
}

pub fn translate_incoming_unsubscribe(client_id: &ClientId, unsubscribe: &mut proto::Unsubscribe) {
    for unsub_from in &mut unsubscribe.unsubscribe_from {
        if let Some(new_topic) = TRANSLATE_C2D.to_internal(unsub_from, client_id) {
            *unsub_from = new_topic;
        }
    }
}

pub fn translate_incoming_publish(client_id: &ClientId, publish: &mut proto::Publish) {
    if let Some(new_topic) = TRANSLATE_D2C.to_internal(&publish.topic_name, client_id) {
        debug!(
            "Translating incoming publication {} to {}",
            publish.topic_name, new_topic
        );
        publish.topic_name = new_topic;
    }
}

pub fn translate_outgoing_publish(publish: &mut proto::Publish) {
    if let Some(new_topic) = TRANSLATE_C2D.to_external(&publish.topic_name) {
        debug!(
            "Translating outgoing publication {} to {}",
            publish.topic_name, new_topic
        );
        publish.topic_name = new_topic;
    }
}

const DEVICE_ID: &str = r"(?P<device_id>[^/]+)";

const MODULE_ID: &str = r"(?P<module_id>[^/]+)";

const DEVICE_OR_MODULE_ID: &str = r"(?P<device_id>[^/]+)(/(?P<module_id>[^/]+))?";

macro_rules! translate_d2c {
    ($(
        $translate_name:ident {
            to_internal { $new_from:expr, $new_to:expr }
        }
    ),*) => {
        struct TranslateD2C {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateD2C {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    $(
                        $translate_name: Regex::new(&$new_from)?,
                    )*
                })
            }

            fn to_internal(&self, topic: &str, client_id: &ClientId) -> Option<String> {
                if topic.starts_with("$iothub") || topic.starts_with("devices") {
                    $(
                        if let Some(captures) = self.$translate_name.captures(topic) {
                            return Some($new_to(captures, client_id).into());
                        }
                    )*
                }

                None
            }
        }
    };
}

macro_rules! translate_c2d {
    ($(
        $translate_name:ident {
            to_internal { $new_from:expr, $new_to:expr },
            to_external { $old_from:expr, $old_to:expr }
        }
    ),*) => {
        struct TranslateC2D {
            to_internal: TranslateRegex,
            to_external: TranslateRegex,
        }

        struct TranslateRegex {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateC2D {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    to_internal: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$new_from)?,
                        )*
                    },
                    to_external: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$old_from)?,
                        )*
                    },
                })
            }

            fn to_internal(&self, topic: &str, client_id: &ClientId) -> Option<String> {
                if topic.starts_with("$iothub/") || topic.starts_with("devices/") {
                    $(
                        if let Some(captures) = self.to_internal.$translate_name.captures(topic) {
                            return Some($new_to(captures, client_id).into());
                        }
                    )*
                }

                None
            }

            fn to_external(&self, topic: &str) -> Option<String> {
                if topic.starts_with("$edgehub/") {
                    $(
                        if let Some(captures) = self.to_external.$translate_name.captures(topic) {
                            return Some($old_to(captures).into());
                        }
                    )*
                }

                None
            }
        }
    };
}

translate_d2c! {
    // Message Translation
    module_send_message {
        to_internal {
            format!("devices/{}/modules/{}/messages/events(?P<path>.*)", DEVICE_ID, MODULE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/{}/messages/events{}", &captures["device_id"], &captures["module_id"], &captures["path"])}
        }
    },

    device_send_message { // note this may have to be split into 2 patterns for device and modules, depending on how client_id encodes device and module id
        to_internal {
            format!("devices/{}/messages/events(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/events{}", &captures["device_id"], &captures["path"])}
        }
    },

    // Twin Translation
    twin_send_update_to_hub {
        to_internal {
            "\\$iothub/twin/PATCH/properties/reported/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/reported/{}", client_id, &captures["params"])}
        }
    },
    twin_get_from_hub {
        to_internal {
            "\\$iothub/twin/GET/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/get/{}", client_id, &captures["params"])}
        }
    },

    // Direct Methods
    direct_method_response {
        to_internal {
            "\\$iothub/methods/res/(?P<status>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/res/{}", client_id, &captures["status"])}
        }
    }
}

translate_c2d! {
    // Message Translation
    module_c2d_message {
        to_internal {
            format!("devices/{}/modules/{}/messages/devicebound(?P<path>.*)", DEVICE_ID, MODULE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/{}/messages/c2d/post{}", &captures["device_id"], &captures["module_id"], &captures["path"])}
        },
        to_external {
            format!("\\$edgehub/{}/{}/messages/c2d/post(?P<path>.*)", DEVICE_ID, MODULE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/modules/{}/messages/devicebound{}", &captures["device_id"], &captures["module_id"], &captures["path"])}
        }
    },

    c2d_message {
        to_internal {
            format!("devices/{}/messages/devicebound(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/c2d/post{}", &captures["device_id"], &captures["path"])}
        },
        to_external {
            format!("\\$edgehub/{}/messages/c2d/post(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/messages/devicebound{}", &captures["device_id"], &captures["path"])}
        }
    },

    // Twin Translation
    twin_receive_update_from_hub {
        to_internal {
            "\\$iothub/twin/PATCH/properties/desired/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/desired/{}", client_id, &captures["params"])}
        },
        to_external {
            format!("\\$edgehub/{}/twin/desired/(?P<params>.*)", DEVICE_OR_MODULE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/PATCH/properties/desired/{}", &captures["params"])}
        }
    },
    twin_response_from_hub {
        to_internal {
            "\\$iothub/twin/res/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/res/{}", client_id, &captures["params"])}
        },
        to_external {
            format!("\\$edgehub/{}/twin/res/(?P<params>.*)", DEVICE_OR_MODULE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/res/{}", &captures["params"])}
        }
    },

    // Direct Methods
    receive_direct_method_request {
        to_internal {
            "\\$iothub/methods/POST/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/post/{}", client_id, &captures["params"])}
        },
        to_external {
            format!("\\$edgehub/{}/methods/post/(?P<params>.*)", DEVICE_OR_MODULE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/methods/POST/{}", &captures["params"])}
        }
    },

    // Module-to-Module inputs
    module_to_module_inputs {
        to_internal {
            format!("devices/{}/modules/{}/(#|inputs/.*)", DEVICE_ID, MODULE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/{}/+/inputs/#", &captures["device_id"], &captures["module_id"])}
        },
        to_external {
            format!("\\$edgehub/{}/{}/[^/]+/inputs/(?P<path>.+)", DEVICE_ID, MODULE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/modules/{}/inputs/{}", &captures["device_id"], &captures["module_id"], &captures["path"])}
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{TranslateC2D, TranslateD2C};

    #[test]
    fn it_doesnt_translate() {
        let d2c = TranslateD2C::new().unwrap();

        let client_id = "client_a".into();

        assert_eq!(d2c.to_internal("blagh", &client_id), None);
        assert_eq!(d2c.to_internal("$iothub/blagh", &client_id), None);
        assert_eq!(
            d2c.to_internal("devices/another/device_1/messages/events", &client_id),
            None
        );
        assert_eq!(
            d2c.to_internal(
                "devices/device_1/modules/another/module-a/messages/devicebound/",
                &client_id
            ),
            None
        );
        assert_eq!(
            d2c.to_internal("devices/device_1/inputs/route_1", &client_id),
            None
        );
        assert_eq!(
            d2c.to_internal("devices/device_1/outputs/route_1", &client_id),
            None
        );
    }

    #[test]
    fn it_translates_for_device() {
        let d2c = TranslateD2C::new().unwrap();
        let c2d = TranslateC2D::new().unwrap();

        let device_1 = "device_1".into();

        // Messages d2c
        assert_eq!(
            d2c.to_internal("devices/device_1/messages/events", &device_1),
            Some("$edgehub/device_1/messages/events".to_owned())
        );
        assert_eq!(
            d2c.to_internal("devices/device_1/messages/events/route_1/input", &device_1),
            Some("$edgehub/device_1/messages/events/route_1/input".to_owned())
        );

        // Messages c2d
        assert_eq!(
            c2d.to_internal("devices/device_1/messages/devicebound", &device_1),
            Some("$edgehub/device_1/messages/c2d/post".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/messages/c2d/post"),
            Some("devices/device_1/messages/devicebound".to_owned())
        );

        assert_eq!(
            c2d.to_internal(
                "devices/device_1/messages/devicebound/route_1/input",
                &device_1
            ),
            Some("$edgehub/device_1/messages/c2d/post/route_1/input".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/messages/c2d/post/route_1/input"),
            Some("devices/device_1/messages/devicebound/route_1/input".to_owned())
        );

        assert_eq!(
            c2d.to_external("$edgehub/device_1/module_a/messages/c2d/post",),
            Some("devices/device_1/modules/module_a/messages/devicebound".to_owned())
        );

        // Twin d2c
        assert_eq!(
            d2c.to_internal("$iothub/twin/PATCH/properties/reported/?rid=1", &device_1),
            Some("$edgehub/device_1/twin/reported/?rid=1".to_owned())
        );
        assert_eq!(
            d2c.to_internal("$iothub/twin/GET/?rid=2", &device_1),
            Some("$edgehub/device_1/twin/get/?rid=2".to_owned())
        );

        // Twin c2d
        assert_eq!(
            c2d.to_internal("$iothub/twin/PATCH/properties/desired/?rid=1", &device_1),
            Some("$edgehub/device_1/twin/desired/?rid=1".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/module_a/twin/desired/?rid=1"),
            Some("$iothub/twin/PATCH/properties/desired/?rid=1".to_owned())
        );

        assert_eq!(
            c2d.to_internal("$iothub/twin/res/#", &device_1),
            Some("$edgehub/device_1/twin/res/#".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/module_a/twin/res/?rid=3"),
            Some("$iothub/twin/res/?rid=3".to_owned())
        );

        // Direct Method d2c
        assert_eq!(
            d2c.to_internal("$iothub/methods/res/200/?rid=4", &device_1),
            Some("$edgehub/device_1/methods/res/200/?rid=4".to_owned())
        );

        // Direct Method c2d
        assert_eq!(
            c2d.to_internal("$iothub/methods/POST/#", &device_1),
            Some("$edgehub/device_1/methods/post/#".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/methods/post/my_method/?rid=5"),
            Some("$iothub/methods/POST/my_method/?rid=5".to_owned())
        );
    }

    #[test]
    #[allow(clippy::too_many_lines)]
    fn it_translates_for_module() {
        let d2c = TranslateD2C::new().unwrap();
        let c2d = TranslateC2D::new().unwrap();

        let client_id = "device_1/module_a".into();

        // Messages d2c
        assert_eq!(
            d2c.to_internal(
                "devices/device_1/modules/module_a/messages/events",
                &client_id
            ),
            Some("$edgehub/device_1/module_a/messages/events".to_owned())
        );
        assert_eq!(
            d2c.to_internal(
                "devices/device_1/modules/module_a/messages/events/route_1/input",
                &client_id
            ),
            Some("$edgehub/device_1/module_a/messages/events/route_1/input".to_owned())
        );
        assert_eq!(
            d2c.to_internal(
                "devices/device_1/modules/module_a/messages/events",
                &client_id
            ),
            Some("$edgehub/device_1/module_a/messages/events".to_owned())
        );

        // Messages c2d
        assert_eq!(
            c2d.to_internal("devices/device_1/messages/devicebound", &client_id),
            Some("$edgehub/device_1/messages/c2d/post".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/messages/c2d/post"),
            Some("devices/device_1/messages/devicebound".to_owned())
        );

        assert_eq!(
            c2d.to_internal(
                "devices/device_1/messages/devicebound/route_1/input",
                &client_id
            ),
            Some("$edgehub/device_1/messages/c2d/post/route_1/input".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/messages/c2d/post/route_1/input"),
            Some("devices/device_1/messages/devicebound/route_1/input".to_owned())
        );

        assert_eq!(
            c2d.to_internal(
                "devices/device_1/modules/module_a/messages/devicebound",
                &client_id
            ),
            Some("$edgehub/device_1/module_a/messages/c2d/post".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/module_a/messages/c2d/post",),
            Some("devices/device_1/modules/module_a/messages/devicebound".to_owned())
        );

        // Twin d2c
        assert_eq!(
            d2c.to_internal("$iothub/twin/PATCH/properties/reported/?rid=1", &client_id),
            Some("$edgehub/device_1/module_a/twin/reported/?rid=1".to_owned())
        );
        assert_eq!(
            d2c.to_internal("$iothub/twin/GET/?rid=2", &client_id),
            Some("$edgehub/device_1/module_a/twin/get/?rid=2".to_owned())
        );

        // Twin c2d
        assert_eq!(
            c2d.to_internal("$iothub/twin/PATCH/properties/desired/?rid=1", &client_id),
            Some("$edgehub/device_1/module_a/twin/desired/?rid=1".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/module_a/twin/desired/?rid=1"),
            Some("$iothub/twin/PATCH/properties/desired/?rid=1".to_owned())
        );

        assert_eq!(
            c2d.to_internal("$iothub/twin/res/#", &client_id),
            Some("$edgehub/device_1/module_a/twin/res/#".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/module_a/twin/res/?rid=3"),
            Some("$iothub/twin/res/?rid=3".to_owned())
        );

        // Direct Method d2c
        assert_eq!(
            d2c.to_internal("$iothub/methods/res/200/?rid=4", &client_id),
            Some("$edgehub/device_1/module_a/methods/res/200/?rid=4".to_owned())
        );

        // Direct Method c2d
        assert_eq!(
            c2d.to_internal("$iothub/methods/POST/#", &client_id),
            Some("$edgehub/device_1/module_a/methods/post/#".to_owned())
        );
        assert_eq!(
            c2d.to_external("$edgehub/device_1/module_a/methods/post/my_method/?rid=5"),
            Some("$iothub/methods/POST/my_method/?rid=5".to_owned())
        );

        // M2M subscription
        assert_eq!(
            c2d.to_internal("devices/device_1/modules/module_a/#", &client_id),
            Some("$edgehub/device_1/module_a/+/inputs/#".to_owned())
        );

        assert_eq!(
            c2d.to_internal("devices/device_1/modules/module_a/inputs/#", &client_id),
            Some("$edgehub/device_1/module_a/+/inputs/#".to_owned())
        );

        assert_eq!(
            c2d.to_internal(
                "devices/device_1/modules/module_a/inputs/route_1/#",
                &client_id
            ),
            Some("$edgehub/device_1/module_a/+/inputs/#".to_owned())
        );

        assert_eq!(
            c2d.to_internal("devices/device_1/modules/module_a/foo/#", &client_id),
            None
        );

        // M2M incoming
        assert_eq!(
            c2d.to_external(
                "$edgehub/device_1/module_a/b9aa0940-dcf2-457f-83a4-45f4c7ceecf9/inputs/route_1/%24.cdid=device_1&%24.cmid=module_a"
            ),
            Some(
                "devices/device_1/modules/module_a/inputs/route_1/%24.cdid=device_1&%24.cmid=module_a"
                    .to_owned()
            )
        );
    }
}
