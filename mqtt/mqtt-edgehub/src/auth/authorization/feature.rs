use std::{env, error::Error as StdError};

use mqtt_broker::auth::{Authorization, Authorizer};

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
    pub fn new(feature_flag: String, inner: Z) -> Self {
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

    fn authorize(
        &self,
        activity: &mqtt_broker::auth::Activity,
    ) -> Result<mqtt_broker::auth::Authorization, Self::Error> {
        if self.enabled {
            self.inner.authorize(activity)
        } else {
            Ok(Authorization::Allowed)
        }
    }

    fn update(&mut self, update: Box<dyn std::any::Any>) -> Result<(), Self::Error> {
        self.inner.update(update)
    }
}
