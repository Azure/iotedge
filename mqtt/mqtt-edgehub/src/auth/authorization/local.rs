use std::error::Error as StdError;

use mqtt_broker_core::auth::{Activity, Authorizer};

pub struct LocalAuthorizer<Z>(Z);

impl<Z> LocalAuthorizer<Z>
where
    Z: Authorizer,
{
    pub fn new(authorizer: Z) -> Self {
        Self(authorizer)
    }
}

impl<Z, E> Authorizer for LocalAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: Activity) -> Result<bool, Self::Error> {
        self.0.authorize(activity)
    }
}
