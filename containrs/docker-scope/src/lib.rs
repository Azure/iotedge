//! Strongly-typed docker authorization scopes.
//!
//! Based off information from the following sources:
//! - https://github.com/docker/distribution/blob/f656e60de56ff4d83e660af01b44bc595ad09cb6/registry/handlers/app.go#L906
//! - https://docs.docker.com/registry/spec/auth/scope/

use std::collections::HashSet;
use std::str::FromStr;

use pest::Parser;
use pest_derive::Parser;

#[derive(Parser)]
#[grammar = "grammar.pest"]
struct PestScopeParser;

mod error;

pub use error::Error;

use std::string::ToString;

/// A collection of [`Scope`]s for one more more resources.
#[derive(Debug)]
pub struct Scopes(Vec<Scope>);

impl Scopes {
    /// Create a new collection of [`Scope`]s
    pub fn new(scopes: impl IntoIterator<Item = Scope>) -> Scopes {
        Scopes(scopes.into_iter().collect())
    }

    /// Returns an iterator over the scopes
    pub fn iter(&self) -> impl Iterator<Item = &Scope> {
        self.0.iter()
    }

    /// Checks if any of the scopes in `self` are supersets of `scope`.
    /// i.e: They reference the same resource, and `scope`'s actions are a
    /// subset of `self`'s actions.
    pub fn is_superset(&self, scope: &Scope) -> bool {
        self.iter().any(|s| s.is_superset(scope))
    }

    /// Checks if any of the scopes in `self` overlap with `scope`.
    /// i.e: They reference the same resource, and share at least one action.
    pub fn is_disjoint(&self, scope: &Scope) -> bool {
        self.iter().any(|s| s.is_disjoint(scope))
    }

    /// Add an additional scope to the collection
    pub fn add(&mut self, scope: Scope) {
        self.0.push(scope)
    }
}

impl IntoIterator for Scopes {
    type Item = Scope;
    type IntoIter = ::std::vec::IntoIter<Self::Item>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

/// Describes a set of actions that are being performed / can be performed on a
/// resource. Can be converted into / from Docker-style scope strings
/// (via .to_string and .parse respectively)
///
/// **NOTE:** When parsing scopes from a WWW-Authenticate header, make sure to
/// parse into a `Scopes` structure, as there may be multiple resource-scopes in
/// a single scope string
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Scope {
    actions: HashSet<Action>,
    resource: Resource,
}

impl Scope {
    /// Create a new scope given a `resource` and a set of `actions`
    pub fn new<'a>(resource: Resource, actions: impl IntoIterator<Item = &'a Action>) -> Scope {
        Scope {
            actions: actions.into_iter().cloned().collect(),
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

    /// Checks if `self` is a subset of the given `scope`
    /// i.e: They reference the same resource, and `scope`'s actions are a
    /// subset of `self`'s actions.
    pub fn is_superset(&self, scope: &Scope) -> bool {
        if self.resource != scope.resource {
            return false;
        }
        self.actions.is_superset(&scope.actions)
    }

    /// Checks if `self` is disjoint with the given `scope`.
    /// i.e: They reference the same resource, and share no actions in common.
    pub fn is_disjoint(&self, scope: &Scope) -> bool {
        if self.resource != scope.resource {
            return false;
        }
        self.actions.is_disjoint(&scope.actions)
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

        Ok(Scope::new(Resource::new(kind, name), actions.iter()))
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
