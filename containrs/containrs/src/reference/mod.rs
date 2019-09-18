use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

#[derive(Parser)]
#[grammar = "reference/grammar.pest"]
struct PestReferenceParser;

pub mod error;

use error::{Error, Result};

#[derive(PartialEq, Eq, Clone, Debug)]
pub enum ReferenceKind {
    Tag(String),
    Digest(String), // TODO: replace with proper digest type
}

impl std::fmt::Display for ReferenceKind {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            ReferenceKind::Tag(s) => write!(f, "{}", s),
            ReferenceKind::Digest(d) => write!(f, "{}", d),
        }
    }
}

/// A well-formed object reference identifier, with all the information required
/// to pull an image (i.e: name, domain, and reference (either a tag or
/// digest)).
#[derive(PartialEq, Eq, Debug)]
pub struct Reference {
    name: String,
    domain: String,
    reference: ReferenceKind,
}

impl Reference {
    /// Return the object's name
    pub fn name(&self) -> &str {
        &self.name
    }

    /// Return the object's domain
    pub fn domain(&self) -> &str {
        &self.domain
    }

    /// Return the object's reference (either a tag or digest string)
    pub fn reference_kind(&self) -> &ReferenceKind {
        &self.reference
    }
}

/// A raw object reference identifier, which may not have all the information
/// required to pull an image.
#[derive(PartialEq, Eq, Debug)]
pub struct RawReference {
    pub name: String,
    pub domain: Option<String>,
    pub tag: Option<String>,
    pub digest: Option<String>,
}

impl RawReference {
    /// Converts the RawReference into a well-formed [Reference] using the given
    /// defaults if the RawReference is missing them
    ///
    /// Setting `docker_compat` to true will prepend `"library/"` to names when
    /// a custom domain is not specified. i.e: `ubuntu:latest` ->
    /// `library/ubuntu:latest`
    ///
    /// e.g: To parse docker-style image URIs:
    /// `raw_ref.canonicalize("registry-1.docker.io/".to_string(),
    /// "latest".to_string(), true)`
    pub fn canonicalize(
        self,
        default_domain: &str,
        default_tag: &str,
        docker_compat: bool,
    ) -> Reference {
        let RawReference {
            mut name,
            mut domain,
            tag,
            digest,
        } = self;

        // TODO: test this more
        if docker_compat {
            match domain {
                // correct incorrectly parsed docker non-library repo shornames
                // i.e: `prilik/ubuntu:latest`
                Some(ref domain_str) => {
                    if !domain_str.contains('.') && !name.contains('/') {
                        name = format!("{}/{}", domain_str, name);
                        domain = None; // fallback to default repo
                    }
                }
                // handle unqualified image names
                // (i.e: `ubuntu:latest` -> `library/ubuntu:latest`)
                None => {
                    if !name.contains('/') {
                        name.insert_str(0, "library/");
                    }
                }
            }
        }

        // If a digest and a tag are both specified, prioritize the digest
        let reference = match (digest, tag) {
            (Some(digest), _) => Some(ReferenceKind::Digest(digest)),
            (None, Some(tag)) => Some(ReferenceKind::Tag(tag)),
            (None, None) => None,
        };

        Reference {
            name,
            domain: domain.unwrap_or_else(|| default_domain.to_string()),
            reference: reference.unwrap_or_else(|| ReferenceKind::Tag(default_tag.to_string())),
        }
    }
}

impl FromStr for RawReference {
    type Err = Error;

