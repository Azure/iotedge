// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug)]
pub(crate) struct Error {
    message: String,
    exit_code: i32,
}

impl Error {
    pub fn new(message: impl std::fmt::Display) -> Self {
        Error {
            message: message.to_string(),
            // The default exit code when a failure occurs.
            exit_code: 1,
        }
    }

    pub fn from_err(message: impl std::fmt::Display, err: impl std::fmt::Display) -> Self {
        Error {
            message: format!("{}: {}", message, err),
            exit_code: 1,
        }
    }

    // Clippy wants the parameter to be &dyn std::error::Error, but that's not possible.
    #[allow(clippy::needless_pass_by_value)]
    pub fn settings_err(err: Box<dyn std::error::Error>) -> Self {
        Error {
            message: format!("Failed to load settings: {}", err),

            // A specific exit code when settings could not be read.
            // This prevents systemd from restarting edged until the user fixes the settings.
            exit_code: 153,
        }
    }
}

// Clippy wants an implementation of From<i32> over Into<i32>. However, we don't want to convert
// from any arbitrary i32 because only '1' and '153' have meaning for aziot-edged.
#[allow(clippy::from_over_into)]
impl std::convert::Into<i32> for Error {
    fn into(self) -> i32 {
        self.exit_code
    }
}

impl std::fmt::Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.message)
    }
}
