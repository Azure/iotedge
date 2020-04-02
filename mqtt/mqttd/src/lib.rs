use std::fmt;

use failure::{Context, Fail};
use mqtt_broker::{Error, ErrorKind};

pub mod shutdown;
pub mod snapshot;

pub struct Terminate {
    error: Error,
}

impl fmt::Debug for Terminate {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let fail: &dyn Fail = &self.error;
        write!(f, "{}", fail)?;
        for cause in fail.iter_causes() {
            write!(f, "\n\tcaused by: {}", cause)?;
        }
        Ok(())
    }
}

impl From<Error> for Terminate {
    fn from(error: Error) -> Self {
        Terminate { error }
    }
}

impl From<Context<ErrorKind>> for Terminate {
    fn from(context: Context<ErrorKind>) -> Self {
        Terminate {
            error: context.into(),
        }
    }
}
