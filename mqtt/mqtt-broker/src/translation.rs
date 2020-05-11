#![allow(dead_code)]
#![allow(unused_imports)]

use crate::{ClientEvent, Publish};
use lazy_static::lazy_static;
use mqtt3::proto;
use regex::Regex;

lazy_static! {
    pub static ref TRANSLATOR: Translator = Translator::new();
}

pub struct Translator {
    incoming: TranslateIncoming,
    outgoing: TranslateOutgoing,
}

impl Translator {
    fn new() -> Self {
        Self {
            incoming: TranslateIncoming::new().unwrap(), //temp
            outgoing: TranslateOutgoing::new().unwrap(),
        }
    }

    pub fn translate_incoming(&self, client_id: &str, event: ClientEvent) -> ClientEvent {
        match event {
            ClientEvent::Subscribe(s) => ClientEvent::Subscribe(self.subscribe(client_id, s)),
            ClientEvent::Unsubscribe(u) => ClientEvent::Unsubscribe(self.unsubscribe(client_id, u)),
            ClientEvent::PublishFrom(p) => {
                ClientEvent::PublishFrom(self.publish_incoming(client_id, p))
            }
            e => e,
        }
    }

    // note pubAcks don't need to be translated since they only contain an
    // id, not a lits of topics subbed to
    pub fn translate_outgoing(&self, event: ClientEvent) -> ClientEvent {
        match event {
            ClientEvent::PublishTo(Publish::QoS0(pid, p)) => {
                ClientEvent::PublishTo(Publish::QoS0(pid, self.publish_outgoing(p)))
            }
            ClientEvent::PublishTo(Publish::QoS12(pid, p)) => {
                ClientEvent::PublishTo(Publish::QoS12(pid, self.publish_outgoing(p)))
            }

            p => p,
        }
    }

    fn subscribe(&self, client_id: &str, subscribe: proto::Subscribe) -> proto::Subscribe {
        let proto::Subscribe {
            packet_identifier,
            mut subscribe_to,
        } = subscribe;

        for mut sub_to in &mut subscribe_to {
            if let Some(new_topic) = self
                .incoming
                .translate_incoming(&sub_to.topic_filter, client_id)
            {
                sub_to.topic_filter = new_topic;
            }
        }

        proto::Subscribe {
            packet_identifier,
            subscribe_to,
        }
    }

    fn unsubscribe(
        &self,
        client_id: &str,
        mut unsubscribe: proto::Unsubscribe,
    ) -> proto::Unsubscribe {
        unsubscribe.unsubscribe_from = unsubscribe
            .unsubscribe_from
            .drain(..)
            .map(|t| self.incoming.translate_incoming(&t, client_id).unwrap_or(t))
            .collect();

        unsubscribe
    }

    fn publish_incoming(&self, client_id: &str, mut publish: proto::Publish) -> proto::Publish {
        if let Some(new_topic) = self
            .incoming
            .translate_incoming(&publish.topic_name, client_id)
        {
            publish.topic_name = new_topic;
        }
        publish
    }

    fn publish_outgoing(&self, mut publish: proto::Publish) -> proto::Publish {
        if let Some(new_topic) = self.outgoing.translate_outgoing(&publish.topic_name) {
            publish.topic_name = new_topic;
        }
        publish
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
    leaf_events {
        format!("devices/{}/messages/events", DEVICE_ID),
        {|_, client_id| format!("$edgehub/{}/messages/events", client_id)}
        // should it use client_id or &captures["device_id"] ?
    },
    module_events {
        format!("devices/{}/modules/(?P<module_id>.*)/messages/events", DEVICE_ID),
        {|_, client_id| format!("$edgehub/{}/messages/events", client_id)}
        // should it use client_id or &captures["device_id"]/modules/&captures["module_id"] ?
    },
    desired_properties {
        "\\$iothub/twin/PATCH/properties/desired",
        {|_, client_id| format!("$edgehub/{}/twin/desired", client_id)}
    },
    twin_get {
        "\\$iothub/twin/GET",
        {|_, client_id| format!("$edgehub/{}/twin/get", client_id)}
    },
    direct_method_response {
        "\\$iothub/methods/res/(?P<status>.*)",
        {|captures: regex::Captures<'_>, client_id| format!("$edgehub/{}/methods/res/{}", client_id, &captures["status"])}
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
    twin_response {
        format!("\\$edgehub/{}/twin/res(?P<params>.*)", DEVICE_ID),
        {|captures: regex::Captures<'_>| format!("$iothub/twin/res{}", &captures["params"])}
    },
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
        assert_eq!(
            translator.translate_outgoing("$edgehub/client_a/methods/post"),
            Some("$iothub/methods/POST/".to_owned())
        );
    }
}
