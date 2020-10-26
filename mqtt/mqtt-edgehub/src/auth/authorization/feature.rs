use std::{any::Any, env, error::Error as StdError, ffi::OsStr};

use mqtt_broker::auth::{Activity, Authorization, Authorizer};

pub struct FeatureFlagAuthorizer<Z: Authorizer> {
    inner: Z,
    enabled: bool,
}

/// A simple wrapper that, if enabled
/// delegates the call to the inner `Authorizer`,
/// and if disabled, allows all operations.
///
/// Used to isolate `PolicyAuthorizer` with feature flag
/// to simplify end-to-end tests.
impl<Z, E> FeatureFlagAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    pub fn new(feature_flag: impl AsRef<OsStr>, inner: Z) -> Self {
        let enabled = env::var(feature_flag).unwrap_or_default().to_lowercase() == "true";

        Self { inner, enabled }
    }
}

impl<Z, E> Authorizer for FeatureFlagAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        if self.enabled {
            self.inner.authorize(activity)
        } else {
            Ok(Authorization::Allowed)
        }
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.inner.update(update)
    }
}
