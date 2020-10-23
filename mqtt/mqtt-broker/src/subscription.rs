use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    str::FromStr,
};

use mqtt3::proto;
use serde::{Deserialize, Serialize};

use crate::Error;

const NUL_CHAR: char = '\0';
const TOPIC_SEPARATOR: char = '/';
static MULTILEVEL_WILDCARD: &str = "#";
static SINGLELEVEL_WILDCARD: &str = "+";

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Subscription {
    filter: TopicFilter,
    max_qos: proto::QoS,
}

impl Subscription {
    pub fn new(filter: TopicFilter, max_qos: proto::QoS) -> Self {
        Self { filter, max_qos }
    }

    pub fn filter(&self) -> &TopicFilter {
        &self.filter
    }

    pub fn max_qos(&self) -> &proto::QoS {
        &self.max_qos
    }
}

#[derive(Default, Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct TopicFilter {
    segments: Vec<Segment>,
    multilevel: bool,
}

impl TopicFilter {
    pub fn new(segments: Vec<Segment>) -> Self {
        let len = segments.len();
        let multilevel = len > 0 && segments[len - 1] == Segment::MultiLevelWildcard;
        Self {
            segments,
            multilevel,
        }
    }

    pub fn matches(&self, topic_name: &str) -> bool {
        let mut segments = self.segments.iter();
        let mut levels = topic_name.split(TOPIC_SEPARATOR);

        let mut segment = segments.next();
        let mut level = levels.next();

        if let Some(Segment::MultiLevelWildcard) = segment {
            if let Some(l) = level {
                if l.starts_with('$') {
                    return false;
                }
            }
        }
        match (segment, level) {
            (Some(Segment::MultiLevelWildcard), Some(l)) if l.starts_with('$') => return false,
            (Some(Segment::SingleLevelWildcard), Some(l)) if l.starts_with('$') => return false,
            (_, _) => (),
        }

        while segment.is_some() || level.is_some() {
            match (segment, level) {
                (Some(Segment::MultiLevelWildcard), _l) => return true,
                (Some(s), Some(l)) if !s.matches(l) => return false,
                (Some(_), Some(_)) => (),
                (Some(_), None) => return false,
                (None, _) => return false,
            }

            segment = segments.next();
            level = levels.next();
        }
        true
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub enum Segment {
    Level(String),
    SingleLevelWildcard,
    MultiLevelWildcard,
}

impl Segment {
    pub fn matches(&self, segment: &str) -> bool {
        match self {
            Segment::Level(s) => s == segment,
            Segment::SingleLevelWildcard => true,
            Segment::MultiLevelWildcard => true,
        }
    }
}

impl Display for TopicFilter {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        let len = self.segments.len();
        for (i, segment) in self.segments.iter().enumerate() {
            match segment {
                Segment::Level(s) => write!(f, "{}", s)?,
                Segment::SingleLevelWildcard => write!(f, "{}", SINGLELEVEL_WILDCARD)?,
                Segment::MultiLevelWildcard => write!(f, "{}", MULTILEVEL_WILDCARD)?,
            }
            if i != len - 1 {
                write!(f, "{}", TOPIC_SEPARATOR)?;
            }
        }
        Ok(())
    }
}

impl FromStr for TopicFilter {
    type Err = Error;

    fn from_str(string: &str) -> Result<Self, Self::Err> {
        // [MQTT-4.7.3-1] - All Topic Names and Topic Filters MUST be at least
        // one character long.
        // [MQTT-4.7.3-2] - Topic Names and Topic Filters MUST NOT include the
        // null character (Unicode U+0000).
        if string.is_empty() || string.contains(NUL_CHAR) {
            return Err(Error::InvalidTopicFilter(string.to_owned()));
        }

        let mut segments = Vec::new();
        for s in string.split(TOPIC_SEPARATOR) {
            let segment = if s == MULTILEVEL_WILDCARD {
                Segment::MultiLevelWildcard
            } else if s == SINGLELEVEL_WILDCARD {
                Segment::SingleLevelWildcard
            } else {
                if s.contains(MULTILEVEL_WILDCARD) || s.contains(SINGLELEVEL_WILDCARD) {
                    return Err(Error::InvalidTopicFilter(string.to_owned()));
                }
                Segment::Level(s.to_owned())
            };
            segments.push(segment);
        }

        // [MQTT-4.7.1-2] - The multi-level wildcard character MUST be
        // specified either on its own or following a topic level separator.
        // In either case it MUST be the last character specified in the
        // Topic Filter.
        let len = segments.len();
        for (i, segment) in segments.iter().enumerate() {
            if *segment == Segment::MultiLevelWildcard && i != len - 1 {
                return Err(Error::InvalidTopicFilter(string.to_owned()));
            }
        }
        let filter = TopicFilter::new(segments);
        Ok(filter)
    }
}

#[cfg(test)]
mod tests {
    use std::str::FromStr;

