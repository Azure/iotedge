//! A strongly-typed WWW-Authenticate header.
//!
//! # Example
//!
//! ```
//! use www_authenticate::{WWWAuthenticate, ChallengeScheme};
//!
//! let www_auth = "Bearer realm=\"https://example.com\",foo=\"bar\",foo2=baz"
//!     .parse::<WWWAuthenticate>()
//!     .unwrap();
//! let challenge = www_auth.into_iter().next().unwrap();
//! assert_eq!(challenge.scheme(), &ChallengeScheme::Bearer);
//! let parameters = challenge.into_parameters();
//! assert_eq!(parameters["realm"], "https://example.com".to_string());
//! assert_eq!(parameters["foo"], "bar".to_string());
//! assert_eq!(parameters["foo2"], "baz".to_string());
//! ```

use std::collections::HashMap;
use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

#[derive(Parser)]
#[grammar = "grammar.pest"]
struct PestWWWAuthenticateParser;

mod error;

pub use error::{ChallengeError, WWWAuthenticateError};

/// (Known) HTTP Authentication Schemes
#[derive(Clone, PartialEq, Eq, Debug)]
pub enum ChallengeScheme {
    Basic,
    Bearer,
    Digest,
    Other(String),
}

impl FromStr for ChallengeScheme {
    type Err = ();
    /// Infallible conversion, as any unrecognised schemes are parsed into a
    /// ChallengeScheme::Other(String)
    fn from_str(s: &str) -> Result<ChallengeScheme, ()> {
        use self::ChallengeScheme::*;
        Ok(match s.to_ascii_lowercase().as_str() {
            "basic" => Basic,
            "bearer" => Bearer,
            "digest" => Digest,
            other => Other(other.to_string()),
        })
    }
}

/// A valid WWW-Authenticate challenge, and it's associated parameters
#[derive(Debug, Clone, Eq, PartialEq)]
pub struct Challenge {
    scheme: ChallengeScheme,
    parameters: HashMap<String, String>,
}

impl Challenge {
    /// Construct a new WWW-Authenticate Challenge, returning an Error if given
    /// parameters are incompatible with the specified scheme
    pub fn new(
        scheme: ChallengeScheme,
        parameters: HashMap<String, String>,
    ) -> Result<Challenge, ChallengeError> {
        // TODO: actually do validation based on given challenge scheme
        Ok(Challenge { scheme, parameters })
    }

    /// Return challenge scheme
    pub fn scheme(&self) -> &ChallengeScheme {
        &self.scheme
    }

    /// Consumes self, and returns a hashmap of associated parameters
    pub fn into_parameters(self) -> HashMap<String, String> {
        self.parameters
    }
}

/// A valid WWW-Authenticate header, as defined in [RFC7235](https://tools.ietf.org/html/rfc7235#section-4.1)
///
/// Implements [IntoIterator] for iterating over the contained [Challenge]s
#[derive(PartialEq, Eq, Debug)]
pub struct WWWAuthenticate(Vec<Challenge>);

impl WWWAuthenticate {
    /// Create a new WWWAuthenticate header from a list of Challenges
    pub fn new(challenges: Vec<Challenge>) -> WWWAuthenticate {
        WWWAuthenticate(challenges)
    }
}

impl IntoIterator for WWWAuthenticate {
    type Item = Challenge;
    type IntoIter = ::std::vec::IntoIter<Self::Item>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

impl FromStr for WWWAuthenticate {
    type Err = WWWAuthenticateError;

    fn from_str(header_str: &str) -> Result<WWWAuthenticate, WWWAuthenticateError> {
        let mut res = WWWAuthenticate(Vec::new());

        // NOTE: The grammar itself provides a lot of invariants regarding the structure
        // of the returned pairs. As such, the code uses quite a lot of unwraps, which
        // rely on the early-bail behavior of the Pest grammar

        let challenge_list_p = PestWWWAuthenticateParser::parse(Rule::root, header_str)
            .map_err(WWWAuthenticateError::Parse)?
            // top-level rules are guaranteed to have a single Pair
            .next()
            .unwrap()
            // root rule must parse into a single challenge_list rule
            .into_inner()
            .next()
            .unwrap();

        // see example structure in the grammar file to get a better understanding of
        // how the following traversal works

        // iterate through the challenges
        for challenge_p in challenge_list_p.into_inner() {
            let mut challenge_ps = challenge_p.into_inner();
            // first pair will always be the scheme
            let scheme = challenge_ps
                .next()
                .unwrap()
                .as_str()
                .parse::<ChallengeScheme>()
                .unwrap(); // Impossible to fail parsing a ChallengeScheme

            // subsequent pairs will always be a bunch of auth_params
            let mut parameters: HashMap<String, String> = HashMap::new();
            for param_p in challenge_ps {
                let mut param_ps = param_p.into_inner();
                // each auth_param must have a name and arg
                let name = param_ps
                    .next()
                    .unwrap()
                    .as_str()
                    .to_string()
                    .to_ascii_lowercase();
                let arg = param_ps
                    .next()
                    .unwrap()
                    .as_str()
                    .trim_matches('"')
                    .to_string();
                // TODO: unescape values in the arg
                parameters.insert(name, arg);
            }

            res.0.push(
                Challenge::new(scheme, parameters).map_err(WWWAuthenticateError::BadChallenge)?,
            );
        }

        Ok(res)
    }
}
