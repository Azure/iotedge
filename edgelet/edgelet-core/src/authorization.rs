// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

#[derive(Clone, Debug, PartialEq)]
pub struct ModuleId(String);

impl fmt::Display for ModuleId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl<T> PartialEq<T> for ModuleId
where
    T: AsRef<str>,
{
    fn eq(&self, other: &T) -> bool {
        self.0 == other.as_ref()
    }
}

impl<T> From<T> for ModuleId
where
    T: Into<String>,
{
    fn from(name: T) -> Self {
        ModuleId(name.into())
    }
}

#[derive(Clone, Debug, PartialEq)]
pub enum AuthId {
    None,
    Any,
    Value(ModuleId),
}

impl fmt::Display for AuthId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AuthId::None => write!(f, "none"),
            AuthId::Any => write!(f, "any"),
            AuthId::Value(auth_id) => write!(f, "{}", auth_id),
        }
    }
}

#[derive(Debug)]
pub enum Policy {
    Anonymous,
    Caller,
    Module(&'static str),
}

impl Policy {
    pub fn should_authenticate<'a>(&self, name: Option<&'a str>) -> (bool, Option<&'a str>) {
        let name = name.map(|n| n.trim_start_matches('$'));
        match self {
            Policy::Anonymous => (false, None),
            Policy::Caller => (true, name),
            Policy::Module(ref expected_name) => (true, Some(expected_name)),
        }
    }

    pub fn authorize(&self, name: Option<&str>, auth_id: AuthId) -> bool {
        let name = name.map(|n| n.trim_start_matches('$'));
        match self {
            Policy::Anonymous => true,
            Policy::Caller => Policy::auth_caller(name, auth_id),
            Policy::Module(ref expected_name) => Policy::auth_caller(Some(expected_name), auth_id),
        }
    }

    fn auth_caller(name: Option<&str>, auth_id: AuthId) -> bool {
        name.map_or_else(
            || false,
            |name| match auth_id {
                AuthId::None => false,
                AuthId::Any => true,
                AuthId::Value(module) => module == name,
            },
        )
    }
}

#[cfg(test)]
mod tests {
    use crate::{AuthId, Policy};

    #[test]
    fn should_authorize_anonymous() {
        let policy = Policy::Anonymous;
        assert!(policy.authorize(None, AuthId::None));
    }

    #[test]
    fn should_authorize_caller() {
        let policy = Policy::Caller;
        assert!(policy.authorize(Some("abc"), AuthId::Value("abc".into())));
    }

    #[test]
    fn should_authorize_system_caller() {
        let policy = Policy::Caller;
        assert!(policy.authorize(Some("$edgeAgent"), AuthId::Value("edgeAgent".into()),));
    }

    #[test]
    fn should_reject_caller_without_name() {
        let policy = Policy::Caller;
        assert!(!policy.authorize(None, AuthId::Value("abc".into())));
    }

    #[test]
    fn should_reject_caller_with_different_name() {
        let policy = Policy::Caller;
        assert!(!policy.authorize(Some("xyz"), AuthId::Value("abc".into())));
    }

    #[test]
    fn should_authorize_module() {
        let policy = Policy::Module("abc");
        assert!(policy.authorize(None, AuthId::Value("abc".into())));
    }

    #[test]
    fn should_reject_module_whose_name_does_not_match_policy() {
        let policy = Policy::Module("abc");
        assert!(!policy.authorize(None, AuthId::Value("xyz".into())));
    }
}