    use proptest::prelude::*;

    use crate::subscription::{Segment, TopicFilter};

    fn filter(segments: Vec<Segment>) -> TopicFilter {
        TopicFilter::new(segments)
    }

    #[test]
    fn topic_filter_valid() {
        let cases = vec![
            (
                "/finance",
                filter(vec![
                    Segment::Level("".to_string()),
                    Segment::Level("finance".to_string()),
                ]),
            ),
            (
                "sport/#",
                filter(vec![
                    Segment::Level("sport".to_string()),
                    Segment::MultiLevelWildcard,
                ]),
            ),
            ("#", filter(vec![Segment::MultiLevelWildcard])),
            (
                "sport/tennis/#",
                filter(vec![
                    Segment::Level("sport".to_string()),
                    Segment::Level("tennis".to_string()),
                    Segment::MultiLevelWildcard,
                ]),
            ),
            ("+", filter(vec![Segment::SingleLevelWildcard])),
            (
                "+/tennis/#",
                filter(vec![
                    Segment::SingleLevelWildcard,
                    Segment::Level("tennis".to_string()),
                    Segment::MultiLevelWildcard,
                ]),
            ),
            (
                "sport/+/player1",
                filter(vec![
                    Segment::Level("sport".to_string()),
                    Segment::SingleLevelWildcard,
                    Segment::Level("player1".to_string()),
                ]),
            ),
        ];

        for (case, expected) in &cases {
            let result = TopicFilter::from_str(case).unwrap();
            assert_eq!(*expected, result);
        }
    }

    #[test]
    fn topic_filter_invalid() {
        let cases = vec![
            "",
            "#/blah",
            "sport/tennis#",
            "sport/tennis/#/ranking",
            "sport+",
            "bla\0h+",
        ];

        for case in &cases {
            let result = TopicFilter::from_str(case);
            assert!(result.is_err());
        }
    }

    proptest! {
        #[test]
        fn display_roundtrip(filter in crate::proptest::arb_topic_filter()) {
            let string = filter.to_string();
            prop_assert_eq!(filter, string.parse::<TopicFilter>().unwrap());
        }
    }

    #[test]
    fn test_topic_matching() {
        let cases = vec![
            ("#", "blah", true),
            ("blah/#", "blah", true),
            ("blah/blah2/#", "blah", false),
            ("blah/+/blah2", "blah/blah1/blah2", true),
            ("blah/+/blah2", "blah/blah1", false),
            ("blah/+", "blah/blah1", true),
            ("blah/blah1", "blah/blah1/blah2", false),
            ("blah/blah1/blah2", "blah/blah", false),
            ("#", "$SYS/blah", false),
            ("+", "$SYS", false),
        ];

        for (filter, topic, expected) in &cases {
            let parsed = TopicFilter::from_str(filter).unwrap();
            assert_eq!(
                *expected,
                parsed.matches(topic),
                "filter \"{}\" matches topic \"{}\"",
                filter,
                topic
            );
        }
    }

    #[test]
    fn test_topic_filter_matching() {
        let cases = vec![
            ("#", "#", true),
            ("#", "+", true),
            ("blah/#", "blah/+", true),
            ("blah/blah2/#", "blah/#", false),
            ("blah/+/blah2", "blah/blah1/#", false),
            ("blah/+/blah2", "blah/+/blah1", false),
            ("blah/+", "blah/blah1/#", false),
            ("blah/blah1", "blah/blah1/+", false),
            ("blah/blah1/blah2", "blah/blah/+", false),
            ("#", "$SYS/#", false),
            ("+", "$SYS/+", false),
        ];

        for (filter, topic, expected) in &cases {
            let parsed = TopicFilter::from_str(filter).unwrap();
            assert_eq!(
                *expected,
                parsed.matches(topic),
                "filter \"{}\" matches topic filter \"{}\"",
                filter,
                topic
            );
        }
    }
}
