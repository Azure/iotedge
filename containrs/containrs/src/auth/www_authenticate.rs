//! Tools to parse and validate WWW-Authenticate headers
//!
//! *not battle tested!*
//! Seems to work well enough in containrs, but it'll probably fail in more
//! complex scenarios!
//!
//! If only https://github.com/hyperium/headers had WWW-Authenticate :'(

use std::collections::HashMap;
use std::str::FromStr;

use lazy_static::lazy_static;
use regex::Regex;

/// HTTP Authentication Schemes, as listed at
/// https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication#Authentication_schemes
#[derive(Clone, PartialEq, Eq, Debug)]
pub enum ChallengeKind {
    Basic,
    Bearer,
    Digest,
    Hoba,
    Mutual,
    Aws4HmacSha256,
    Other(String),
}

/// `WWW-Authenticate` header, defined in [RFC7235](https://tools.ietf.org/html/rfc7235#section-4.1)
impl FromStr for ChallengeKind {
    type Err = ();
    fn from_str(s: &str) -> std::result::Result<ChallengeKind, ()> {
        use self::ChallengeKind::*;
        Ok(match s {
            "Basic" => Basic,
            "Bearer" => Bearer,
            "Digest" => Digest,
            "HOBA" => Hoba,
            "Mutual" => Mutual,
            "AWS4-HMAC-SHA256" => Aws4HmacSha256,
            other => Other(other.to_string()),
        })
    }
}

/// A WWW-Authenticate challenge, and it's associated parameters
// TODO: strongly type the various challenge types?
#[derive(Debug, Clone, Eq, PartialEq)]
pub struct Challenge {
    kind: ChallengeKind,
    parameters: HashMap<String, String>,
}

impl Challenge {
    pub fn kind(&self) -> &ChallengeKind {
        &self.kind
    }

    pub fn into_parameters(self) -> HashMap<String, String> {
        self.parameters
    }
}

/// A valid WWW-Authenticate header.
///
/// Implements [IntoIterator] for iterating over the contained [Challenge]s
#[derive(PartialEq, Eq, Debug)]
pub struct WWWAuthenticate(Vec<Challenge>);

impl IntoIterator for WWWAuthenticate {
    type Item = Challenge;
    type IntoIter = ::std::vec::IntoIter<Self::Item>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

// TODO: maybe _don't_ use an unintelligible regex to parse these things
impl FromStr for WWWAuthenticate {
    type Err = ();
    fn from_str(s: &str) -> std::result::Result<WWWAuthenticate, ()> {
        lazy_static! {
            // parses (authtype)? (key)=("val")|(val)
            // group 1: authtype
            // group 2: key
            // group 3: value (in quotes)
            // group 4: value (without quotes)
            // https://regex101.com/ is your friend in understanding this beast
            static ref RE: Regex =
                Regex::new(r#"\s*(?:(\w+)\s+)?(\w+)=(?:"(.*?[^\\])"|([^"\s,]*)),?"#).unwrap();
        }

        let mut challenges = Vec::new();

        // headers are only valid if _all_ characters in the string are part of a
        // capture group. As such, keep track of the expected next char index while
        // iterating through the captures
        let mut expected_next_start = 0;

        for cap in RE.captures_iter(s) {
            // safe to unwrap, since a capture is guaranteed to capture _something_
            let full_cap = cap.get(0).unwrap();
            if expected_next_start != full_cap.start() {
                return Err(());
            }
            expected_next_start = full_cap.end();

            // Finding a new challenge requires creating a new  if this is the start of a
            // new challenge
            if let Some(kind) = cap.get(1) {
                challenges.push(Challenge {
                    kind: kind.as_str().parse()?,
                    parameters: HashMap::new(),
                })
            }

            // Parse subsequent groups into the latest challenge
            let parameters = &mut challenges.last_mut().ok_or(())?.parameters;

            let key = cap.get(2).ok_or(())?.as_str().to_string();
            let val = cap
                .get(3)
                .or_else(|| cap.get(4))
                .ok_or(())?
                .as_str()
                .to_string();

            // TODO: properly unescape val strings

            parameters.insert(key, val);
        }

        Ok(WWWAuthenticate(challenges))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn smoke() {
        assert_eq!(
            "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/ubuntu:pull\""
                .parse::<WWWAuthenticate>()
                .unwrap(),
            WWWAuthenticate(vec![Challenge {
                kind: ChallengeKind::Bearer,
                parameters: [
                    ("realm", "https://auth.docker.io/token"),
                    ("service", "registry.docker.io"),
                    ("scope", "repository:library/ubuntu:pull")
                ]
                .into_iter()
                .map(|(a, b)| (a.to_string(), b.to_string()))
                .collect()
            }])
        );
    }

    #[test]
    fn close_but_invalid() {
        assert!("close Bearer realm=\"https://auth.docker.io/token\""
            .parse::<WWWAuthenticate>()
            .is_err());
    }

    #[test]
    fn complex_multi_mode() {
        let ours = r#"
Digest realm="htt\"p\"-auth@example.org", qop="auth, auth-int",
    algorithm=MD5,
    nonce="7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
    opaque="FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS"
Basic realm="example.com""#
            .parse::<WWWAuthenticate>()
            .unwrap();

        let expected = WWWAuthenticate(vec![
            Challenge {
                kind: ChallengeKind::Digest,
                parameters: [
                    ("realm", "htt\\\"p\\\"-auth@example.org"),
                    ("qop", "auth, auth-int"),
                    ("algorithm", "MD5"),
                    ("nonce", "7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v"),
                    ("opaque", "FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS"),
                ]
                .into_iter()
                .map(|(a, b)| (a.to_string(), b.to_string()))
                .collect(),
            },
            Challenge {
                kind: ChallengeKind::Basic,
                parameters: [("realm", "example.com")]
                    .into_iter()
                    .map(|(a, b)| (a.to_string(), b.to_string()))
                    .collect(),
            },
        ]);

        assert_eq!(ours, expected);
    }
}
