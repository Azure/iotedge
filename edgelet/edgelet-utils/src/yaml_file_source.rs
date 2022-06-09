// Copyright (c) Microsoft. All rights reserved.

use std::borrow::Cow;
use std::collections::HashMap;
use std::fs::File;
use std::io::Read;
use std::path::PathBuf;

use config::{ConfigError, Source, Value};
use yaml_rust::{Yaml, YamlLoader};

/// This is similar to [`config::File`] in that it parses a YAML string or file and implements `config::Source`.
///
/// We use this to parse our config.yaml instead of `config::File` because `config::File` lower-cases all field names that it reads.
/// This causes issues with fields like `agent.config.createOptions` since the config crate returns `agent.config.createoptions`
/// which the serde deserializer ignores.
#[derive(Clone, Debug)]
pub enum YamlFileSource {
    File(PathBuf),
    String(Cow<'static, str>),
}

impl Source for YamlFileSource {
    fn clone_into_box(&self) -> Box<dyn Source + Send + Sync> {
        Box::new(self.clone())
    }

    fn collect(&self) -> Result<HashMap<String, Value>, ConfigError> {
        let origin = match self {
            YamlFileSource::File(path) => Some(path.to_string_lossy().into_owned()),
            YamlFileSource::String(_) => None,
        };

        let contents = match self {
            YamlFileSource::File(path) => {
                let mut file =
                    File::open(path).map_err(|err| ConfigError::Foreign(Box::new(err)))?;
                let mut contents = String::new();
                let _ = file
                    .read_to_string(&mut contents)
                    .map_err(|err| ConfigError::Foreign(Box::new(err)))?;
                Cow::Owned(contents)
            }

            YamlFileSource::String(s) => Cow::Borrowed(&**s),
        };

        let docs = YamlLoader::load_from_str(&*contents)
            .map_err(|err| ConfigError::Foreign(Box::new(err)))?;

        let mut docs = docs.into_iter();
        let doc = match docs.next() {
            Some(doc) => {
                if docs.next().is_some() {
                    return Err(ConfigError::Foreign(Box::new(
                        YamlFileSourceError::MoreThanOneDocument,
                    )));
                }

                doc
            }

            None => Yaml::Hash(Default::default()),
        };

        let mut result = HashMap::new();

        if let Yaml::Hash(hash) = doc {
            for (key, value) in hash {
                if let Yaml::String(key) = key {
                    result.insert(key, from_yaml_value(origin.as_ref(), value)?);
                }
            }
        }

        Ok(result)
    }
}

/// Identical to https://github.com/mehcode/config-rs/blob/0.8.0/src/file/format/yaml.rs#L32-L68
/// except that it does not lower-case hash keys.
///
/// Unfortunately the `ValueKind` enum used by the `Value` constructor is not exported from the crate.
/// It does however impl `From` for the various corresponding standard types, so this code uses those.
/// The only difference is the fallback `_` case at the end.
fn from_yaml_value(uri: Option<&String>, value: Yaml) -> Result<Value, ConfigError> {
    match value {
        Yaml::String(value) => Ok(Value::new(uri, value)),
        Yaml::Real(value) => {
            // TODO: Figure out in what cases this can fail?
            Ok(Value::new(
                uri,
                value
                    .parse::<f64>()
                    .map_err(|err| ConfigError::Foreign(Box::new(err)))?,
            ))
        }
        Yaml::Integer(value) => Ok(Value::new(uri, value)),
        Yaml::Boolean(value) => Ok(Value::new(uri, value)),
        Yaml::Hash(table) => {
            let mut m = HashMap::new();
            for (key, value) in table {
                if let Yaml::String(key) = key {
                    m.insert(key, from_yaml_value(uri, value)?);
                }
                // TODO: should we do anything for non-string keys?
            }
            Ok(Value::new(uri, m))
        }
        Yaml::Array(array) => {
            let mut l = vec![];

            for value in array {
                l.push(from_yaml_value(uri, value)?);
            }

            Ok(Value::new(uri, l))
        }

        // 1. Yaml NULL
        // 2. BadValue – It shouldn't be possible to hit BadValue as this only happens when
        //               using the index trait badly or on a type error but we send back nil.
        // 3. Alias – No idea what to do with this and there is a note in the lib that its
        //            not fully supported yet anyway
        //
        // All of these return ValueKind::Nil in original, so use
        //    Option::None here to transform into ValueKind::Nil
        _ => Ok(Value::new(uri, None::<String>)),
    }
}

#[derive(Debug)]
enum YamlFileSourceError {
    MoreThanOneDocument,
}

impl std::fmt::Display for YamlFileSourceError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            YamlFileSourceError::MoreThanOneDocument => {
                write!(f, "more than one YAML document provided")
            }
        }
    }
}

impl std::error::Error for YamlFileSourceError {}
