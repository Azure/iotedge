use lazy_static::lazy_static;
use mqtt3::proto;
use regex::Regex;

lazy_static! {
    pub static ref TRANSLATOR: Translator = Translator::new();
}

pub struct Translator {
    helper: TranslateHelper,
}

impl Translator {
    fn new() -> Self {
        Self {
            helper: TranslateHelper::new().unwrap(), //temp
        }
    }

    pub fn incoming_subscribe(
        &self,
        client_id: &str,
        mut subscribe: proto::Subscribe,
    ) -> proto::Subscribe {
        for mut sub_to in &mut subscribe.subscribe_to {
            if let Some(new_topic) = self
                .helper
                .translate_incoming(&sub_to.topic_filter, client_id)
            {
                println!(
                    "Translating subscription {} to {}",
                    sub_to.topic_filter, new_topic
                );
                sub_to.topic_filter = new_topic;
            }
        }

        subscribe
    }

    pub fn incoming_unsubscribe(
        &self,
        client_id: &str,
        mut unsubscribe: proto::Unsubscribe,
    ) -> proto::Unsubscribe {
        for unsub_from in &mut unsubscribe.unsubscribe_from {
            if let Some(new_topic) = self.helper.translate_incoming(&unsub_from, client_id) {
                *unsub_from = new_topic;
            }
        }

        unsubscribe
    }

    pub fn incoming_publish(&self, client_id: &str, mut publish: proto::Publish) -> proto::Publish {
        if let Some(new_topic) = self
            .helper
            .translate_incoming(&publish.topic_name, client_id)
        {
            println!(
                "Translating incoming publication {} to {}",
                publish.topic_name, new_topic
            );
            publish.topic_name = new_topic;
        }

        publish
    }

    pub fn outgoing_publish(&self, mut publish: proto::Publish) -> proto::Publish {
        if let Some(new_topic) = self.helper.translate_outgoing(&publish.topic_name) {
            println!(
                "Translating outgoing publication {} to {}",
                publish.topic_name, new_topic
            );
            publish.topic_name = new_topic;
        }

        publish
    }
}

const DEVICE_ID: &str = r"(?P<device_id>.*)";
macro_rules! translate {
    ($(
        $translate_name:ident {
            to_new { $new_from:expr, $new_to:expr },
            to_old { $old_from:expr, $old_to:expr }
        }
    ),*) => {
        struct TranslateHelper {
            to_new: TranslateRegex,
            to_old: TranslateRegex,
        }

        struct TranslateRegex {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateHelper {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    to_new: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$new_from)?,
                        )*
                    },
                    to_old: TranslateRegex {
                        $(
                            $translate_name: Regex::new(&$old_from)?,
                        )*
                    },
                })
            }

            fn translate_incoming(&self, topic: &str, client_id: &str) -> Option<String> {
                if topic.starts_with("$iothub") || topic.starts_with("devices") {
                    $(
                        if let Some(captures) = self.to_new.$translate_name.captures(topic) {
                            return Some($new_to(captures, client_id).into());
                        }
                    )*
                }

                None
            }

            fn translate_outgoing(&self, topic: &str) -> Option<String> {
                if topic.starts_with("$edgehub") {
                    $(
                        if let Some(captures) = self.to_old.$translate_name.captures(topic) {
                            return Some($old_to(captures).into());
                        }
                    )*
                }

                None
            }
        }
    };
}

translate! {
    // Message Translation
    events { // note this may have to be split into 2 patterns for device and modules, depending on how client_id encodes device and module id
        to_new {
            format!("devices/{}/messages/events", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/events", &captures["device_id"])}
        },
        to_old {
            format!("\\$edgehub/{}/messages/events", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/messages/events", &captures["device_id"])}
        }
    },
    c2d_message {
        to_new {
            format!("devices/{}/messages/devicebound", DEVICE_ID),
            {|captures: regex::Captures<'_>, _| format!("$edgehub/{}/messages/c2d/post", &captures["device_id"])}
        },
        to_old {
            format!("\\$edgehub/{}/messages/c2d/post", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("devices/{}/messages/devicebound", &captures["device_id"])}
        }
    },

    // Twin translation
    twin_desired {
        to_new {
            "\\$iothub/twin/PATCH/properties/desired(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/desired{}", client_id, &captures["params"])}
        },
        to_old {
            format!("\\$edgehub/{}/twin/desired(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/PATCH/properties/desired{}", &captures["params"])}
        }
    },
    twin_reported {
        to_new {
            "\\$iothub/twin/PATCH/properties/reported(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/reported{}", client_id, &captures["params"])}
        },
        to_old {
            format!("\\$edgehub/{}/twin/reported(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/PATCH/properties/reported{}", &captures["params"])}
        }
    },
    twin_get {
        to_new {
            "\\$iothub/twin/GET(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/get{}", client_id, &captures["params"])}
        },
        to_old {
            format!("\\$edgehub/{}/twin/get(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/GET{}", &captures["params"])}
        }
    },
    twin_response {
        to_new {
            "\\$iothub/twin/res(?P<params>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/twin/get{}", client_id, &captures["params"])}
        },
        to_old {
            format!("\\$edgehub/{}/twin/res(?P<params>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/twin/res{}", &captures["params"])}
        }
    },

    // Direct Methods
    direct_method_response {
        to_new {
            "\\$iothub/methods/res/(?P<status>.*)",
            {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/res/{}", client_id, &captures["status"])}
        },
        to_old {
            format!("\\$edgehub/{}/methods/res/(?P<status>.*)", DEVICE_ID),
            {|captures: regex::Captures<'_>| format!("$iothub/methods/res/{}", &captures["status"])}
        }
    },
    direct_method_request {
        to_new {
            "\\$iothub/methods/POST/",
            {|_, client_id| format!("$edgehub/{}/methods/post", client_id)}
        },
        to_old {
            format!("\\$edgehub/{}/methods/post", DEVICE_ID),
            {|_| "$iothub/methods/POST/"}
        }
    }
}

// #[cfg(test)]
// pub(crate) mod tests {
//     use super::*;
//     #[test]
//     fn test_translate_incoming() {
//         let translator = TranslateIncoming::new().unwrap();
//         assert_eq!(translator.translate_incoming("blagh", "client_a"), None);
//         assert_eq!(
//             translator.translate_incoming("$iothub/not_a_topic", "client_a"),
//             None
//         );
//         assert_eq!(
//             translator.translate_incoming("$iothub/twin/PATCH/properties/desired", "client_a"),
//             Some("$edgehub/client_a/twin/desired".to_owned())
//         );
//     }

//     #[test]
//     fn test_translate_outgoing() {
//         let translator = TranslateOutgoing::new().unwrap();
//         assert_eq!(translator.translate_outgoing("blagh"), None);
//         assert_eq!(
//             translator.translate_outgoing("$edgehub/client_a/not_a_topic"),
//             None
//         );
//         assert_eq!(
//             translator.translate_outgoing("$edgehub/client_a/messages/c2d/post"),
//             Some("devices/client_a/messages/devicebound".to_owned())
//         );
//         assert_eq!(
//             translator.translate_outgoing("$edgehub/client_a/twin/reported?res=1234"),
//             Some("$iothub/twin/PATCH/properties/reported?res=1234".to_owned())
//         );
//         assert_eq!(
//             translator.translate_outgoing("$edgehub/client_a/twin/res?res=1234"),
//             Some("$iothub/twin/res?res=1234".to_owned())
//         );
//         assert_eq!(
//             translator.translate_outgoing("$edgehub/client_a/methods/post"),
//             Some("$iothub/methods/POST/".to_owned())
//         );
//     }
// }
