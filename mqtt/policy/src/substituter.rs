use crate::{Error, Request};

/// Trait to extend `Policy` variable rules resolution.
pub trait Substituter {
    /// The type of the context associated with the request.
    type Context;

    /// This method is called by `Policy` on every `Request` for every variable identity rule.
    fn visit_identity(
        &self,
        value: &str,
        context: &Request<Self::Context>,
    ) -> Result<String, Error>;

    /// This method is called by `Policy` on every `Request` for every variable resource rule.
    fn visit_resource(
        &self,
        value: &str,
        context: &Request<Self::Context>,
    ) -> Result<String, Error>;
}

#[derive(Debug)]
pub struct DefaultSubstituter;

impl Substituter for DefaultSubstituter {
    type Context = ();
    fn visit_identity(
        &self,
        value: &str,
        _context: &Request<Self::Context>,
    ) -> Result<String, Error> {
        Ok(value.to_string())
    }

    fn visit_resource(
        &self,
        value: &str,
        _context: &Request<Self::Context>,
    ) -> Result<String, Error> {
        Ok(value.to_string())
    }
}
