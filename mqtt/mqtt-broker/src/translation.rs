use mqtt3::proto;

use crate::ClientEvent;

pub fn translate_incoming(client_id: &str, event: ClientEvent) -> ClientEvent {
    match event {
        ClientEvent::Subscribe(s) => ClientEvent::Subscribe(subscribe(client_id, s)),
        ClientEvent::Unsubscribe(u) => ClientEvent::Unsubscribe(unsubscribe(client_id, u)),
        ClientEvent::PublishFrom(p) => ClientEvent::PublishFrom(publish(client_id, p)),
        _ => event,
    }
}

fn subscribe(client_id: &str, subscribe: proto::Subscribe) -> proto::Subscribe {
    let proto::Subscribe {
        packet_identifier,
        mut subscribe_to,
    } = subscribe;

    for mut sub_to in &mut subscribe_to {
        sub_to.topic_filter = translate_sub_topics(sub_to.topic_filter.clone(), client_id);
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
        .map(|t| translate_sub_topics(t, client_id))
        .collect();

    unsubscribe
}

fn translate_sub_topics(topic: String, client_id: &str) -> String {
    //TODO: make this cover all iothub topics
    if topic == "$iothub/twin/res" {
        format!("$edgehub/{}/twin/res", client_id)
    } else {
        topic
    }
}

fn publish(client_id: &str, mut subscribe: proto::Publish) -> proto::Publish {
    if subscribe.topic_name == "$iothub/twin/GET" {
        subscribe.topic_name = format!("$edgehub/{}/twin/get", client_id);
    }

    subscribe
}
