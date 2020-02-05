use std::fmt;
use std::str::FromStr;

use sha2::digest::DynDigest;

use crate::digest::Digest;
use crate::error::*;

pub struct Validator {
    expect_digest: Vec<u8>,
    digest: Box<dyn DynDigest>,
}

impl Validator {
    /// Returns a new Validator.
    /// If the digest algorithm is not recognized, returns None instead.
    pub(crate) fn new(digest: &Digest) -> Option<Validator> {
        const EXPECT_VALID: &str = "digest struct should never be backed by a malformed string";

        let mut parts = digest.as_str().split(':');
        let algorithm = parts
            .next()
            .expect(EXPECT_VALID)
            .parse::<AlgorithmKind>()
            .expect(EXPECT_VALID);
        let digest_str = parts.next().expect(EXPECT_VALID);

        use AlgorithmKind::*;

        Some(Validator {
            expect_digest: match algorithm {
                Sha256 | Sha384 | Sha512 => hex::decode(digest_str).expect(EXPECT_VALID),
                Unregistered(_) => return None,
            },
            digest: match algorithm {
                Sha256 => Box::new(sha2::Sha256::default()),
                Sha384 => Box::new(sha2::Sha384::default()),
                Sha512 => Box::new(sha2::Sha512::default()),
                Unregistered(_) => return None,
            },
        })
    }

    /// Check if the given string matches the digest grammar, _without_ creating
    /// a new validator.
    ///
    /// The top-level grammar is copied below, through individual algorithms may
    /// impose additional constraints on the `encoded` component of the grammar.
    ///
    /// ```ignore
    /// digest                ::= algorithm ":" encoded
    /// algorithm             ::= algorithm-component (algorithm-separator algorithm-component)*
    /// algorithm-component   ::= [a-z0-9]+
    /// algorithm-separator   ::= [+._-]
    /// encoded               ::= [a-zA-Z0-9=_-]+
    /// ```
    pub(crate) fn validate_digest_str(s: &str) -> Result<(), DigestParseError> {
        let mut parts = s.split(':');

        let algorithm = parts
            .next()
            .ok_or(DigestParseError::InvalidFormat)?
            .parse::<AlgorithmKind>()
            .map_err(|_| DigestParseError::Unsupported)?;
        let digest_str = parts.next().ok_or(DigestParseError::InvalidFormat)?;

        /// Matches against [a-zA-Z0-9=_-]+
        fn valid_encoded(s: &str) -> bool {
            // avoid pulling in the whole regex crate for such a simple check
            s.chars().all(|c| match c {
                'a'..='z' | 'A'..='Z' | '0'..='9' | '=' | '_' | '-' => true,
                _ => false,
            }) && !s.is_empty()
        }

        if !valid_encoded(digest_str) {
            return Err(DigestParseError::InvalidFormat);
        }

        // fine-grained checks for individual algorithms
        use AlgorithmKind::*;
        match algorithm {
            Sha256 | Sha384 | Sha512 => {
                // Note that [A-F] MUST NOT be used here
                if !digest_str.chars().all(|c| match c {
                    'a'..='f' | '0'..='9' => true,
                    _ => false,
                }) {
                    return Err(DigestParseError::InvalidFormat);
                }

                let expect_len = match algorithm {
                    Sha256 => 256,
                    Sha384 => 384,
                    Sha512 => 512,
                    _ => unreachable!(),
                };

                if digest_str.len() != expect_len / 8 * 2 {
                    return Err(DigestParseError::InvalidLength);
                }
            }
            // unspecified algorithm
            Unregistered(_) => {}
        };

        Ok(())
    }

    /// Digest input data.
    ///
    /// This method can be called repeatedly for use with streaming messages.
    pub fn input(&mut self, data: &[u8]) {
        self.digest.input(data)
    }

    /// Consumes the validator, returning true if input data's digest matches
    /// the expected digest.
    pub fn validate(self) -> bool {
        self.digest.result()[..] == self.expect_digest[..]
    }
}

#[derive(Debug, Clone)]
pub enum AlgorithmKind {
    Sha256,
    Sha384,
    Sha512,
    Unregistered(String),
}

impl fmt::Display for AlgorithmKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::AlgorithmKind::*;
        let s = match self {
            Sha256 => "sha256",
            Sha384 => "sha384",
            Sha512 => "sha512",
            Unregistered(other) => other,
        };
        f.write_str(s)
    }
}

impl FromStr for AlgorithmKind {
    type Err = DigestParseError;
    fn from_str(s: &str) -> Result<AlgorithmKind, DigestParseError> {
        // TODO: replace this with something that validates against the grammar
        //
        // algorithm             ::= algorithm-component
        //                           (algorithm-separator algorithm-component)*
        // algorithm-component   ::= [a-z0-9]+
        // algorithm-separator   ::= [+._-]
        use self::AlgorithmKind::*;
        let algorithm_kind = match s {
            "sha256" => Sha256,
            "sha384" => Sha384,
            "sha512" => Sha512,
            other => Unregistered(other.to_string()),
        };
        Ok(algorithm_kind)
    }
}
