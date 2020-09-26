use std::{any::Any, error::Error as StdError, fmt::Display};

use thiserror::Error;

use mqtt_broker::auth::{Activity, Authorization, Authorizer};

/// `EdgeHubAuthorizer` chains up together `LocalAuthorizer`, `IotHubAuthorizer` and `PolicyAuthorizer`.
/// It encapsulates rules of interaction b/w those authorizers:
/// 1. All local clients must be allowed (see `LocalAuthorizer`)
/// 2. All iothub-specific operations must be checked by `IotHubAuthorizer`
/// 3. If non-iothub-specific operation, or `IotHubAuthorizer` denies the operation,
///    `PolicyAuthorizer` must be called last to make the final decision based on
///     the customer defined policy.
#[derive(Debug)]
pub struct EdgeHubAuthorizer<Z1, Z2, Z3> {
    local: Z1,
    iothub: Z2,
    policy: Z3,
}

impl<Z1, Z2, Z3> EdgeHubAuthorizer<Z1, Z2, Z3>
where
    Z1: Authorizer,
    Z2: Authorizer,
    Z3: Authorizer,
{
    pub fn new(local: Z1, iothub: Z2, policy: Z3) -> Self {
        Self {
            local,
            iothub,
            policy,
        }
    }
}

impl<Z1, Z2, Z3, E1, E2, E3> Authorizer for EdgeHubAuthorizer<Z1, Z2, Z3>
where
    Z1: Authorizer<Error = E1>,
    Z2: Authorizer<Error = E2>,
    Z3: Authorizer<Error = E3>,
    E1: StdError + 'static,
    E2: StdError + 'static,
    E3: StdError + 'static,
{
    type Error = Error;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        let auth = self
            .local
            .authorize(activity)
            .map_err(|e| Error(Box::new(e)))?;
        let auth = match auth {
            auth @ Authorization::Allowed => auth,
            Authorization::Forbidden(_) => self
                .iothub
                .authorize(activity)
                .map_err(|e| Error(Box::new(e)))?,
        };
        let auth = match auth {
            auth @ Authorization::Allowed => auth,
            Authorization::Forbidden(_) => self
                .policy
                .authorize(activity)
                .map_err(|e| Error(Box::new(e)))?,
        };
        Ok(auth)
    }

    fn update(&mut self, _update: Box<dyn Any>) -> Result<(), Self::Error> {
        Ok(())
    }
}

/// Whapper to bring all three different error types from three different authorizers to one.
#[derive(Debug, Error)]
pub struct Error(Box<dyn StdError>);

impl Display for Error {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0)
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use mqtt_broker::auth::{AllowAll, Authorization, Authorizer, DenyAll};

    use crate::auth::authorization::tests;

    use super::EdgeHubAuthorizer;

    #[test]
    fn deny_if_all_deny() {
        let activity = tests::publish_activity("device-1", "device-1", "topic");

        let authorizer = EdgeHubAuthorizer::new(DenyAll, DenyAll, DenyAll);
        assert_matches!(
            authorizer.authorize(&activity),
            Ok(Authorization::Forbidden(_))
        );
    }

    #[test]
    fn allow_if_any_allows() {
        let activity = tests::publish_activity("device-1", "device-1", "topic");

        let authorizer = EdgeHubAuthorizer::new(AllowAll, DenyAll, DenyAll);
        assert_matches!(authorizer.authorize(&activity), Ok(Authorization::Allowed));

        let authorizer = EdgeHubAuthorizer::new(DenyAll, AllowAll, DenyAll);
        assert_matches!(authorizer.authorize(&activity), Ok(Authorization::Allowed));

        let authorizer = EdgeHubAuthorizer::new(DenyAll, DenyAll, AllowAll);
        assert_matches!(authorizer.authorize(&activity), Ok(Authorization::Allowed));
    }
}
