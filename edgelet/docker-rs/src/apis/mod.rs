use std::error::Error;
use std::fmt;

#[derive(Debug)]
pub struct ApiError<C, E> {
    pub context: C,
    pub underlying: Option<E>,
}

impl<C, E> ApiError<C, E>
where
    C: fmt::Display + fmt::Debug,
    E: fmt::Display + fmt::Debug,
{
    pub fn add_context(context: C, error: E) -> Self {
        ApiError {
            context,
            underlying: Some(error),
        }
    }

    pub fn with_context(context: C) -> impl FnOnce(E) -> Self {
        |error: E| Self::add_context(context, error)
    }
}

impl<C> ApiError<C, String>
where
    C: fmt::Display + fmt::Debug,
{
    pub fn with_message(message: C) -> Self {
        Self {
            context: message,
            underlying: None,
        }
    }
}

impl<C, E> fmt::Display for ApiError<C, E>
where
    C: fmt::Display,
    E: fmt::Debug,
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(underlying) = &self.underlying {
            write!(f, "Error: {}\n{:?}", self.context, underlying)
        } else {
            write!(f, "Error: {}", self.context)
        }
    }
}

impl<C, E> Error for ApiError<C, E>
where
    C: fmt::Debug + fmt::Display,
    E: fmt::Debug + fmt::Display,
{
}

mod client;
pub use self::client::{DockerApi, DockerApiClient};
pub mod configuration;
