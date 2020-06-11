mod credentials;
pub use self::credentials::Credentials;
mod error_response;
pub use self::error_response::ErrorResponse;
mod identity_result;
pub use self::identity_result::IdentityResult;
mod identity_spec;
pub use self::identity_spec::IdentitySpec;

// TODO(farcaller): sort out files
pub struct File;
