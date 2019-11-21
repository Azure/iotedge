//! Strongly-typed docker reference URIs.
//!
//! # Examples
//!
//! ## Simple
//!
//! ```
//! use docker_reference::{Reference, ReferenceKind};
//!
//! let reference = "ubuntu:19.04".parse::<Reference>().unwrap();
//! assert_eq!(reference.repo(), "library/ubuntu");
//! assert_eq!(reference.registry(), "registry-1.docker.io");
//! assert_eq!(reference.kind(), &ReferenceKind::Tag("19.04".to_string()));
//! ```
//!
//! ## RawReference
//!
//! ```
//! use docker_reference::{RawReference, Reference, ReferenceKind};
//!
//! // Parse a string into a RawReference
//! let raw_ref: RawReference = "ubuntu:19.04".parse::<RawReference>().unwrap();
//! assert_eq!(raw_ref.path(), "ubuntu".to_string());
//! assert_eq!(raw_ref.tag(), Some("19.04"));
//! assert_eq!(raw_ref.domain(), None);
//! assert_eq!(raw_ref.digest(), None);
//!
//! // Canonicalize the RawReference
//! // (in this case, docker compat will prepend "library/")
//! let reference = raw_ref.canonicalize();
//! assert_eq!(reference.repo(), "library/ubuntu");
//! assert_eq!(reference.registry(), "registry-1.docker.io");
//! assert_eq!(reference.kind(), &ReferenceKind::Tag("19.04".to_string()));
//! ```

use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

use oci_digest::Digest;

const DEFAULT_REGISTRY: &str = "registry-1.docker.io";

#[derive(Parser)]
#[grammar = "grammar.pest"]
struct PestReferenceParser;

mod error;

pub use error::{Error, Result};

/// A reference to a particular item in a repository.
///
/// ## Why aren't there two separate Tag and Digest types?
///
/// While reference strings _can_ specify both a tag and a digest, in such
/// cases, the tag is ignored completely, as the digest is more specific.
#[derive(Debug, PartialEq, Eq, Ord, PartialOrd, Hash, Clone)]
pub enum ReferenceKind {
    Tag(String),
    Digest(Digest),
}

impl ReferenceKind {
    /// Returns a reference to the raw underlying string (unlike to_string,
    /// which has a leading ':' or '@' signifying the underlying reference type)
    pub fn as_str(&self) -> &str {
        match self {
            ReferenceKind::Tag(s) => s.as_str(),
            ReferenceKind::Digest(d) => d.as_str(),
        }
    }
}

impl std::fmt::Display for ReferenceKind {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        match self {
            ReferenceKind::Tag(s) => write!(f, ":{}", s),
            ReferenceKind::Digest(d) => write!(f, "@{}", d),
        }
    }
}

/// A well-formed object reference identifier, with all the information required
/// to pull an image (i.e: repo, registry, and reference (either a tag or
/// digest)).
///
/// A [`Reference`] is immutable, but can be converted back into a
/// [`RawReference`] if modifications are required.
#[derive(Debug, PartialEq, Eq, Ord, PartialOrd, Hash, Clone)]
pub struct Reference {
    repo: String,
    registry: String,
    kind: ReferenceKind,
}

impl std::fmt::Display for Reference {
    fn fmt(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        write!(f, "{}/{}{}", self.registry, self.repo, self.kind)
    }
}

impl Reference {
    /// Return the reference's repo
    pub fn repo(&self) -> &str {
        &self.repo
    }

    /// Return the reference's registry
    pub fn registry(&self) -> &str {
        &self.registry
    }

    /// Return the reference kind (either a tag or digest string)
    pub fn kind(&self) -> &ReferenceKind {
        &self.kind
    }

    // Consumes self, returning a [RawReference]
    pub fn into_raw_reference(self) -> RawReference {
        self.to_string()
            .parse::<RawReference>()
            .expect("somehow constructed an invalid Reference")
    }
}

impl FromStr for Reference {
    type Err = Error;

    /// Parse a raw object reference str into a [Reference], returning
    /// an Error if the string doesn't conform to the docker reference grammar,
    /// or violates some non-syntactic rule (e.g: specifies an invalid digest)
    ///
    /// For the full grammar, see
    /// github.com/docker/distribution/reference/reference.go
    /// or, see the `grammar.pest` file used by this crate.
    fn from_str(s: &str) -> Result<Reference> {
        Ok(s.parse::<RawReference>()?.canonicalize())
    }
}

/// A raw object reference identifier, which may not have all the information
/// required to pull an image.
///
/// Should be converted into a [Reference] via `canonicalize`
#[derive(PartialEq, Eq, Debug)]
pub struct RawReference {
    path: String,
    domain: Option<String>,
    tag: Option<String>,
    digest: Option<Digest>,
}

