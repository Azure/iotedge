use crate::{Error, Request};

/// Trait to extend `Policy` variable rules resolution.
pub trait Substituter {
    /// This method is called by `Policy` on every `Request` for every variable identity rule.
    fn visit_identity(&self, value: &str, context: &Request) -> Result<String, Error>;

    /// This method is called by `Policy` on every `Request` for every variable resource rule.
    fn visit_resource(&self, value: &str, context: &Request) -> Result<String, Error>;
}

#[derive(Debug)]
pub struct DefaultSubstituter;

impl Substituter for DefaultSubstituter {
    fn visit_identity(&self, value: &str, _context: &Request) -> Result<String, Error> {
        Ok(value.to_string())
    }

    fn visit_resource(&self, value: &str, _context: &Request) -> Result<String, Error> {
        Ok(value.to_string())
    }
}
