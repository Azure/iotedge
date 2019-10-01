// https://github.com/docker/distribution/blob/f656e60de56ff4d83e660af01b44bc595ad09cb6/registry/handlers/app.go#L906
// https://docs.docker.com/registry/spec/auth/scope/

use std::str::FromStr;
use std::string::ToString;

/// Describes a set of actions that are being perfoemd / can be performed on a
/// resource. Can be converted into / from Docker-style scope strings
/// (via .to_string and .parse respectively)
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
}

impl FromStr for Scope {
    type Err = ();

    #[rustfmt::skip] // TODO: implement scope parsing
    fn from_str(_s: &str) -> Result<Scope, ()> {
        // scope                   := resourcescope [ ' ' resourcescope ]*
        // resourcescope           := resourcetype  ":" resourcename  ":" action [ ',' action ]*
        // resourcetype            := resourcetypevalue [ '(' resourcetypevalue ')' ]
        // resourcetypevalue       := /[a-z0-9]+/
        // resourcename            := [ hostname '/' ] component [ '/' component ]*
        // hostname                := hostcomponent ['.' hostcomponent]* [':' port-number]
        // hostcomponent           := /([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])/
        // port-number             := /[0-9]+/
        // action                  := /[a-z]*/
        // component               := alpha-numeric [ separator alpha-numeric ]*
        // alpha-numeric           := /[a-z0-9]+/
        // separator               := /[_.]|__|[-]*/
        unimplemented!()
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Resource {
    kind: ResourceKind,
    name: String,
}

impl Resource {
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

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum Action {
    Pull,
    Push,
    Delete,
    Any,
}

impl ToString for Action {
    fn to_string(&self) -> String {
        use self::Action::*;
        match self {
            Pull => "pull",
            Push => "push",
            Delete => "delete",
            Any => "*",
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
            "*" => Any,
            _ => return Err(()),
        };
        Ok(action)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ResourceKind {
    Repository,
    Registry,
}

impl ToString for ResourceKind {
    fn to_string(&self) -> String {
        use self::ResourceKind::*;
        match self {
            Repository => "repository",
            Registry => "registry",
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
            _ => return Err(()),
        };
        Ok(resource)
    }
}