impl RawReference {
    /// Create a new RawReference from it's constituent parts.
    pub fn new(
        path: String,
        domain: Option<String>,
        tag: Option<String>,
        digest: Option<Digest>,
    ) -> Result<RawReference> {
        let mut res = RawReference {
            path,
            domain: None,
            tag: None,
            digest,
        };

        res.set_domain(domain.as_ref().map(|s| s.as_str()))?;
        res.set_tag(tag.as_ref().map(|s| s.as_str()))?;

        Ok(res)
    }

    /// Consumes the RawReference, returning a well-formed [`Reference`],
    /// canonicalizing it according to docker's canonicalization rules:
    /// - if no registry domain is specified, "registry-1.docker.io" is used
    /// - if no tag/digest is specified, the "latest" tag is used
    /// - if no registry domain is specified and the repo is a single word,
    ///   "library/" is prepended to the repo ("ubuntu" -> "library/ubuntu")
    pub fn canonicalize(self) -> Reference {
        let RawReference {
            mut path,
            mut domain,
            tag,
            digest,
        } = self;

        // TODO: test docker-compat transformations some more
        match domain {
            // correct incorrectly parsed docker non-library repo shornames
            // i.e: `prilik/ubuntu:latest`
            Some(ref registry_str) => {
                if !registry_str.contains('.') && !path.contains('/') {
                    path = format!("{}/{}", registry_str, path);
                    domain = None; // fallback to default path
                }
            }
            // handle unqualified image names
            // (i.e: `ubuntu:latest` -> `library/ubuntu:latest`)
            None => {
                if !path.contains('/') {
                    path.insert_str(0, "library/");
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
            repo: path,
            registry: domain.unwrap_or_else(|| DEFAULT_REGISTRY.to_string()),
            kind: reference.unwrap_or_else(|| ReferenceKind::Tag("latest".to_string())),
        }
    }

    /// Return the raw-reference's path (i.e: repo)
    pub fn path(&self) -> &str {
        &self.path
    }

    /// Return the raw-reference's domain (i.e: registry)
    pub fn domain(&self) -> Option<&str> {
        self.domain.as_ref().map(String::as_ref)
    }

    /// Return the raw-reference's tag (if it exists)
    pub fn tag(&self) -> Option<&str> {
        self.tag.as_ref().map(String::as_ref)
    }

    /// Return the raw-reference's digest (if it exists)
    pub fn digest(&self) -> Option<&Digest> {
        self.digest.as_ref()
    }

    /// Set the raw-reference's path (i.e: repo)
    pub fn set_path(&mut self, s: impl Into<String>) -> Result<()> {
        self.path = {
            let s: String = s.into();
            let path = PestReferenceParser::parse(Rule::path, &s)
                .map_err(Error::Parse)?
                .as_str();

            if path.len() + self.domain.as_ref().map_or(0, |s| s.len() + 1) > 255 {
                return Err(Error::NameTooLong);
            }

            s
        };

        Ok(())
    }

    /// Set the raw-reference's domain (i.e: registry)
    pub fn set_domain(&mut self, s: Option<impl Into<String>>) -> Result<()> {
        self.domain = match s {
            None => None,
            Some(s) => {
                let s: String = s.into();
                let domain = PestReferenceParser::parse(Rule::domain, &s)
                    .map_err(Error::Parse)?
                    .as_str();

                if self.path.len() + domain.len() + 1 > 255 {
                    return Err(Error::NameTooLong);
                }

                Some(s)
            }
        };

        Ok(())
    }

    /// Set the raw-reference's tag
    pub fn set_tag(&mut self, s: Option<impl Into<String>>) -> Result<()> {
        self.tag = match s {
            None => None,
            Some(s) => {
                let s: String = s.into();
                let _tag = PestReferenceParser::parse(Rule::tag, &s).map_err(Error::Parse)?;
                Some(s)
            }
        };

        Ok(())
    }

    /// Set the raw-reference's digest
    pub fn set_digest(&mut self, d: Option<Digest>) {
        self.digest = d;
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

        let mut path_str = None;
        let mut domain_str = None;
        let mut tag_str = None;
        let mut digest_str = None;

        let refstr_p = PestReferenceParser::parse(Rule::refstr, refstr)
            .map_err(Error::Parse)?
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
                            Rule::path => path_str = Some(val),
                            _ => unreachable!(),
                        }
                    }
                }
                Rule::tag => tag_str = Some(val),
                Rule::digest => digest_str = Some(val),
                _ => unreachable!(),
            }
        }

        // ok to unwrap, as a path must be present for the string to parse
        let path = path_str.unwrap();

        if path.len() + domain_str.as_ref().map_or(0, |s| s.len() + 1) > 255 {
            return Err(Error::NameTooLong);
        }

        let mut digest = None;
        if let Some(digest_str) = digest_str {
            digest = Some(digest_str.parse().map_err(Error::Digest)?);
        }

        Ok(RawReference {
            path,
            domain: domain_str,
            tag: tag_str,
            digest,
        })
    }
}
