use std::fmt;
use std::str::FromStr;

use crate::algorithms::Algorithm;
use crate::error::DigestParseError;
use crate::validator::Validator;

/// A cheap wrapper around a raw String that ensures it's contents is formatted
/// as a valid OCI digest string. i.e: the underlying string is guaranteed to
/// be of the form `<algorithm>:<hex-digest>`
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Digest {
    string: String,
}

impl Digest {
    /// Return a new validator for this digest
    pub fn new_validator(&self) -> Validator {
        Validator::new(self)
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
        let mut parts = s.split(':');

        let algorithm = parts
            .next()
            .ok_or(DigestParseError::InvalidFormat)?
            .parse::<Algorithm>()
            .map_err(|_| DigestParseError::Unsupported)?;
        let digest_str = parts.next().ok_or(DigestParseError::InvalidFormat)?;

        if digest_str
            .matches(|c: char| !(('0'..='9').contains(&c) || ('a'..='f').contains(&c)))
            .count()
            != 0
        {
            return Err(DigestParseError::InvalidFormat);
        }

        // each byte takes 2 chars when represented in a hex string
        if digest_str.len() != algorithm.digest_len() * 2 {
            return Err(DigestParseError::InvalidLength);
        }

        Ok(Digest {
            string: s.to_string(),
        })
    }
}
