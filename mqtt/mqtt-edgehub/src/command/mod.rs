mod authorized_identities;
mod disconnect;
mod handler;

pub use authorized_identities::AuthorizedIdentities;
pub use disconnect::Disconnect;
pub use handler::{CommandHandler, ShutdownHandle};

use std::error::Error as StdError;

use mqtt3::ReceivedPublication;

pub trait Command {
    type Error;

    fn topic(&self) -> &str;

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error>;
}

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
