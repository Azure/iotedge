// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Error {
    message: String,
    exit_code: i32,
}

impl Error {
    pub fn new(message: String) -> Self {
        Error {
            message,
            // The default exit code when a failure occurs.
            exit_code: 1,
        }
    }
}

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

impl std::convert::From<edgelet_docker::LoadSettingsError> for Error {
    fn from(_err: edgelet_docker::LoadSettingsError) -> Self {
        Error {
            message: "Failed to load settings".to_string(), // TODO: make this more detailed

            // A specific exit code when settings could not be read.
            // This prevents systemd from restarting edged until the user fixes the settings.
            exit_code: 153,
        }
    }
}
