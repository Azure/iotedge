//! A strongly-typed WWW-Authenticate header

use std::collections::HashMap;
use std::str::FromStr;

use failure::Fail;
use pest::error::Error as PestError;
use pest::Parser;
use pest_derive::Parser;

#[derive(Parser)]
#[grammar = "auth/www_authenticate.pest"]
struct PestWWWAuthenticateParser;

#[derive(Debug, Fail)]
pub enum WWWAuthenticateError {
    #[fail(display = "Failed to parse WWW-Authenticate Header: {}", _0)]
    Pest(PestError<Rule>),

    #[fail(display = "Error in Challenge: {}", _0)]
    BadChallenge(ChallengeError),
}

type Error = WWWAuthenticateError;

/// HTTP Authentication Schemes
#[derive(Clone, PartialEq, Eq, Debug)]
pub enum ChallengeScheme {
    Basic,
    Bearer,
    Digest,
    Other(String),
}

/// `WWW-Authenticate` header, defined in [RFC7235](https://tools.ietf.org/html/rfc7235#section-4.1)
impl FromStr for ChallengeScheme {
    type Err = ();
    fn from_str(s: &str) -> std::result::Result<ChallengeScheme, ()> {
        use self::ChallengeScheme::*;
        Ok(match s.to_ascii_lowercase().as_str() {
            "basic" => Basic,
            "bearer" => Bearer,
            "digest" => Digest,
            other => Other(other.to_string()),
        })
    }
}

#[derive(Debug, Fail)]
pub enum ChallengeError {
    #[fail(display = "TODO: add errors")]
    _Placeholder,
}

/// A WWW-Authenticate challenge, and it's associated parameters
#[derive(Debug, Clone, Eq, PartialEq)]
pub struct Challenge {
    scheme: ChallengeScheme,
    parameters: HashMap<String, String>,
}

impl Challenge {
    fn new(
        scheme: ChallengeScheme,
        parameters: HashMap<String, String>,
    ) -> Result<Challenge, ChallengeError> {
        // TODO: actually do validation based on given challenge scheme
        Ok(Challenge { scheme, parameters })
    }

    pub fn scheme(&self) -> &ChallengeScheme {
        &self.scheme
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

impl FromStr for WWWAuthenticate {
    type Err = Error;

    fn from_str(header_str: &str) -> Result<WWWAuthenticate, Error> {
        let mut res = WWWAuthenticate(Vec::new());

        // NOTE: The grammar itself provides a lot of invariants regarding the structure
        // of the returned pairs. As such, the code uses quite a lot of unwraps, which
        // rely on the early-bail behavior of the Pest grammar

        let challenge_list_p = PestWWWAuthenticateParser::parse(Rule::root, header_str)
            .map_err(Error::Pest)?
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
                let name = param_ps.next().unwrap().as_str().to_string().to_ascii_lowercase();
                let arg = param_ps
                    .next()
                    .unwrap()
                    .as_str()
                    .trim_matches('"')
                    .to_string();
                // TODO: unescape values in the arg
                parameters.insert(name, arg);
            }

            res.0
                .push(Challenge::new(scheme, parameters).map_err(Error::BadChallenge)?);
        }

        Ok(res)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    macro_rules! string_map(
        {} => { ::std::collections::HashMap::new() };
        { $($key:expr => $value:expr),+} => {
            {
                let mut m = ::std::collections::HashMap::new();
                $(
                    m.insert($key.to_string(), $value.to_string());
                )+
                m
            }
         };
    );

    macro_rules! single_challenge_valid {
        ($name:ident, $teststr:expr, $scheme:expr, $parameters:expr) => {
            #[test]
            fn $name() {
                assert_eq!(
                    $teststr.parse::<WWWAuthenticate>().unwrap(), 
                    WWWAuthenticate(vec![Challenge {
                        scheme: $scheme, 
                        parameters: $parameters
                    }])
                );
            }
        };
    }

    macro_rules! multi_challenge_valid {
        ($name:ident, $teststr:expr, $({$scheme:expr, $parameters:expr}),+) => {
            #[test]
            fn $name() {
                assert_eq!(
                    $teststr.parse::<WWWAuthenticate>().unwrap(), 
                    WWWAuthenticate(vec![
                        $(
                            Challenge {
                                scheme: $scheme, 
                                parameters: $parameters
                            },
                        )+
                    ])
                );
            }
        };
    }

    /// $err should be a pattern describing the expected error type 
    ///
    /// e.g: invalid!(test, "asd asd asd asd", Error::Pest(_))
    macro_rules! invalid {
        ($name:ident, $teststr:expr, $err:pat) => {
            #[test]
            fn $name() {
                let err = $teststr.parse::<WWWAuthenticate>().unwrap_err();
                match err {
                    $err => {}
                    _ => panic!()
                }
            }
        }
    }

    single_challenge_valid!(smoke, 
        "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/ubuntu:pull\"", 
        ChallengeScheme::Bearer,
        string_map!{
            "realm" => "https://auth.docker.io/token",
            "service" => "registry.docker.io",
            "scope" => "repository:library/ubuntu:pull"
        }
    );

    invalid!(close_but_invalid, "close Bearer realm=\"https://auth.docker.io/token\"", Error::Pest(_));

    #[test]
    fn complex_multi_mode() {
        let ours = r#" Digest realm="htt\"p\"-auth@example.org", qop="auth, auth-int",
    algorithm=MD5,
    nonce="7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
    opaque="FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS",
 Basic realm="example.com""#
            .parse::<WWWAuthenticate>()
            .unwrap();

        let expected = WWWAuthenticate(vec![
            Challenge {
                scheme: ChallengeScheme::Digest,
                parameters: string_map! {
                    "realm" => "htt\\\"p\\\"-auth@example.org",
                    "qop" => "auth, auth-int",
                    "algorithm" => "MD5",
                    "nonce" => "7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
                    "opaque" => "FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS"
                },
            },
            Challenge {
                scheme: ChallengeScheme::Basic,
                parameters: string_map! { "realm" => "example.com" },
            },
        ]);

        assert_eq!(ours, expected);
    }

    // subsequent tests are from http://test.greenbytes.de/tech/tc/httpauth/

    single_challenge_valid!(simplebasic, 
        r#"Basic realm="foo""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo" }
    );

    single_challenge_valid!(simplebasiclf, 
        r#"Basic
 realm="foo""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo" }
    );

