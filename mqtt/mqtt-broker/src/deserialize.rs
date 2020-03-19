#![allow(dead_code)]
#![allow(unused_imports)]

use serde::de::{self, Deserialize, DeserializeSeed, Deserializer, SeqAccess, Visitor};
use std::collections::hash_map::DefaultHasher;
use std::collections::HashMap;
use std::fmt;
use std::hash::{Hash, Hasher};
use std::marker::PhantomData;
use std::ops::DerefMut;
use std::sync::Mutex;

use crate::broker::BrokerState;
use mqtt3::proto::*;

impl<'de, 'a> DeserializeSeed<'de> for SeededDeserializer<'a, BrokerState> {
    type Value = BrokerState;

    fn deserialize<D>(self, deserializer: D) -> Result<Self::Value, D::Error>
    where
        D: Deserializer<'de>,
    {
        impl<'de, 'a> Visitor<'de> for SeededVisitor<'a, BrokerState> {
            type Value = BrokerState;

            fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                write!(formatter, "an array of integers")
            }

            fn visit_seq<A>(self, mut seq: A) -> Result<Self::Value, A::Error>
            where
                A: SeqAccess<'de>,
            {
                let topic_name = seq
                    .next_element()?
                    .ok_or_else(|| de::Error::invalid_length(0, &self))?;
                let qos = seq
                    .next_element()?
                    .ok_or_else(|| de::Error::invalid_length(1, &self))?;

                Ok(Publication {
                    topic_name,
                    qos,
                    retain,
                    payload,
                })
            }
        }

        deserializer.deserialize_seq(SeededVisitor::<Self::Value>(self.0, PhantomData))
    }
}
