use std::fmt;

use mqtt_broker::Error;

pub mod shutdown;
pub mod snapshot;

pub struct Terminate {
    error: Error,
}

impl fmt::Debug for Terminate {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.error)?;
        let mut current: &dyn std::error::Error = &self.error;
        while let Some(source) = current.source() {
            write!(f, "\n\tcaused by: {}", source)?;
            current = source;
        }
        Ok(())
    }
}

impl From<Error> for Terminate {
    fn from(error: Error) -> Self {
        Terminate { error }
    }
}
