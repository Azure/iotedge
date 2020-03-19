#![allow(dead_code)]
#![allow(unused_imports)]

use crate::proto::packet::Publication;
use bytes::Bytes;
use lazy_static::lazy_static;
use serde::de::{self, Deserialize, DeserializeSeed, Deserializer, SeqAccess, Visitor};
use std::collections::hash_map::DefaultHasher;
use std::collections::HashMap;
use std::fmt;
use std::hash::{Hash, Hasher};
use std::marker::PhantomData;
use std::ops::DerefMut;
use std::sync::Mutex;

pub struct DeserializeState {
    payload_de_duper: PublicationDeDuper,
}

impl DeserializeState {
    pub fn new() -> Self {
        Self {
            payload_de_duper: PublicationDeDuper::new(),
        }
    }
}

pub struct SeededDeserializer<'a, T>(&'a mut DeserializeState, PhantomData<T>);
pub struct SeededVisitor<'a, T>(&'a mut DeserializeState, PhantomData<T>);

impl<'de, 'a> DeserializeSeed<'de> for SeededDeserializer<'a, Bytes> {
    type Value = Bytes;

    fn deserialize<D>(self, deserializer: D) -> Result<Self::Value, D::Error>
    where
        D: Deserializer<'de>,
    {
        impl<'de, 'a> Visitor<'de> for SeededVisitor<'a, Bytes> {
            type Value = Bytes;

            fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                write!(formatter, "an array of integers")
            }

            fn visit_seq<A>(self, mut seq: A) -> Result<Self::Value, A::Error>
            where
                A: SeqAccess<'de>,
            {
                let payload: Vec<u8> = seq
                    .next_element()?
                    .ok_or_else(|| de::Error::invalid_length(0, &self))?;

                let payload = self.0.payload_de_duper.de_dupe(payload);

                Ok(payload)
            }
        }

        deserializer.deserialize_seq(SeededVisitor::<Self::Value>(self.0, PhantomData))
    }
}

impl<'de, 'a> DeserializeSeed<'de> for SeededDeserializer<'a, Publication> {
    type Value = Publication;

    fn deserialize<D>(self, deserializer: D) -> Result<Self::Value, D::Error>
    where
        D: Deserializer<'de>,
    {
        impl<'de, 'a> Visitor<'de> for SeededVisitor<'a, Publication> {
            type Value = Publication;

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
                let retain = seq
                    .next_element()?
                    .ok_or_else(|| de::Error::invalid_length(2, &self))?;
                let payload = seq
                    .next_element_seed(SeededDeserializer::<Bytes>(self.0, PhantomData))?
                    .ok_or_else(|| de::Error::invalid_length(3, &self))?;

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

struct PublicationDeDuper {
    loaded_bytes: HashMap<u64, Bytes>,
}

impl PublicationDeDuper {
    fn new() -> Self {
        Self {
            loaded_bytes: HashMap::new(),
        }
    }

    fn de_dupe(&mut self, bytes: Vec<u8>) -> Bytes {
        let hash = Self::calculate_hash(&bytes);
        if let Some(payload) = self.loaded_bytes.get(&hash) {
            if payload == &bytes {
                return payload.clone();
            }
        }

        let payload = Bytes::from(bytes);
        self.loaded_bytes.insert(hash, payload.clone());

        payload
    }

    fn calculate_hash<T: Hash>(t: &T) -> u64 {
        let mut s = DefaultHasher::new();
        t.hash(&mut s);
        s.finish()
    }

    fn reset(&mut self) {
        self.loaded_bytes = HashMap::new();
    }
}
