//! Strongly-typed docker authorization scopes.
//!
//! Based off information from the following sources:
//! - https://github.com/docker/distribution/blob/f656e60de56ff4d83e660af01b44bc595ad09cb6/registry/handlers/app.go#L906
//! - https://docs.docker.com/registry/spec/auth/scope/

use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

#[derive(Parser)]
#[grammar = "grammar.pest"]
struct PestScopeParser;

mod error;

pub use error::Error;

use std::string::ToString;

/// A collection of [`Scope`]s for one more more resources. Backed by a
/// `Vec<Scope>`.
pub struct Scopes(Vec<Scope>);

impl Scopes {
    /// Consume self, returning the underlying `Vec<Scope>`
    pub fn into_vec(self) -> Vec<Scope> {
        self.0
    }
}

/// Describes a set of actions that are being performed / can be performed on a
/// resource. Can be converted into / from Docker-style scope strings
/// (via .to_string and .parse respectively)
///
/// **NOTE:** When parsing scopes from a WWW-Authenticate header, make sure to
/// parse into a `Scopes` structure, as there may be multiple resource-scopes in
/// a single scope string
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Scope {
    actions: Vec<Action>,
    resource: Resource,
}

impl Scope {
    /// Create a new scope given a `resource` and a set of `actions`
    pub fn new(resource: Resource, actions: &[Action]) -> Scope {
        Scope {
            actions: actions.to_vec(),
            resource,
        }
    }

    /// Get the resource associated with this scope
    pub fn resource(&self) -> &Resource {
        &self.resource
    }

    /// Iterate over the actions associated with this scope
    pub fn actions(&self) -> impl Iterator<Item = &Action> {
        self.actions.iter()
    }
}

impl FromStr for Scope {
    type Err = Error;

    fn from_str(s: &str) -> Result<Scope, Error> {
        let mut resourcescope_ps = PestScopeParser::parse(Rule::resourcescope, s)
            .map_err(Error::Parse)?
            // top-level rules are guaranteed to have a single Pair
            .next()
            .unwrap()
            .into_inner();

        // see example structure in the grammar file to get a better understanding of
        // how the following traversal works

        // the first item must be a resource type
        let kind = resourcescope_ps
            .next()
            .unwrap()
            .as_str()
            .parse::<ResourceKind>()
            .unwrap(); // infallible parse
                       // the next item must be a resource name
        let name = resourcescope_ps.next().unwrap().as_str();

        // all subsequent items must be actions
        let actions = resourcescope_ps
            .map(|action_p| action_p.as_str().parse::<Action>().unwrap()) // infallible parse
            .collect::<Vec<_>>();

        Ok(Scope::new(Resource::new(kind, name), &actions))
    }
}

impl FromStr for Scopes {
    type Err = Error;

    fn from_str(s: &str) -> Result<Scopes, Error> {
        // splitting on ' ' is equivalent to the grammar's definition
        let scopes = s
            .split(' ')
            .map(|s| s.parse::<Scope>())
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Scopes(scopes))
    }
}

/// Identifies a resource associated with a given scope.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Resource {
    kind: ResourceKind,
    name: String,
}

impl Resource {
    /// Create a new Resource
    pub fn new(kind: ResourceKind, name: &str) -> Resource {
        Resource {
            kind,
            name: name.to_string(),
        }
    }

    /// Convenience method for creating Repository resources
    pub fn repo(name: &str) -> Resource {
        Resource {
            kind: ResourceKind::Repository,
            name: name.to_string(),
        }
    }

    /// Convenience method for creating Registry resources
    pub fn registry(name: &str) -> Resource {
        Resource {
            kind: ResourceKind::Registry,
            name: name.to_string(),
        }
    }
}

/// The kind of action the scope enables.
///
/// FromStr will never fail, returning the `Unknown` variant instead.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum Action {
    // Docker standard actions
    Pull,
    Push,
    Delete,
    Wildcard,
    // ACR Extensions
    MetadataRead,
    // Other
    Unknown(String),
}

impl ToString for Action {
    fn to_string(&self) -> String {
        use self::Action::*;
        match self {
            Pull => "pull",
            Push => "push",
            Delete => "delete",
            Wildcard => "*",
            MetadataRead => "metadata_read",
            Unknown(s) => s,
        }
        .to_string()
    }
}

impl FromStr for Action {
    type Err = ();
    fn from_str(s: &str) -> Result<Action, ()> {
        use self::Action::*;
        let action = match s {
            "pull" => Pull,
            "push" => Push,
            "delete" => Delete,
            "*" => Wildcard,
            "metadata_read" => MetadataRead,
            s => Unknown(s.to_string()),
        };
        Ok(action)
    }
}

/// The kind of resource this scope is for (either a repository or registry).
///
/// FromStr will never fail, returning the `Unknown` variant instead.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum ResourceKind {
    Repository,
    Registry,
    Unknown(String),
}

impl ToString for ResourceKind {
    fn to_string(&self) -> String {
        use self::ResourceKind::*;
        match self {
            Repository => "repository",
            Registry => "registry",
            Unknown(s) => s,
        }
        .to_string()
    }
}

impl FromStr for ResourceKind {
    type Err = ();
    fn from_str(s: &str) -> Result<ResourceKind, ()> {
        use self::ResourceKind::*;
        let resource = match s {
            "repository" => Repository,
            "registry" => Registry,
            s => Unknown(s.to_string()),
        };
        Ok(resource)
    }
}
