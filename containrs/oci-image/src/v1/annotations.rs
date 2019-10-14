use std::collections::HashMap;

/// A JSON field that contains arbitrary metadata.
///
/// It conforms to the following rules:
///
/// - Annotations MUST be a key-value map where both the key and value MUST be
///   strings.
/// - While the value MUST be present, it MAY be an empty string.
/// - Keys MUST be unique within this map, and best practice is to namespace the
///   keys.
/// - Keys SHOULD be named using a reverse domain notation - e.g.
///   com.example.myKey.
/// - The prefix org.opencontainers is reserved for keys defined in Open
///   Container Initiative (OCI) specifications and MUST NOT be used by other
///   specifications and extensions.
/// - Keys using the org.opencontainers.image namespace are reserved for use in
///   the OCI Image Specification and MUST NOT be used by other specifications
///   and extensions, including other OCI specifications.
/// - If there are no annotations then this property MUST either be absent or be
///   an empty map.
/// - Consumers MUST NOT generate an error if they encounter an unknown
///   annotation key.
pub type Annotations = HashMap<String, String>;

pub mod key {
    /// The date and time on which the image was built (date-time string as
    /// defined by RFC 3339).
    pub const CREATED: &str = "org.opencontainers.image.created";

    /// The contact details of the people or organization responsible for the
    /// image (freeform string).
    pub const AUTHORS: &str = "org.opencontainers.image.authors";

    /// The URL to find more information on the image.
    pub const URL: &str = "org.opencontainers.image.url";

    /// The URL to get documentation on the image.
    pub const DOCUMENTATION: &str = "org.opencontainers.image.documentation";

    /// The URL to get source code for building the image.
    pub const SOURCE: &str = "org.opencontainers.image.source";

    /// The version of the packaged software.
    /// The version MAY match a label or tag in the source code repository.
    /// The version MAY be Semantic versioning-compatible.
    pub const VERSION: &str = "org.opencontainers.image.version";

    /// The source control revision identifier for the packaged software.
    pub const REVISION: &str = "org.opencontainers.image.revision";

    /// The name of the distributing entity, organization or individual.
    pub const VENDOR: &str = "org.opencontainers.image.vendor";

    /// The license(s) under which contained software is distributed as an SPDX
    /// License Expression.
    pub const LICENSES: &str = "org.opencontainers.image.licenses";

    /// The name of the reference for a target.
    /// SHOULD only be considered valid when on descriptors on `index.json`
    /// within image layout.
    ///
    /// The reference must match the following grammar:
    ///
    /// ```ignore
    /// ref       ::= component ("/" component)*
    /// component ::= alphanum (separator alphanum)*
    /// alphanum  ::= [A-Za-z0-9]+
    /// separator ::= [-._:@+] | "--"
    /// ```
    ///
    /// NOTE: This grammar is a superset of the docker-reference grammar. All
    /// docker references are valid OCI references, but the inverse is NOT
    /// guaranteed!
    pub const REFNAME: &str = "org.opencontainers.image.ref.name";

    /// The human-readable title of the image.
    pub const TITLE: &str = "org.opencontainers.image.title";

    /// The human-readable description of the software packaged in the image.
    pub const DESCRIPTION: &str = "org.opencontainers.image.description";
}
