use std::collections::HashMap;

use serde::Deserialize;

use crate::{bridge::BridgeHandle, controller::Error, settings::Direction, settings::TopicRule};

pub struct ConfigUpdater {
    bridge_handle: BridgeHandle,
    current_subscriptions: HashMap<String, TopicRule>,
    current_forwards: HashMap<String, TopicRule>,
}

impl ConfigUpdater {
    pub fn new(bridge_handle: BridgeHandle) -> Self {
        Self {
            bridge_handle,
            current_subscriptions: HashMap::new(),
            current_forwards: HashMap::new(),
        }
    }

    pub fn diff(&self, bridge_update: &BridgeUpdate) -> BridgeDiff {
        let local_diff =
            Self::diff_topic_rules(bridge_update.clone().forwards(), &self.current_forwards);

        let remote_diff = Self::diff_topic_rules(
            bridge_update.clone().subscriptions(),
            &self.current_subscriptions,
        );

        BridgeDiff::default()
            .with_local_diff(local_diff)
            .with_remote_diff(remote_diff)
    }

    pub fn update(&mut self, bridge_diff: &BridgeDiff) {
        Self::update_pump(bridge_diff.local_updates(), &mut self.current_forwards);

        Self::update_pump(
            bridge_diff.remote_updates(),
            &mut self.current_subscriptions,
        )
    }

    pub async fn send(&mut self, message: BridgeDiff) -> Result<(), Error> {
        self.bridge_handle
            .send(message)
            .await
            .map_err(Error::SendBridgeMessage)
    }

    fn diff_topic_rules(updated: Vec<TopicRule>, current: &HashMap<String, TopicRule>) -> PumpDiff {
        let mut added = vec![];
        let mut removed = vec![];

        let subs_map = updated
            .iter()
            .map(|sub| (sub.subscribe_to(), sub.clone()))
            .collect::<HashMap<_, _>>();

        for sub in updated {
            if !current.contains_key(&sub.subscribe_to())
                || current
                    .get(&sub.subscribe_to())
                    .filter(|curr| curr.to_owned().eq(&sub))
                    == None
            {
                added.push(sub);
            }
        }

        for sub in current.keys() {
            if !subs_map.contains_key(sub) {
                if let Some(curr) = current.get(sub) {
                    removed.push(curr.to_owned())
                }
            }
        }

        PumpDiff::default().with_added(added).with_removed(removed)
    }

    fn update_pump(pump_diff: &PumpDiff, current: &mut HashMap<String, TopicRule>) {
        pump_diff.added().into_iter().for_each(|added| {
            current.insert(added.subscribe_to(), added.to_owned());
        });

        pump_diff.removed().iter().for_each(|updated| {
            current.remove(&updated.subscribe_to());
        });
    }
}

#[derive(Debug, Deserialize)]
pub struct BridgeControllerUpdate {
    bridge_updates: Vec<BridgeUpdate>,
}

impl BridgeControllerUpdate {
    pub fn from_bridge_topic_rules(name: &str, subs: &[TopicRule], forwards: &[TopicRule]) -> Self {
        let subscriptions = subs
            .iter()
            .map(|s| Direction::Out(s.to_owned()))
            .chain(forwards.iter().map(|s| Direction::In(s.to_owned())))
            .collect();

        let bridge_update = BridgeUpdate {
            endpoint: name.to_owned(),
            subscriptions,
        };
        Self {
            bridge_updates: vec![bridge_update],
        }
    }

    pub fn bridge_updates(self) -> Vec<BridgeUpdate> {
        self.bridge_updates
    }
}

#[derive(Clone, Debug, PartialEq, Deserialize)]
pub struct BridgeUpdate {
    endpoint: String,
    subscriptions: Vec<Direction>,
}

impl BridgeUpdate {
    pub fn endpoint(&self) -> &str {
        self.endpoint.as_ref()
    }

    pub fn subscriptions(self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::In(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn forwards(self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::Out(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct BridgeDiff {
    local_pump_diff: PumpDiff,
    remote_pump_diff: PumpDiff,
}

impl BridgeDiff {
    pub fn with_local_diff(mut self, diff: PumpDiff) -> Self {
        self.local_pump_diff = diff;
        self
    }

    pub fn with_remote_diff(mut self, diff: PumpDiff) -> Self {
        self.remote_pump_diff = diff;
        self
    }

    pub fn has_local_updates(&self) -> bool {
        self.local_pump_diff.has_updates()
    }

    pub fn has_remote_updates(&self) -> bool {
        self.remote_pump_diff.has_updates()
    }

    pub fn local_updates(&self) -> &PumpDiff {
        &self.local_pump_diff
    }

    pub fn remote_updates(&self) -> &PumpDiff {
        &self.remote_pump_diff
    }
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct PumpDiff {
    added: Vec<TopicRule>,
    removed: Vec<TopicRule>,
}

impl PumpDiff {
    pub fn with_added(mut self, added: Vec<TopicRule>) -> Self {
        self.added = added;
        self
    }

    pub fn with_removed(mut self, removed: Vec<TopicRule>) -> Self {
        self.removed = removed;
        self
    }

    pub fn added(&self) -> Vec<&TopicRule> {
        self.added.iter().collect::<Vec<_>>()
    }

    pub fn removed(&self) -> Vec<&TopicRule> {
        self.removed.iter().collect::<Vec<_>>()
    }

    pub fn has_updates(&self) -> bool {
        !(self.added.is_empty() && self.removed.is_empty())
    }
}