    /// Parse a raw object reference str into a [RawReference], returning
    /// an Error if the refstr doesn't conform to the grammar, or violates
    /// some non-syntactic rule (e.g: invalid digest)
    ///
    /// For a full string grammar, see
    /// github.com/docker/distribution/reference/reference.go
    fn from_str(refstr: &str) -> Result<RawReference> {
        // TODO: leverage Pest for better error messages

        let mut name_str = None;
        let mut domain_str = None;
        let mut tag_str = None;
        let mut digest_str = None;

        let refstr_p = PestReferenceParser::parse(Rule::refstr, refstr)
            .map_err(Error::Pest)?
            // top-level rules are guaranteed to have a single Pair
            .next()
            .unwrap()
            // refstr rule _must_ parse into a single reference rule
            .into_inner()
            .next()
            .unwrap();

        // this nastly little mess of code traverses the parse tree to extract
        // the tokes we care about
        for ref_p in refstr_p.into_inner() {
            let val = ref_p.as_str().to_string();
            match ref_p.as_rule() {
                Rule::name => {
                    for name_p in ref_p.into_inner() {
                        let val = name_p.as_str().to_string();
                        match name_p.as_rule() {
                            Rule::domain => domain_str = Some(val),
                            Rule::path => name_str = Some(val),
                            _ => unreachable!(),
                        }
                    }
                }
                Rule::tag => tag_str = Some(val),
                Rule::digest => digest_str = Some(val),
                _ => unreachable!(),
            }
        }

        // ok to unwrap, as a name must be present for the string to parse
        let name = name_str.unwrap();

        if name.len() + domain_str.as_ref().map_or(0, |s| s.len() + 1) > 255 {
            return Err(Error::NameTooLong);
        }

        let mut digest = None;
        if let Some(digest_str) = digest_str {
            // TODO: validate digest, and convert it to a proper Digest type
            // return an error if the digest is invalid
            digest = Some(digest_str);
        }

        Ok(RawReference {
            name,
            domain: domain_str,
            tag: tag_str,
            digest,
        })
    }
}

/// Extended from github.com/docker/distribution/reference/reference_test.go
#[cfg(test)]
mod tests {
    use super::*;

    // TODO: look into using `proptest` to iron-out parsing issues

    #[test]
    fn bare() {
        assert_eq!(
            "test_com".parse::<RawReference>().unwrap(),
            RawReference {
                name: "test_com".to_string(),
                domain: None,
                tag: None,
                digest: None
            }
        );
    }

