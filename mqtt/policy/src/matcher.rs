use crate::core::Request;

/// Trait to extend `Policy` engine core resource matching.
pub trait ResourceMatcher {
    /// The type of the context associated with the request.
    type Context;

    /// This method is being called by `Policy` when it tries to match a `Request` to
    /// a resource in the policy rules.
    fn do_match(&self, context: &Request<Self::Context>, input: &str, policy: &str) -> bool;
}

/// Default matcher uses equality check for resource matching.
#[derive(Debug)]
pub struct DefaultResourceMatcher;

impl ResourceMatcher for DefaultResourceMatcher {
    type Context = ();

    fn do_match(&self, _context: &Request<Self::Context>, input: &str, policy: &str) -> bool {
        input == policy
    }
}
