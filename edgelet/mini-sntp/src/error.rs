#[derive(Debug)]
pub struct Error(failure::Context<ErrorKind>);

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.0.get_context()
    }
}

impl std::fmt::Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.0.fmt(f)
    }
}

impl failure::Fail for Error {
    fn cause(&self) -> Option<&dyn failure::Fail> {
        self.0.cause()
    }

    fn backtrace(&self) -> Option<&failure::Backtrace> {
        self.0.backtrace()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error(failure::Context::new(kind))
    }
}

impl From<failure::Context<ErrorKind>> for Error {
    fn from(context: failure::Context<ErrorKind>) -> Self {
        Error(context)
    }
}

#[derive(Debug)]
pub enum ErrorKind {
    BadServerResponse(BadServerResponseReason),
    BindLocalSocket,
    ReceiveServerResponse(std::io::Error),
    ResolveNtpPoolHostname(Option<std::io::Error>),
    SendClientRequest(std::io::Error),
    SetReadTimeoutOnSocket,
    SetWriteTimeoutOnSocket,
}

impl std::fmt::Display for ErrorKind {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ErrorKind::BadServerResponse(reason) => {
                write!(f, "could not parse NTP server response: {}", reason)
            }
            ErrorKind::BindLocalSocket => write!(f, "could not bind local UDP socket"),
            ErrorKind::ReceiveServerResponse(err) => {
                write!(f, "could not receive NTP server response: {}", err)
            }
            ErrorKind::ResolveNtpPoolHostname(Some(err)) => {
                write!(f, "could not resolve NTP pool hostname: {}", err)
            }
            ErrorKind::ResolveNtpPoolHostname(None) => {
                write!(f, "could not resolve NTP pool hostname: no addresses found")
            }
            ErrorKind::SendClientRequest(err) => {
                write!(f, "could not send SNTP client request: {}", err)
            }
            ErrorKind::SetReadTimeoutOnSocket => {
                write!(f, "could not set read timeout on local UDP socket")
            }
            ErrorKind::SetWriteTimeoutOnSocket => {
                write!(f, "could not set write timeout on local UDP socket")
            }
        }
    }
}

impl failure::Fail for ErrorKind {
    fn cause(&self) -> Option<&dyn failure::Fail> {
        #[allow(clippy::match_same_arms)]
        match self {
            ErrorKind::BadServerResponse(_) => None,
            ErrorKind::BindLocalSocket => None,
            ErrorKind::ReceiveServerResponse(err) => Some(err),
            ErrorKind::ResolveNtpPoolHostname(Some(err)) => Some(err),
            ErrorKind::ResolveNtpPoolHostname(None) => None,
            ErrorKind::SendClientRequest(err) => Some(err),
            ErrorKind::SetReadTimeoutOnSocket => None,
            ErrorKind::SetWriteTimeoutOnSocket => None,
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub enum BadServerResponseReason {
    LeapIndicator(u8),
    OriginateTimestamp {
        expected: chrono::DateTime<chrono::Utc>,
        actual: chrono::DateTime<chrono::Utc>,
    },
    Mode(u8),
    VersionNumber(u8),
}

impl std::fmt::Display for BadServerResponseReason {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            BadServerResponseReason::LeapIndicator(leap_indicator) => {
                write!(f, "invalid value of leap indicator {}", leap_indicator)
            }
            BadServerResponseReason::OriginateTimestamp { expected, actual } => write!(
                f,
                "expected originate timestamp to be {} but it was {}",
                expected, actual
            ),
            BadServerResponseReason::Mode(mode) => {
                write!(f, "expected mode to be 4 but it was {}", mode)
            }
            BadServerResponseReason::VersionNumber(version_number) => write!(
                f,
                "expected version number to be 3 but it was {}",
                version_number
            ),
        }
    }
}
