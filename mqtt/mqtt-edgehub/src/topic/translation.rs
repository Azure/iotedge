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
use mqtt_broker_core::ClientId;

lazy_static! {
    static ref TRANSLATE_D2C: TranslateD2C =
        TranslateD2C::new().expect("Invalid regex in tranlsation.");
    static ref TRANSLATE_C2D: TranslateC2D =
        TranslateC2D::new().expect("Invalid regex in tranlsation.");
}

pub fn translate_incoming_subscribe(
    client_id: &ClientId,
    mut subscribe: proto::Subscribe,
) -> proto::Subscribe {
    for mut sub_to in &mut subscribe.subscribe_to {
        if let Some(new_topic) = TRANSLATE_C2D.to_new_topic(&sub_to.topic_filter, client_id) {
            debug!(
                "Translating subscription {} to {}",
                sub_to.topic_filter, new_topic
            );
            sub_to.topic_filter = new_topic;
        }
    }

    subscribe
}

pub fn translate_incoming_unsubscribe(
    client_id: &ClientId,
    mut unsubscribe: proto::Unsubscribe,
) -> proto::Unsubscribe {
    for unsub_from in &mut unsubscribe.unsubscribe_from {
        if let Some(new_topic) = TRANSLATE_C2D.to_new_topic(&unsub_from, client_id) {
            *unsub_from = new_topic;
        }
    }

    unsubscribe
}

pub fn translate_incoming_publish(
    client_id: &ClientId,
    mut publish: proto::Publish,
) -> proto::Publish {
    if let Some(new_topic) = TRANSLATE_D2C.to_new_topic(&publish.topic_name, client_id) {
        debug!(
            "Translating incoming publication {} to {}",
            publish.topic_name, new_topic
        );
        publish.topic_name = new_topic;
    }

    publish
}

pub fn translate_outgoing_publish(mut publish: proto::Publish) -> proto::Publish {
    if let Some(new_topic) = TRANSLATE_C2D.to_old_topic(&publish.topic_name) {
        debug!(
            "Translating outgoing publication {} to {}",
            publish.topic_name, new_topic
        );
        publish.topic_name = new_topic;
    }

    publish
}

const DEVICE_ID: &str = r"(?P<device_id>.+)";
macro_rules! translate_d2c {
    ($(
        $translate_name:ident {
            to_new_topic { $new_from:expr, $new_to:expr }
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

            fn to_new_topic(&self, topic: &str, client_id: &ClientId) -> Option<String> {
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
            to_new_topic { $new_from:expr, $new_to:expr },
            to_old_topic { $old_from:expr, $old_to:expr }
        }
    ),*) => {
        struct TranslateC2D {
            to_new_topic: TranslateRegex,
            to_old_topic: TranslateRegex,
        }

        struct TranslateRegex {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateC2D {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    to_new_topic: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$new_from)?,
                        )*
                    },
                    to_old_topic: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$old_from)?,
                        )*
                    },
                })
            }

            fn to_new_topic(&self, topic: &str, client_id: &ClientId) -> Option<String> {
                if topic.starts_with("$iothub/") || topic.starts_with("devices/") {
                    $(
                        if let Some(captures) = self.to_new_topic.$translate_name.captures(topic) {
                            return Some($new_to(captures, client_id).into());
                        }
                    )*
                }

                None
            }

            fn to_old_topic(&self, topic: &str) -> Option<String> {
                if topic.starts_with("$edgehub/") {
                    $(
                        if let Some(captures) = self.to_old_topic.$translate_name.captures(topic) {
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
    device_send_message { // note this may have to be split into 2 patterns for device and modules, depending on how client_id encodes device and module id
        to_new_topic {
            format!("devices/{}/messages/events(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/events{}", &captures["device_id"], &captures["path"])}
        }
    },

    // Twin Translation
    twin_send_update_to_hub {
        to_new_topic {
            "\\$iothub/twin/PATCH/properties/reported/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/reported/{}", client_id, &captures["params"])}
        }
    },
    twin_get_from_hub {
        to_new_topic {
            "\\$iothub/twin/GET/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/get/{}", client_id, &captures["params"])}
        }
    },

    // Direct Methods
    direct_method_response {
        to_new_topic {
            "\\$iothub/methods/res/(?P<status>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/res/{}", client_id, &captures["status"])}
        }
    }
}

