//! Strongly-typed docker reference URIs.
//!
//! # Examples
//!
//! ## Simple
//!
//! ```
//! use docker_reference::{Reference, ReferenceKind};
//!
//! let reference = Reference::parse("ubuntu:19.04", "registry-1.docker.io", true).unwrap();
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
//! assert_eq!(raw_ref.name, "ubuntu".to_string());
//! assert_eq!(raw_ref.tag, Some("19.04".to_string()));
//! assert_eq!(raw_ref.domain, None);
//! assert_eq!(raw_ref.digest, None);
//!
//! // Canonicalize the RawReference, enabling docker compat
//! // (in this case, docker compat will prepend "library/")
//! let reference = raw_ref.canonicalize("registry-1.docker.io", true);
//! assert_eq!(reference.repo(), "library/ubuntu");
//! assert_eq!(reference.registry(), "registry-1.docker.io");
//! assert_eq!(reference.kind(), &ReferenceKind::Tag("19.04".to_string()));
//! ```

use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

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
/// to pull an image (i.e: repo, registry, and reference (either a tag or
/// digest).
#[derive(PartialEq, Eq, Debug)]
pub struct Reference {
    repo: String,
    registry: String,
    kind: ReferenceKind,
}

impl Reference {
    /// Parse a Reference from a given refstr, using the `default_registry` if
    /// none is found in the string.
    ///
    /// Setting `docker_compat` will ensure the Result can communicate with
    /// dockerhub. See [RawReference::canonicalize] for details on the specific
    /// transformations.
    pub fn parse(refstr: &str, default_registry: &str, docker_compat: bool) -> Result<Reference> {
        Ok(refstr
            .parse::<RawReference>()?
            .canonicalize(default_registry, docker_compat))
    }

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
    /// default registry if the RawReference is missing it
    ///
    /// Setting `docker_compat` to true will prepend `"library/"` to names when
    /// a custom registry is not specified. i.e: `ubuntu:latest` ->
    /// `library/ubuntu:latest`
    pub fn canonicalize(self, default_registry: &str, docker_compat: bool) -> Reference {
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
                Some(ref registry_str) => {
                    if !registry_str.contains('.') && !name.contains('/') {
                        name = format!("{}/{}", registry_str, name);
                        domain = None; // fallback to default name
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
            repo: name,
            registry: domain.unwrap_or_else(|| default_registry.to_string()),
            kind: reference.unwrap_or_else(|| ReferenceKind::Tag("latest".to_string())),
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
