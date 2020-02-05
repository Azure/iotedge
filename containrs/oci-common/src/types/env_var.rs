use serde::{Deserialize, Deserializer, Serialize, Serializer};

/// A newtype around `String` which ensures it is a properly formatted
/// environment variable (i.e: "name=value")
///
/// Includes custom [Deserialize] implementation that enforces formatting
#[derive(Debug, PartialEq, Eq, Clone)]
pub struct EnvVar(String);

impl EnvVar {
    /// Checks if the string is a valid EnvVar
    pub fn validate(s: &str) -> bool {
        s.contains('=')
    }

    /// Create a new EnvVar, returning an error if the String isn't a valid env
    /// var.
    pub fn new(s: String) -> Result<EnvVar, ()> {
        if !EnvVar::validate(&s) {
            Err(())
        } else {
            Ok(EnvVar(s))
        }
    }

    /// Returns the name component of the EnvVar
    pub fn name(&self) -> &str {
        // safe to unwrap, since splitn returns an iterator with at-least 1 element
        self.0.splitn(2, '=').nth(0).unwrap()
    }

    /// Returns the value component of the EnvVar
    pub fn value(&self) -> &str {
        self.0
            .splitn(2, '=')
            .nth(1)
            .expect("somehow obtained an `EnvVar` without any '=' chars")
    }
}

impl Serialize for EnvVar {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.0.serialize(serializer)
    }
}

impl<'de> Deserialize<'de> for EnvVar {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        EnvVar::new(s).map_err(|_| serde::de::Error::custom("invalid environment variable"))
    }
}
