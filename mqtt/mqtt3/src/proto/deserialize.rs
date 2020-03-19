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
use std::ops::DerefMut;
use std::sync::Mutex;

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

lazy_static! {
    static ref PUBDEDUPER: Mutex<PublicationDeDuper> = Mutex::new(PublicationDeDuper::new());
}

pub fn clear_publication_load() {
    PUBDEDUPER.lock().unwrap().reset();
}

struct PubDeDuperHolder<'a>(&'a mut PublicationDeDuper);

impl<'de, 'a> DeserializeSeed<'de> for PubDeDuperHolder<'a> {
    type Value = Bytes;

    fn deserialize<D>(self, deserializer: D) -> Result<Self::Value, D::Error>
    where
        D: Deserializer<'de>,
    {
        // Visitor implementation that will walk an inner array of the JSON
        // input.
        struct PublicationDeDuperVisitor<'a>(&'a mut PublicationDeDuper);

        impl<'de, 'a> Visitor<'de> for PublicationDeDuperVisitor<'a> {
            type Value = Bytes;

            fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                write!(formatter, "an array of integers")
            }

            fn visit_seq<A>(self, mut seq: A) -> Result<Bytes, A::Error>
            where
                A: SeqAccess<'de>,
            {
                let payload: Vec<u8> = seq
                    .next_element()?
                    .ok_or_else(|| de::Error::invalid_length(0, &self))?;

                let payload = self.0.de_dupe(payload);

                Ok(payload)
            }
        }

        deserializer.deserialize_seq(PublicationDeDuperVisitor(self.0))
    }
}

impl<'de> Deserialize<'de> for Publication {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct PublicationVisitor;

        impl<'de> Visitor<'de> for PublicationVisitor {
            type Value = Publication;

            fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                formatter.write_str("struct Publication")
            }

            fn visit_seq<V>(self, mut seq: V) -> Result<Publication, V::Error>
            where
                V: SeqAccess<'de>,
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
                    .next_element_seed(PubDeDuperHolder(
                        PUBDEDUPER.lock().map_err(de::Error::custom)?.deref_mut(),
                    ))?
                    .ok_or_else(|| de::Error::invalid_length(3, &self))?;

                Ok(Publication {
                    topic_name,
                    qos,
                    retain,
                    payload,
                })
            }
        }

        const FIELDS: &[&str] = &["topic_name", "qos", "retain", "payload"];
        deserializer.deserialize_struct("Publication", FIELDS, PublicationVisitor)
    }
}