translate_c2d! {
    // Message Translation
    c2d_message {
        to_new_topic {
            format!("devices/{}/messages/devicebound(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/c2d/post{}", &captures["device_id"], &captures["path"])}
        },
        to_old_topic {
            format!("\\$edgehub/{}/messages/c2d/post(?P<path>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/messages/devicebound{}", &captures["device_id"], &captures["path"])}
        }
    },

    // Twin Translation
    twin_receive_update_from_hub {
        to_new_topic {
            "\\$iothub/twin/PATCH/properties/desired/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/desired/{}", client_id, &captures["params"])}
        },
        to_old_topic {
            format!("\\$edgehub/{}/twin/desired/(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/PATCH/properties/desired/{}", &captures["params"])}
        }
    },
    twin_response_from_hub {
        to_new_topic {
            "\\$iothub/twin/res/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/res/{}", client_id, &captures["params"])}
        },
        to_old_topic {
            format!("\\$edgehub/{}/twin/res/(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/res/{}", &captures["params"])}
        }
    },

    // Direct Methods
    receive_direct_method_request {
        to_new_topic {
            "\\$iothub/methods/POST/(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/post/{}", client_id, &captures["params"])}
        },
        to_old_topic {
            format!("\\$edgehub/{}/methods/post/(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/methods/POST/{}", &captures["params"])}
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{TranslateC2D, TranslateD2C};

    #[test]
    fn test_translater() {
        let d2c = TranslateD2C::new().unwrap();
        let c2d = TranslateC2D::new().unwrap();

        let client_a = "client_a".into();
        let device_1 = "device_1".into();

        // Noneranslate_t
        assert_eq!(d2c.to_new_topic("blagh", &client_a), None);
        assert_eq!(d2c.to_new_topic("$iothub/blagh", &client_a), None);

        // Messages d2c
        assert_eq!(
            d2c.to_new_topic("devices/device_1/messages/events", &device_1),
            Some("$edgehub/device_1/messages/events".to_owned())
        );
        assert_eq!(
            d2c.to_new_topic("devices/device_1/messages/events/route_1/input", &device_1),
            Some("$edgehub/device_1/messages/events/route_1/input".to_owned())
        );
        assert_eq!(
            d2c.to_new_topic(
                "devices/device_1/modules/client_a/messages/events",
                &client_a
            ),
            Some("$edgehub/device_1/modules/client_a/messages/events".to_owned())
        );

        // Messages c2d
        assert_eq!(
            c2d.to_new_topic("devices/device_1/messages/devicebound", &device_1),
            Some("$edgehub/device_1/messages/c2d/post".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/device_1/messages/c2d/post"),
            Some("devices/device_1/messages/devicebound".to_owned())
        );

        assert_eq!(
            c2d.to_new_topic(
                "devices/device_1/messages/devicebound/route_1/input",
                &device_1
            ),
            Some("$edgehub/device_1/messages/c2d/post/route_1/input".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/device_1/messages/c2d/post/route_1/input"),
            Some("devices/device_1/messages/devicebound/route_1/input".to_owned())
        );

        assert_eq!(
            c2d.to_new_topic(
                "devices/device_1/modules/client_a/messages/devicebound",
                &client_a
            ),
            Some("$edgehub/device_1/modules/client_a/messages/c2d/post".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/device_1/modules/client_a/messages/c2d/post",),
            Some("devices/device_1/modules/client_a/messages/devicebound".to_owned())
        );

        // Twin d2c
        assert_eq!(
            d2c.to_new_topic("$iothub/twin/PATCH/properties/reported/?rid=1", &client_a),
            Some("$edgehub/client_a/twin/reported/?rid=1".to_owned())
        );
        assert_eq!(
            d2c.to_new_topic("$iothub/twin/GET/?rid=2", &client_a),
            Some("$edgehub/client_a/twin/get/?rid=2".to_owned())
        );

        // Twin c2d
        assert_eq!(
            c2d.to_new_topic("$iothub/twin/PATCH/properties/desired/?rid=1", &client_a),
            Some("$edgehub/client_a/twin/desired/?rid=1".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/client_a/twin/desired/?rid=1"),
            Some("$iothub/twin/PATCH/properties/desired/?rid=1".to_owned())
        );

        assert_eq!(
            c2d.to_new_topic("$iothub/twin/res/#", &client_a),
            Some("$edgehub/client_a/twin/res/#".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/client_a/twin/res/?rid=3"),
            Some("$iothub/twin/res/?rid=3".to_owned())
        );

        // Direct Method d2c
        assert_eq!(
            d2c.to_new_topic("$iothub/methods/res/200/?rid=4", &client_a),
            Some("$edgehub/client_a/methods/res/200/?rid=4".to_owned())
        );

        // Direct Method c2d
        assert_eq!(
            c2d.to_new_topic("$iothub/methods/POST/#", &client_a),
            Some("$edgehub/client_a/methods/post/#".to_owned())
        );
        assert_eq!(
            c2d.to_old_topic("$edgehub/client_a/methods/post/my_method/?rid=5"),
            Some("$iothub/methods/POST/my_method/?rid=5".to_owned())
        );
    }
}
