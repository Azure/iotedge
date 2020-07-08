use std::{error::Error as StdError, fmt};

use mqtt_broker::Error;

pub mod broker;

pub struct Terminate {
    error: Error,
}

impl fmt::Debug for Terminate {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.error)?;
        let mut current: &dyn StdError = &self.error;
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
