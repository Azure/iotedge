mod authorized_identities;
mod disconnect;
mod handler;

pub use authorized_identities::AuthorizedIdentities;
pub use disconnect::Disconnect;
pub use handler::{CommandHandler, ShutdownHandle};

use std::error::Error as StdError;

use mqtt3::ReceivedPublication;

/// A command trait to be implemented by any MQTT command handler.
pub trait Command {
    /// An error type that occurs when handling command.
    type Error;

    /// A topic to receive command.
    fn topic(&self) -> &str;

    /// Unwraps command from MQTT publication and process it.
    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error>;
}

/// Wrapper to take any command and wrap error type as a trait object.
///
/// We need this to collect all commands in a `HashMap<dyn Command<Error = Box<dyn StdError + Send>>`.
/// Otherwise there is no good way to bring different commands to the same
/// trait object type with associated trait object error type.
pub struct DynCommand<C> {
    inner: C,
}

impl<C, E> Command for DynCommand<C>
where
    C: Command<Error = E>,
    E: StdError + Into<Box<dyn StdError>> + 'static,
{
    type Error = Box<dyn StdError>;

    fn topic(&self) -> &str {
        self.inner.topic()
    }

    fn handle(&mut self, publication: &mqtt3::ReceivedPublication) -> Result<(), Self::Error> {
        self.inner.handle(publication)?;
        Ok(())
    }
}

impl<C> From<C> for DynCommand<C> {
    fn from(command: C) -> Self {
        Self { inner: command }
    }
}
