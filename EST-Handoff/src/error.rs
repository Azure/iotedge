use std::fmt;

#[derive(Clone, Debug)]
pub struct Error {
    pub message: String,
}

impl Error {
    pub fn prefix(&self, prefix: &str) -> Self {
        Error {
            message: format!("{}: {}", prefix, &self.message),
        }
    }
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        write!(f, "{}", self.message)
    }
}

impl From<&str> for Error {
    fn from(error: &str) -> Self {
        Error {
            message: error.to_string(),
        }
    }
}

impl From<String> for Error {
    fn from(error: String) -> Self {
        error.as_str().into()
    }
}

impl From<&String> for Error {
    fn from(error: &String) -> Self {
        error.as_str().into()
    }
}

impl From<std::io::Error> for Error {
    fn from(error: std::io::Error) -> Self {
        format!("IO error: {}", error).into()
    }
}

impl From<std::string::FromUtf8Error> for Error {
    fn from(error: std::string::FromUtf8Error) -> Self {
        format!("UTF-8 error: {}", error).into()
    }
}

impl From<openssl::error::ErrorStack> for Error {
    fn from(error: openssl::error::ErrorStack) -> Self {
        format!("{}", error).into()
    }
}