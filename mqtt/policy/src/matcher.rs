use crate::core::Request;

/// Trait to extend `Policy` engine core resource matching.
pub trait ResourceMatcher {
    /// This method is being called by `Policy` when it tries to match a `Request` to
    /// a resource in the policy rules.
    fn do_match(&self, context: &Request, input: &str, policy: &str) -> bool;
}

#[derive(Debug)]
pub struct DefaultResourceMatcher;

impl ResourceMatcher for DefaultResourceMatcher {
    fn do_match(&self, _context: &Request, input: &str, policy: &str) -> bool {
        input == policy
    }
}
