#[macro_export]
macro_rules! create_error {
    ($type_name: ident) => {
        use std::{
            error::Error as StdError,
            fmt::{Display, Formatter, Result},
        };

        #[derive(Debug)]
        pub struct $type_name {
            description: String,
            cause: Option<Box<dyn StdError>>,
        }

        impl $type_name {
            pub fn new(description: String, cause: Option<Box<dyn StdError>>) -> Self {
                Self { description, cause }
            }

            pub fn from_err(message: String, err: Box<dyn StdError>) -> Self {
                $type_name::new(message, Some(err))
            }
        }

        impl Display for $type_name {
            fn fmt(&self, f: &mut Formatter<'_>) -> Result {
                write!(f, "message: {}, cause: {:?}", self.description, self.cause)
            }
        }

        impl StdError for $type_name {}
    };
}
