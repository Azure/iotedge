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

const TRANSLATION_TOPIC: &str = "$edgehub";
const DEVICE_ID: &str = r"(?P<device_id>[a-zA-Z-\.\+%_#\*\?!\(\),=@\$']*)";
struct Translator {
    basic: Regex,
    twin_pattern: Regex,
}

impl Translator {
    fn new() -> Result<Self, regex::Error> {
        Ok(Self {
            basic: Regex::new(&format!("\\$edgehub/{}", DEVICE_ID))?,
            twin_pattern: Regex::new(&format!(
                "\\$edgehub/{}/twin/get(?P<request_id>.*)",
                DEVICE_ID
            ))?,
        })
    }

    fn translate_from(&self, topic: &str) -> Option<String> {
        if !topic.starts_with(TRANSLATION_TOPIC) {
            return None;
        }

        if let Some(captures) = self.basic.captures(topic) {
            println!("Device Id: {}", &captures["device_id"]);
        }

        if let Some(captures) = self.twin_pattern.captures(topic) {
            println!("Request Id: {}", &captures["request_id"]);
            return Some(format!("$iothub/twin/GET{}", &captures["request_id"]));
        }

        None
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    #[test]
    fn test_translator() {
        let translator = Translator::new().unwrap();
        assert_eq!(translator.translate_from("blagh"), None);
        assert_eq!(
            translator.translate_from("$edgehub/client_a/twin/get?res=1234"),
            Some("$iothub/twin/GET?res=1234".to_owned())
        );
    }
}
