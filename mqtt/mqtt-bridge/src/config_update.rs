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
    #[serde(rename = "bridges")]
    bridge_updates: Vec<BridgeUpdate>,
}

impl BridgeControllerUpdate {
    pub fn from_bridge_topic_rules(name: &str, subs: &[TopicRule], forwards: &[TopicRule]) -> Self {
        let subscriptions = subs
            .iter()
            .map(|s| Direction::In(s.to_owned()))
            .chain(forwards.iter().map(|s| Direction::Out(s.to_owned())))
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
    #[serde(rename = "settings")]
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn diff_with_empty_current_topics_and_empty_update() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let config_updater = ConfigUpdater::new(handler);

        let bridge_update = BridgeUpdate {
            endpoint: "$upstream".to_owned(),
            subscriptions: Vec::new(),
        };
        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &PumpDiff::default());
        assert_eq!(diff.remote_updates(), &PumpDiff::default());
    }

    #[test]
    fn diff_with_empty_current_topics_and_remote_pump_update() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let config_updater = ConfigUpdater::new(handler);

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }"#;

        let topic_rule = r#"{
             "topic": "test/#",
             "inPrefix": "/local",
             "outPrefix": "/remote"
        }"#;

        let expected =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule).unwrap()]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &PumpDiff::default());
        assert_eq!(diff.remote_updates(), &expected);
    }

    #[test]
    fn diff_with_empty_current_topics_and_local_pump_update() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let config_updater = ConfigUpdater::new(handler);

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "out",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }"#;

        let topic_rule = r#"{
             "topic": "test/#",
              "inPrefix": "/local",
              "outPrefix": "/remote"
        }"#;

        let expected_topic_rule: TopicRule = serde_json::from_str(topic_rule).unwrap();
        let expected = PumpDiff::default().with_added(vec![expected_topic_rule]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &expected);
        assert_eq!(diff.remote_updates(), &PumpDiff::default());
    }

    #[test]
    fn diff_with_empty_current_topics_and_both_pump_update() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let config_updater = ConfigUpdater::new(handler);

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "out",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "both",
                    "topic": "test2/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }"#;

        let topic_rule1 = r#"{
            "topic": "test/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#;
        let topic_rule2 = r#"{
           "topic": "test2/#",
           "inPrefix": "/local",
           "outPrefix": "/remote"
        }"#;

        let expected = PumpDiff::default().with_added(vec![
            serde_json::from_str(topic_rule1).unwrap(),
            serde_json::from_str(topic_rule2).unwrap(),
        ]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &expected);
        assert_eq!(diff.remote_updates(), &expected);
    }

    #[test]
    fn diff_with_current_topics_and_both_pump_update_added() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule);

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "out",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "both",
                    "topic": "existing/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }"#;

        let topic_rule1 = r#"{
            "topic": "test/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#;

        let expected =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule1).unwrap()]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &expected);
        assert_eq!(diff.remote_updates(), &expected);
    }

    #[test]
    fn diff_with_current_topics_and_both_pump_update_outprefix_updated() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
                "topic": "test/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/test/#".to_owned(), existing_rule);

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/updated"
                }
            ]
        }"#;

        let topic_rule1 = r#"{
            "topic": "test/#",
            "inPrefix": "/local",
            "outPrefix": "/updated"
        }"#;

        let expected =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule1).unwrap()]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &PumpDiff::default());
        assert_eq!(diff.remote_updates(), &expected);
    }

    #[test]
    fn diff_with_current_topics_and_both_pump_update_added_and_removed() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule.clone());

        let update = r#"
        {
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "out",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "both",
                    "topic": "test2/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }"#;

        let topic_rule1 = r#"{
            "topic": "test/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#;
        let topic_rule2 = r#"{
           "topic": "test2/#",
           "inPrefix": "/local",
           "outPrefix": "/remote"
        }"#;

        let expected = PumpDiff::default()
            .with_added(vec![
                serde_json::from_str(topic_rule1).unwrap(),
                serde_json::from_str(topic_rule2).unwrap(),
            ])
            .with_removed(vec![existing_rule]);

        let bridge_update: BridgeUpdate = serde_json::from_str(update).unwrap();

        let diff = config_updater.diff(&bridge_update);

        assert_eq!(diff.local_updates(), &expected);
        assert_eq!(diff.remote_updates(), &expected);
    }

    #[test]
    fn update_config_from_diff_added() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule.clone());

        let topic_rule1 = r#"{
                "topic": "forward/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#;

        let topic_rule2 = r#"{
                "topic": "sub/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#;

        let forwards_diff =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule1).unwrap()]);

        let subs_diff =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule2).unwrap()]);

        config_updater.update(
            &BridgeDiff::default()
                .with_local_diff(forwards_diff)
                .with_remote_diff(subs_diff),
        );

        let expected_forward_rule = serde_json::from_str(topic_rule1).unwrap();
        let expected_subs_rule = serde_json::from_str(topic_rule2).unwrap();
        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/existing/#")
                .unwrap(),
            &existing_rule
        );
        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/forward/#")
                .unwrap(),
            &expected_forward_rule
        );
        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/subs/#")
                .is_none(),
            true
        );
        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/existing/#")
                .unwrap(),
            &existing_rule
        );
        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/sub/#")
                .unwrap(),
            &expected_subs_rule
        );
        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/forward/#")
                .is_none(),
            true
        );
    }

    #[test]
    fn update_config_from_diff_updated() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule);

        let topic_rule1 = r#"{
                "topic": "existing/#",
                "inPrefix": "/local",
                "outPrefix": "/forward-remote"
            }"#;

        let topic_rule2 = r#"{
                "topic": "existing/#",
                "inPrefix": "/local",
                "outPrefix": "/sub-remote"
            }"#;

        let forwards_diff =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule1).unwrap()]);

        let subs_diff =
            PumpDiff::default().with_added(vec![serde_json::from_str(topic_rule2).unwrap()]);

        config_updater.update(
            &BridgeDiff::default()
                .with_local_diff(forwards_diff)
                .with_remote_diff(subs_diff),
        );

        let expected_forward_rule = serde_json::from_str(topic_rule1).unwrap();
        let expected_subs_rule = serde_json::from_str(topic_rule2).unwrap();
        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/existing/#")
                .unwrap(),
            &expected_forward_rule
        );
        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/subs/#")
                .is_none(),
            true
        );
        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/existing/#")
                .unwrap(),
            &expected_subs_rule
        );
        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/forward/#")
                .is_none(),
            true
        );
    }

    #[test]
    fn update_config_from_diff_removed_forward() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule.clone());

        let topic_rule1 = r#"{
                "topic": "existing/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#;

        let forwards_diff =
            PumpDiff::default().with_removed(vec![serde_json::from_str(topic_rule1).unwrap()]);

        config_updater.update(&BridgeDiff::default().with_local_diff(forwards_diff));

        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/existing/#")
                .is_none(),
            true
        );

        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/existing/#")
                .is_some(),
            true
        );
    }

    #[test]
    fn update_config_from_diff_removed_sub() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);
        let existing_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "existing/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        config_updater
            .current_subscriptions
            .insert("/local/existing/#".to_owned(), existing_rule.clone());
        config_updater
            .current_forwards
            .insert("/local/existing/#".to_owned(), existing_rule.clone());

        let topic_rule1 = r#"{
                "topic": "existing/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#;

        let forwards_diff =
            PumpDiff::default().with_removed(vec![serde_json::from_str(topic_rule1).unwrap()]);

        config_updater.update(&BridgeDiff::default().with_remote_diff(forwards_diff));

        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/existing/#")
                .is_some(),
            true
        );

        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/existing/#")
                .is_none(),
            true
        );
    }

    #[test]
    fn update_config_from_diff_removed_sub_when_not_in_current() {
        let (local_handle, _) = crate::pump::channel();
        let (remote_handle, _) = crate::pump::channel();

        let handler = BridgeHandle::new(local_handle, remote_handle);

        let mut config_updater = ConfigUpdater::new(handler);

        let topic_rule1 = r#"{
                "topic": "existing/#",
                "inPrefix": "/local",
                "outPrefix": "/remote"
            }"#;

        let forwards_diff =
            PumpDiff::default().with_removed(vec![serde_json::from_str(topic_rule1).unwrap()]);

        config_updater.update(&BridgeDiff::default().with_remote_diff(forwards_diff));

        assert_eq!(
            config_updater
                .current_forwards
                .get("/local/existing/#")
                .is_none(),
            true
        );

        assert_eq!(
            config_updater
                .current_subscriptions
                .get("/local/existing/#")
                .is_none(),
            true
        );
    }

    #[test]
    fn deserialize_bridge_controller_update() {
        let update = r#"{
        "bridges": [{
            "endpoint": "$upstream",
            "settings":  [
                {
                    "direction": "in",
                    "topic": "test/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                },
                {
                    "direction": "out",
                    "topic": "test2/#",
                    "inPrefix": "/local",
                    "outPrefix": "/remote"
                }
            ]
        }]}"#;

        let bridge_controller_update: BridgeControllerUpdate =
            serde_json::from_str(update).unwrap();

        let updates = bridge_controller_update.bridge_updates();
        let bridge_update = updates.first().take().unwrap();

        let sub_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "test/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        let forward_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "test2/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        assert_eq!(bridge_update.clone().endpoint(), "$upstream");
        assert_eq!(bridge_update.clone().subscriptions(), vec![sub_rule]);
        assert_eq!(bridge_update.clone().forwards(), vec![forward_rule]);
    }

    #[test]
    fn bridge_controller_from_bridge() {
        let sub_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "sub/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        let forward_rule: TopicRule = serde_json::from_str(
            r#"{
            "topic": "forward/#",
            "inPrefix": "/local",
            "outPrefix": "/remote"
        }"#,
        )
        .unwrap();

        let bridge_controller_update = BridgeControllerUpdate::from_bridge_topic_rules(
            "$upstream",
            vec![sub_rule.clone()].as_slice(),
            vec![forward_rule.clone()].as_slice(),
        );

        let updates = bridge_controller_update.bridge_updates();
        let bridge_update = updates.first().take().unwrap();

        assert_eq!(bridge_update.clone().endpoint(), "$upstream");
        assert_eq!(bridge_update.clone().subscriptions(), vec![sub_rule]);
        assert_eq!(bridge_update.clone().forwards(), vec![forward_rule]);
    }
}