    single_challenge_valid!(simplebasicucase, 
        r#"BASIC REALM="foo""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo" }
    );

    // TODO: Uncomment once challenge-type valdation is in
    // /// realm is only allowed to use quoted-string syntax
    // invalid!(simplebasictok, r#"Basic realm=foo"#, Error::ChallengeError(?));

    // FIXME: apparently, this should pass the parser, but not the realm check?
    // invalid!(simplebasictokbs, r#"Basic realm=\f\o\o"#, Error::?);

    // TODO: Uncomment once challenge-type valdation is in
    // /// realm is only allowed to use quoted-string syntax
    // invalid!(simplebasicsq, r#"Basic realm='foo'"#, Error::?);

    single_challenge_valid!(simplebasicpct, 
        r#"Basic realm="foo%20bar""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo%20bar" }
    );

    single_challenge_valid!(simplebasiccomma, 
        r#"Basic , realm="foo""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo" }
    );

    invalid!(simplebasiccomma2, r#"Basic, realm="foo""#, Error::Pest(_));

    // TODO: Uncomment once challenge-type valdation is in
    // invalid!(simplebasicnorealm, r#"Basic"#, Error::ChallengeError(?));

    // TODO: Uncomment once challenge-type valdation is in
    // invalid!(simplebasic2realms, r#"Basic realm="foo", realm="bar""#, Error::ChallengeError(?))

    single_challenge_valid!(simplebasicwsrealm, 
        r#"Basic realm = "foo""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo" }
    );

    // FIXME: this is broken
    // single_challenge_valid!(simplebasicrealmsqc, 
    //     r#"Basic realm="\f\o\o""#, 
    //     ChallengeScheme::Basic,
    //     string_map!{ "realm" => "foo" }
    // );

    // TODO: Uncomment once double-quote unescape is in 
    // single_challenge_valid!(simplebasicrealmsqc, 
    //     r#"Basic realm="\"foo\""#, 
    //     ChallengeScheme::Basic,
    //     string_map!{ "realm" => "foo" }
    // );

    single_challenge_valid!(simplebasicnewparam1, 
        r#"Basic realm="foo", bar="xyz",, a=b,,,c=d"#, 
        ChallengeScheme::Basic,
        string_map!{
            "realm" => "foo",
            "bar" => "xyz",
            "a" => "b",
            "c" => "d"
        }
    );

    single_challenge_valid!(simplebasicnewparam2, 
        r#"Basic bar="xyz", realm="foo""#, 
        ChallengeScheme::Basic,
        string_map!{
            "realm" => "foo",
            "bar" => "xyz"
        }
    );

    single_challenge_valid!(simplebasicrealmiso88591, 
        r#"Basic realm="foo-ä""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo-ä" }
    );

    single_challenge_valid!(simplebasicrealmut8, 
        r#"Basic realm="foo-Ã¤""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "foo-Ã¤" }
    );

    single_challenge_valid!(simplebasicrealmrfc2047, 
        r#"Basic realm="=?ISO-8859-1?Q?foo-=E4?=""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "=?ISO-8859-1?Q?foo-=E4?=" }
    );

    multi_challenge_valid!(multibasicunknown,
        r#"Basic realm="basic", Newauth realm="newauth""#,
        {
            ChallengeScheme::Basic,
            string_map!{ "realm" => "basic" }
        },
        {
            ChallengeScheme::Other("newauth".to_string()),
            string_map!{ "realm" => "newauth" }
        }
    );

    multi_challenge_valid!(multibasicunknownnoparam,
        r#"Basic realm="basic", Newauth"#,
        {
            ChallengeScheme::Basic,
            string_map!{ "realm" => "basic" }
        },
        {
            ChallengeScheme::Other("newauth".to_string()),
            string_map!{}
        }
    );

    multi_challenge_valid!(multibasicunknown2,
        r#"Newauth realm="newauth", Basic realm="basic""#,
        {
            ChallengeScheme::Other("newauth".to_string()),
            string_map!{ "realm" => "newauth" }
        },
        {
            ChallengeScheme::Basic,
            string_map!{ "realm" => "basic" }
        }
    );

    multi_challenge_valid!(multibasicunknown2np,
        r#"Newauth, Basic realm="basic""#,
        {
            ChallengeScheme::Other("newauth".to_string()),
            string_map!{}
        },
        {
            ChallengeScheme::Basic,
            string_map!{ "realm" => "basic" }
        }
    );

    single_challenge_valid!(multibasicempty, 
        r#",Basic realm="basic""#, 
        ChallengeScheme::Basic,
        string_map!{ "realm" => "basic" }
    );

    // TODO: Uncomment once double-quote unescape is in 
    // multi_challenge_valid!(multibasicqs, 
    //     r#"Newauth realm="apps", type=1, title="Login to \"apps\"", Basic realm="simple""#, 
    //     {
    //         ChallengeScheme::Other("newauth".to_string()),
    //         string_map!{
    //             "realm" => "apps",
    //             "type" => "1",
    //             "title" => "Login to \"apps\"" 
    //         }
    //     },
    //     {
    //         ChallengeScheme::Basic,
    //         string_map!{ "realm" => "simple" }
    //     }
    // );

    multi_challenge_valid!(multidisgscheme, 
        r#"Newauth realm="Newauth Realm", basic=foo, Basic realm="Basic Realm""#, 
        {
            ChallengeScheme::Other("newauth".to_string()),
            string_map!{
                "realm" => "Newauth Realm",
                "basic" => "foo"
            }
        },
        {
            ChallengeScheme::Basic,
            string_map!{ "realm" => "Basic Realm" }
        }
    );

    single_challenge_valid!(unknown, 
        r#"Newauth param="value""#, 
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{ "param" => "value" }
    );

    multi_challenge_valid!(parametersnotrequired, 
        r#"A, B"#, 
        {
            ChallengeScheme::Other("a".to_string()),
            string_map!{}
        },
        {
            ChallengeScheme::Other("b".to_string()),
            string_map!{}
        }
    );

    single_challenge_valid!(disguisedrealm, 
        r#"Basic foo="realm=nottherealm", realm="basic""#, 
        ChallengeScheme::Basic,
        string_map!{
            "foo" => "realm=nottherealm",
            "realm" => "basic"
        }
    );

    single_challenge_valid!(disguisedrealm2, 
        r#"Basic nottherealm="nottherealm", realm="basic""#, 
        ChallengeScheme::Basic,
        string_map!{
            "nottherealm" => "nottherealm",
            "realm" => "basic"
        }
    );

    invalid!(missing_quote, r#"Basic realm="basic"#, Error::Pest(_));
}
