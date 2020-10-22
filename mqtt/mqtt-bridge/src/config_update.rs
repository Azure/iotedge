#![allow(dead_code, unused_imports, unused_variables)] // TODO: remove when ready

use std::collections::HashMap;

use futures_util::future::join_all;
use serde::{Deserialize, Serialize};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedReceiver, UnboundedSender};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    bridge::Bridge, bridge::BridgeError, bridge::BridgeHandle, controller::Error,
    settings::BridgeSettings, settings::Direction, settings::TopicRule,
};

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
        let mut added_subs = vec![];
        let mut added_forwards = vec![];
        let mut removed_subs = vec![];
        let mut removed_forwards = vec![];
        let mut updated_subs = vec![];
        let mut updated_forwards = vec![];

        let subs_map = bridge_update
            .clone()
            .subscriptions()
            .iter()
            .map(|sub| (sub.subscribe_to(), sub.clone()))
            .collect::<HashMap<_, _>>();

        let forwards_map = bridge_update
            .clone()
            .forwards()
            .iter()
            .map(|sub| (sub.subscribe_to(), sub.clone()))
            .collect::<HashMap<_, _>>();

        for sub in bridge_update.clone().subscriptions() {
            if !self.current_subscriptions.contains_key(&sub.subscribe_to()) {
                added_subs.push(sub);
            } else if !self
                .current_subscriptions
                .get(&sub.subscribe_to())
                .expect("Key missing")
                .eq(&sub)
            {
                updated_subs.push(sub);
            }
        }
        for forward in bridge_update.clone().forwards() {
            if !self.current_forwards.contains_key(&forward.subscribe_to()) {
                added_forwards.push(forward);
            } else if !self
                .current_forwards
                .get(&forward.subscribe_to())
                .expect("Key missing")
                .eq(&forward)
            {
                updated_forwards.push(forward);
            }
        }

        for sub in self.current_subscriptions.keys() {
            if !subs_map.contains_key(sub) {
                removed_subs.push(self.current_subscriptions.get(sub).unwrap().to_owned())
            }
        }

        for forward in self.current_forwards.keys() {
            if !forwards_map.contains_key(forward) {
                removed_forwards.push(self.current_forwards.get(forward).unwrap().to_owned())
            }
        }

        BridgeDiff::default()
            .with_local_diff(
                PumpDiff::default()
                    .with_added(added_forwards)
                    .with_updated(updated_forwards)
                    .with_removed(removed_forwards),
            )
            .with_remote_diff(
                PumpDiff::default()
                    .with_added(added_subs)
                    .with_updated(updated_subs)
                    .with_removed(removed_subs),
            )
    }

    pub fn update(&mut self, bridge_diff: &BridgeDiff) {
        bridge_diff
            .local_updates()
            .added()
            .into_iter()
            .for_each(|added| {
                self.current_forwards
                    .insert(added.subscribe_to(), added.to_owned());
            });

        bridge_diff
            .local_updates()
            .updated()
            .into_iter()
            .for_each(|updated| {
                self.current_forwards
                    .insert(updated.subscribe_to(), updated.to_owned());
            });

        bridge_diff
            .local_updates()
            .removed()
            .iter()
            .for_each(|updated| {
                self.current_forwards.remove(&updated.subscribe_to());
            });

        bridge_diff
            .remote_updates()
            .added()
            .into_iter()
            .for_each(|added| {
                self.current_subscriptions
                    .insert(added.subscribe_to(), added.to_owned());
            });

        bridge_diff
            .remote_updates()
            .updated()
            .into_iter()
            .for_each(|updated| {
                self.current_subscriptions
                    .insert(updated.subscribe_to(), updated.to_owned());
            });

        bridge_diff
            .remote_updates()
            .removed()
            .iter()
            .for_each(|updated| {
                self.current_subscriptions.remove(&updated.subscribe_to());
            });
    }

    pub async fn send(&mut self, message: BridgeDiff) -> Result<(), Error> {
        self.bridge_handle
            .send(message)
            .await
            .map_err(Error::SendBridgeMessage)
    }
}

#[derive(Debug, Deserialize)]
pub struct BridgeControllerUpdate {
    bridge_updates: Vec<BridgeUpdate>,
}

impl BridgeControllerUpdate {
    pub fn from_bridge(name: &str, subs: Vec<TopicRule>, forwards: Vec<TopicRule>) -> Self {
        let subscriptions = subs
            .iter()
            .map(|s| Direction::Out(s.to_owned()))
            .chain(forwards.iter().map(|s| Direction::In(s.to_owned())))
            .collect();

        let bridge_update = BridgeUpdate {
            name: name.to_owned(),
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
    name: String,
    subscriptions: Vec<Direction>,
}

impl BridgeUpdate {
    pub fn name(&self) -> &str {
        self.name.as_ref()
    }

    pub fn subscriptions(self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::Out(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn forwards(self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::In(topic) | Direction::Both(topic) => Some(topic.clone()),
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
    updated: Vec<TopicRule>,
    removed: Vec<TopicRule>,
}

impl PumpDiff {
    pub fn with_added(mut self, added: Vec<TopicRule>) -> Self {
        self.added = added;
        self
    }

    pub fn with_updated(mut self, updated: Vec<TopicRule>) -> Self {
        self.updated = updated;
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

    pub fn updated(&self) -> Vec<&TopicRule> {
        self.updated.iter().collect::<Vec<_>>()
    }

    pub fn has_updates(&self) -> bool {
        !(self.added.is_empty() && self.removed.is_empty() && self.updated.is_empty())
    }
}