    #[test]
    fn tag() {
        assert_eq!(
            "test_com:tag".parse::<RawReference>().unwrap(),
            RawReference {
                name: "test_com".to_string(),
                domain: None,
                tag: Some("tag".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn url_ish() {
        assert_eq!(
            "test.com:5000".parse::<RawReference>().unwrap(),
            RawReference {
                name: "test.com".to_string(),
                domain: None,
                tag: Some("5000".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn domain_no_dot_port() {
        assert_eq!(
            "test:5000/repo".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test:5000".to_string()),
                tag: None,
                digest: None
            }
        );
    }

    #[test]
    fn domain_tag() {
        assert_eq!(
            "test.com/repo:tag".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test.com".to_string()),
                tag: Some("tag".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn domain_port() {
        assert_eq!(
            "test.com:5000/repo".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test.com:5000".to_string()),
                tag: None,
                digest: None
            }
        );
    }

    #[test]
    fn domain_port_tag() {
        assert_eq!(
            "test.com:5000/repo:tag".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test.com:5000".to_string()),
                tag: Some("tag".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn domain_port_digest() {
        assert_eq!(
            "test:5000/repo@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test:5000".to_string()),
                tag: None,
                digest: Some("sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".to_string())
            }
        );
    }

    #[test]
    fn domain_port_tag_digest() {
        assert_eq!(
            "test:5000/repo:tag@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".parse::<RawReference>().unwrap(),
            RawReference {
                name: "repo".to_string(),
                domain: Some("test:5000".to_string()),
                tag: Some("tag".to_string()),
                digest: Some("sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".to_string())
            }
        );
    }

    #[test]
    fn empty() {
        assert!("".parse::<RawReference>().is_err());
    }

    #[test]
    fn just_tag() {
        assert!(":justtag".parse::<RawReference>().is_err());
    }

    #[test]
    fn just_digest() {
        assert!(
            "@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
                .parse::<RawReference>()
                .is_err()
        );
    }

    #[test]
    fn short_digest() {
        assert!("@sha256:fffffffffffffffff".parse::<RawReference>().is_err());
    }

    #[rustfmt::skip]
    // FIXME: uncomment once digest validation is implemented
    // #[test]
    // fn unsupported_digest() {
    //     assert!(
    //         "validname@bruhhash:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
    //             .parse::<RawReference>()
    //             .is_err()
    //     );
    // }

    #[test]
    fn uppercase_name() {
        assert!("Uppercase:tag".parse::<RawReference>().is_err());
    }

    // FIXME "Uppercase" is incorrectly handled as a domain-name here, therefore
    // passes. See https://github.com/docker/distribution/pull/1778, and https://github.com/docker/docker/pull/20175
    // #[test]
    // fn uppercase_with_slash_tag() {
    //     assert!("Uppercase/lowercase:tag".parse::<RawReference>().is_err());
    // }

    #[test]
    fn domain_uppercase_name() {
        assert!("test:5000/Uppercase/lowercase:tag"
            .parse::<RawReference>()
            .is_err());
    }

    #[test]
    fn name_too_long() {
        assert!(("a/".repeat(128) + "a:tag")
            .parse::<RawReference>()
            .is_err());
    }

    #[test]
    fn name_almost_too_long() {
        let name = "a/".repeat(126) + "a";

        assert_eq!(
            ("a/".to_string() + &name + ":tag-puts-this-over-max")
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name,
                domain: Some("a".to_string()),
                tag: Some("tag-puts-this-over-max".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn nonsense() {
        assert!("aa/asdf$$^/aa".parse::<RawReference>().is_err());
    }

    #[test]
    fn complex_domain() {
        assert_eq!(
            "sub-dom1.foo.com/bar/baz/quux"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "bar/baz/quux".to_string(),
                domain: Some("sub-dom1.foo.com".to_string()),
                tag: None,
                digest: None
            }
        );
    }

    #[test]
    fn complex_domain_tag() {
        assert_eq!(
            "sub-dom1.foo.com/bar/baz/quux:long-tag"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "bar/baz/quux".to_string(),
                domain: Some("sub-dom1.foo.com".to_string()),
                tag: Some("long-tag".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn complex_domain_tag_2() {
        assert_eq!(
            "b.gcr.io/test.example.com/my-app:test.example.com"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "test.example.com/my-app".to_string(),
                domain: Some("b.gcr.io".to_string()),
                tag: Some("test.example.com".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn complex_domain_doubledash() {
        assert_eq!(
            "xn--n3h.com/myimage:xn--n3h.com"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "myimage".to_string(),
                domain: Some("xn--n3h.com".to_string()),
                tag: Some("xn--n3h.com".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn complex_domain_doubledash_digest() {
        assert_eq!(
            "xn--7o8h.com/myimage:xn--7o8h.com@sha512:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "myimage".to_string(),
                domain: Some("xn--7o8h.com".to_string()),
                tag: Some("xn--7o8h.com".to_string()),
                digest: Some("sha512:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".to_string())
            }
        );
    }

    #[test]
    fn complex_domain_front_hyphen() {
        assert!("-test.com/myimage".parse::<RawReference>().is_err());
    }

    #[test]
    fn complex_domain_end_hyphen() {
        assert!("test-.com/myimage".parse::<RawReference>().is_err());
    }

    #[test]
    fn misleading_1() {
        assert_eq!(
            "foo_bar.com:8080".parse::<RawReference>().unwrap(),
            RawReference {
                name: "foo_bar.com".to_string(),
                domain: None,
                tag: Some("8080".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn misleading_2() {
        assert_eq!(
            "foo/foo_bar.com:8080".parse::<RawReference>().unwrap(),
            RawReference {
                name: "foo_bar.com".to_string(),
                domain: Some("foo".to_string()),
                tag: Some("8080".to_string()),
                digest: None
            }
        );
    }

    #[test]
    fn azure() {
        assert_eq!(
            "iotedgeresources.azurecr.io/samplemodule:0.0.2-amd64"
                .parse::<RawReference>()
                .unwrap(),
            RawReference {
                name: "samplemodule".to_string(),
                domain: Some("iotedgeresources.azurecr.io".to_string()),
                tag: Some("0.0.2-amd64".to_string()),
                digest: None
            }
        );
    }
}
