#![allow(dead_code)]
#![allow(unused_imports)]

use crate::{ClientEvent, Publish};
use lazy_static::lazy_static;
use mqtt3::proto;
use regex::Regex;

pub fn translate_incoming(client_id: &str, event: ClientEvent) -> ClientEvent {
    match event {
        ClientEvent::Subscribe(s) => ClientEvent::Subscribe(subscribe(client_id, s)),
        ClientEvent::Unsubscribe(u) => ClientEvent::Unsubscribe(unsubscribe(client_id, u)),
        ClientEvent::PublishFrom(p) => ClientEvent::PublishFrom(publish_from(client_id, p)),
        e => e,
    }
}

pub fn translate_outgoing(event: ClientEvent) -> ClientEvent {
    match event {
        ClientEvent::PublishTo(Publish::QoS0(pid, p)) => {
            ClientEvent::PublishTo(Publish::QoS0(pid, publish_to(p)))
        }
        ClientEvent::PublishTo(Publish::QoS12(pid, p)) => {
            ClientEvent::PublishTo(Publish::QoS12(pid, publish_to(p)))
        }

        p => p,
    }
}

fn subscribe(client_id: &str, subscribe: proto::Subscribe) -> proto::Subscribe {
    let proto::Subscribe {
        packet_identifier,
        mut subscribe_to,
    } = subscribe;

    for mut sub_to in &mut subscribe_to {
        sub_to.topic_filter = to_new_topic(client_id, sub_to.topic_filter.clone());
    }

    proto::Subscribe {
        packet_identifier,
        subscribe_to,
    }
}

fn unsubscribe(client_id: &str, mut unsubscribe: proto::Unsubscribe) -> proto::Unsubscribe {
    unsubscribe.unsubscribe_from = unsubscribe
        .unsubscribe_from
        .into_iter()
        .map(|t| to_new_topic(client_id, t))
        .collect();

    unsubscribe
}

fn publish_from(client_id: &str, mut publish: proto::Publish) -> proto::Publish {
    publish.topic_name = to_new_topic(client_id, publish.topic_name);

    publish
}

fn publish_to(mut publish: proto::Publish) -> proto::Publish {
    publish.topic_name = to_legacy_topic(publish.topic_name);

    publish
}

fn to_new_topic(client_id: &str, topic: String) -> String {
    //TODO: make this cover all iothub topics
    match topic.as_ref() {
        "$iothub/twin/res" => format!("$edgehub/{}/twin/res", client_id),
        "$iothub/twin/GET" => format!("$edgehub/{}/twin/get", client_id),
        _ => topic,
    }
}

fn to_legacy_topic(topic: String) -> String {
    const DEVICE_ID: &str = r"[a-zA-Z-\.\+%_#\*\?!\(\),=@\$']";
    lazy_static! {
        static ref TWIN_PATTERN: Regex = Regex::new(&format!("\\$edgehub/{}/twin/get", DEVICE_ID))
            .expect("failed to create new Regex from pattern");
    }

    if TWIN_PATTERN.is_match(&topic) {
        "$iothub/twin/GET".to_owned()
    } else {
        topic
    }
}

const DEVICE_ID: &str = r"(?P<device_id>[a-zA-Z-\.\+%_#\*\?!\(\),=@\$']*)";
macro_rules! translate_incoming {
    ($(  $translate_name:ident { $translate_from:expr, $translate_to:expr } ),*) => {
        struct TranslateIncoming {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateIncoming {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    $(
                        $translate_name: Regex::new(&$translate_from)?,
                    )*
                })
            }

            fn translate_incoming(&self, topic: &str, client_id: &str) -> Option<String> {
                if topic.starts_with("$iothub") || topic.starts_with("devices") {
                    $(
                        if let Some(captures) = self.$translate_name.captures(topic) {
                            return Some($translate_to(captures, client_id).into());
                        }
                    )*
                }

                None
            }
        }
    };
}

macro_rules! translate_outgoing {
    ($( $translate_name:ident { $translate_from:expr, $translate_to:expr } ),*) => {
        struct TranslateOutgoing {
            $(
                $translate_name: Regex,
            )*
        }

        impl TranslateOutgoing {
            fn new() -> Result<Self, regex::Error> {
                Ok(Self {
                    $(
                        $translate_name: Regex::new(&$translate_from)?,
                    )*
                })
            }

            fn translate_outgoing(&self, topic: &str) -> Option<String> {
                if topic.starts_with("$edgehub") {
                    $(
                        if let Some(captures) = self.$translate_name.captures(topic) {
                            return Some($translate_to(captures).into());
                        }
                    )*
                }

                None
            }
        }
    };
}

translate_incoming! {
    desired_properties {
        format!("\\$iothub/twin/PATCH/properties/desired"),
        {|_, client_id| format!("$edgehub/{}/twin/desired", client_id)}
    }
}

translate_outgoing! {
    c2d_message {
        format!("\\$edgehub/{}/messages/c2d/post", DEVICE_ID),
        {|captures: regex::Captures<'_>| format!("devices/{}/messages/devicebound", &captures["device_id"])}
    },
    twin_reported {
        format!("\\$edgehub/{}/twin/reported(?P<params>.*)", DEVICE_ID),
        {|captures: regex::Captures<'_>| format!("$iothub/twin/PATCH/properties/reported{}", &captures["params"])}
    },
    twin_res {
        format!("\\$edgehub/{}/twin/res(?P<params>.*)", DEVICE_ID),
        {|captures: regex::Captures<'_>| format!("$iothub/twin/res{}", &captures["params"])}
    },
    // twin_get, format!("\\$edgehub/{}/twin/get(?P<request_id>.*)", DEVICE_ID), {|captures: regex::Captures<'_>| format!("$iothub/twin/GET{}", &captures["request_id"])},
    direct_method_request {
        format!("\\$edgehub/{}/methods/post", DEVICE_ID),
        {|_| "$iothub/methods/POST/"}
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    #[test]
    fn test_translate_incoming() {
        let translator = TranslateIncoming::new().unwrap();
        assert_eq!(translator.translate_incoming("blagh", "client_a"), None);
        assert_eq!(
            translator.translate_incoming("$iothub/not_a_topic", "client_a"),
            None
        );
        assert_eq!(
            translator.translate_incoming("$iothub/twin/PATCH/properties/desired", "client_a"),
            Some("$edgehub/client_a/twin/desired".to_owned())
        );
    }

    #[test]
    fn test_translate_outgoing() {
        let translator = TranslateOutgoing::new().unwrap();
        assert_eq!(translator.translate_outgoing("blagh"), None);
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/not_a_topic"),
            None
        );
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/messages/c2d/post"),
            Some("devices/client_a/messages/devicebound".to_owned())
        );
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/twin/reported?res=1234"),
            Some("$iothub/twin/PATCH/properties/reported?res=1234".to_owned())
        );
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/twin/res?res=1234"),
            Some("$iothub/twin/res?res=1234".to_owned())
        );
        // assert_eq!(
        //     translator.translate_outgoing("$edgehub/client_a/twin/get?res=1234"),
        //     Some("$iothub/twin/GET?res=1234".to_owned())
        // );
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/methods/post"),
            Some("$iothub/methods/POST/".to_owned())
        );
    }
}
