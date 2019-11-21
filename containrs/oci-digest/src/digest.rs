use std::fmt;
use std::str::FromStr;

use crate::error::DigestParseError;
use crate::validator::Validator;

/// A cheap newtype around a [String] that ensures valid OCI digest string
/// formatting.
///
/// To validate data using it's Digest, use the [`validator`] to construct a new
/// [`Validator`].
#[derive(PartialEq, Eq, Ord, PartialOrd, Hash, Clone)]
pub struct Digest {
    string: String,
}

impl Digest {
    /// Create a new Digest
    pub fn new(s: &str) -> Result<Digest, DigestParseError> {
        s.parse::<Digest>()
    }

    /// Return a new validator for this digest, or None if the algorithm is
    /// not recognized
    pub fn validator(&self) -> Option<Validator> {
        Validator::new(self)
    }

    /// Convenience method to validate a one-off slice of data.
    ///
    /// Guaranteed to return false if the algorithm is not recognized.
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

    /// Consume self, returning the underlying string
    pub fn into_inner(self) -> String {
        self.string
    }

    /// Returns a reference to the algorithm component of the digest string.
    pub fn algorithm(&self) -> &str {
        &self.string[..self.string.find(':').unwrap()]
    }

    /// Returns a reference to the encoded component of the digest string.
    pub fn encoded(&self) -> &str {
        &self.string[(self.string.find(':').unwrap() + 1)..]
    }
}

impl fmt::Display for Digest {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.string)
    }
}

impl fmt::Debug for Digest {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "Digest({})", self.string)
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
