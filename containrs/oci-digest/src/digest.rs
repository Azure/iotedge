use std::fmt;
use std::str::FromStr;

use crate::error::DigestParseError;
use crate::validator::Validator;

/// A cheap newtype around a [String] that ensures it's contents are a valid OCI
/// digest string.
///
/// To validate data using it's Digest, use the [`validator`] to construct a new
/// [`Validator`].
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Digest {
    string: String,
}

impl Digest {
    /// Return a new validator for this digest, or None if the algorithm is
    /// unregistered
    pub fn validator(&self) -> Option<Validator> {
        Validator::new(self)
    }

    /// Convenience method to validate a one-off slice of data.
    /// Returns None if the algorithm is unregistered.
    pub fn validate(&self, data: &[u8]) -> bool {
        match self.validator() {
            Some(mut validator) => {
                validator.input(data);
                validator.validate()
            }
            None => false,
        }
    }

    /// View the digest as a raw str
    pub fn as_str(&self) -> &str {
        &self.string
    }
}

impl fmt::Display for Digest {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.string)
    }
}

impl FromStr for Digest {
    type Err = DigestParseError;

    fn from_str(s: &str) -> Result<Digest, Self::Err> {
        Validator::validate_digest_str(s)?;
        Ok(Digest {
            string: s.to_string(),
        })
    }
}
