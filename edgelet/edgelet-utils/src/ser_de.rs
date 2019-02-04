// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, HashMap};
use std::fmt;
use std::hash::BuildHasher;
use std::marker::PhantomData;
use std::result::Result as StdResult;
use std::str::FromStr;

use failure::ResultExt;
use serde::de::{self, Deserialize, DeserializeOwned, Deserializer, MapAccess, Visitor};
use serde::ser::{Serialize, Serializer};
use serde_json;

use error::{ErrorKind, Result};

// This implementation has been adapted from: https://serde.rs/string-or-struct.html

pub fn string_or_struct<'de, T, D>(deserializer: D) -> StdResult<T, D::Error>
where
    T: Deserialize<'de> + FromStr<Err = serde_json::Error>,
    D: Deserializer<'de>,
{
    // This is a Visitor that forwards string types to T's `FromStr` impl and
    // forwards map types to T's `Deserialize` impl. The `PhantomData` is to
    // keep the compiler from complaining about T being an unused generic type
    // parameter. We need T in order to know the Value type for the Visitor
    // impl.
    struct StringOrStruct<T>(PhantomData<fn() -> T>);

    impl<'de, T> Visitor<'de> for StringOrStruct<T>
    where
        T: Deserialize<'de> + FromStr<Err = serde_json::Error>,
    {
        type Value = T;

        fn expecting(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
            formatter.write_str("string or map")
        }

        fn visit_str<E>(self, value: &str) -> StdResult<T, E>
        where
            E: de::Error,
        {
            FromStr::from_str(value).map_err(de::Error::custom)
        }

        fn visit_map<M>(self, visitor: M) -> StdResult<T, M::Error>
        where
            M: MapAccess<'de>,
        {
            // `MapAccessDeserializer` is a wrapper that turns a `MapAccess`
            // into a `Deserializer`, allowing it to be used as the input to T's
            // `Deserialize` implementation. T then deserializes itself using
            // the entries from the map visitor.
            Deserialize::deserialize(de::value::MapAccessDeserializer::new(visitor))
        }
    }

    deserializer.deserialize_any(StringOrStruct(PhantomData))
}

pub fn serde_clone<T>(inp: &T) -> Result<T>
where
    T: Serialize + DeserializeOwned,
{
    Ok(serde_json::to_string(inp)
        .and_then(|s| serde_json::from_str(&s))
        .context(ErrorKind::SerdeClone)?)
}

pub fn serialize_ordered<S, T>(
    x: &HashMap<String, String, T>,
    serializer: S,
) -> StdResult<S::Ok, S::Error>
where
    S: Serializer,
    T: BuildHasher,
{
    let sorted_map: BTreeMap<_, _> = x.iter().collect();
    sorted_map.serialize(serializer)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::str::FromStr;

    use serde_json;

    #[derive(Debug, Deserialize)]
    struct Options {
        opt1: String,
        opt2: Option<String>,
    }

    impl FromStr for Options {
        type Err = serde_json::Error;

        fn from_str(s: &str) -> StdResult<Self, Self::Err> {
            serde_json::from_str(s)
        }
    }

    #[derive(Debug, Deserialize)]
    struct Container {
        #[serde(deserialize_with = "string_or_struct")]
        options: Options,
    }

    #[derive(Debug, Serialize)]
    struct Setting {
        #[serde(serialize_with = "serialize_ordered")]
        map: HashMap<String, String>,
    }

    #[test]
    fn deser_from_map() {
        let container_json = json!({
            "options": {
                "opt1": "val1",
                "opt2": "val2"
            }
        })
        .to_string();

        let container: Container = serde_json::from_str(&container_json).unwrap();
        assert_eq!(&container.options.opt1, "val1");
        assert_eq!(&container.options.opt2.unwrap(), "val2");
    }

    #[test]
    fn deser_from_str() {
        let container_json = json!({
            "options": json!({
                "opt1": "val1",
                "opt2": "val2"
            }).to_string()
        })
        .to_string();

        let container: Container = serde_json::from_str(&container_json).unwrap();
        assert_eq!(&container.options.opt1, "val1");
        assert_eq!(&container.options.opt2.unwrap(), "val2");
    }

    #[test]
    #[should_panic]
    fn deser_from_bad_str_fails() {
        let container_json = json!({
            "options": "not really json you know"
        })
        .to_string();

        let _container: Container = serde_json::from_str(&container_json).unwrap();
    }

    #[test]
    fn serde_clone_succeeds() {
        #[derive(Serialize, Deserialize)]
        struct CloneMe {
            name: String,
            age: u8,
        }

        let c1 = CloneMe {
            name: "p1".to_string(),
            age: 10,
        };
        let c2 = serde_clone(&c1).unwrap();
        assert_eq!(c1.name, c2.name);
        assert_eq!(c1.age, c2.age);
    }

    #[test]
    fn serde_serialize_map() {
        let setting_json = json!({
            "map": {
                "a": "val1",
                "b": "val2",
                "c": "val3"
            }
        })
        .to_string();

        let mut map = HashMap::new();
        map.insert("b".to_string(), "val2".to_string());
        map.insert("a".to_string(), "val1".to_string());
        map.insert("c".to_string(), "val3".to_string());

        let map_container = Setting { map };

        let s = serde_json::to_string(&map_container).unwrap();
        assert_eq!(s, setting_json);
    }
}
