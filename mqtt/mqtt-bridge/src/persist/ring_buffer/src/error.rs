use std::{
    error::Error as StdError,
    fmt::{Display, Formatter, Result},
};

#[derive(Debug)]
pub struct RingBufferError {
    description: String,
    cause: Option<Box<dyn StdError>>,
}

impl RingBufferError {
    pub fn new(description: String, cause: Option<Box<dyn StdError>>) -> Self {
        Self { description, cause }
    }
}

impl Display for RingBufferError {
    fn fmt(&self, f: &mut Formatter<'_>) -> Result {
        write!(f, "message: {}, cause: {:?}", self.description, self.cause)
    }
}

impl StdError for RingBufferError {}
