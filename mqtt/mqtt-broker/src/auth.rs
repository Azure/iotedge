use std::error::Error as StdError;

use mqtt_broker_core::auth::{authorize_fn_ok, Authenticator, Authorizer};

pub fn authenticator() -> impl Authenticator<Error = Box<dyn StdError>> {
    imp::authenticator()
}

pub fn authorizer() -> impl Authorizer {
    authorize_fn_ok(|_| true)
}

#[cfg(feature = "edgehub")]
mod imp {
    use mqtt_edgehub::authentication::EdgeHubAuthenticator;

    pub(super) fn authenticator() -> EdgeHubAuthenticator {
        let url = "http://localhost:7120/authenticate/".into();
        EdgeHubAuthenticator::new(url)
    }
}

#[cfg(not(feature = "edgehub"))]
mod imp {
    use std::error::Error as StdError;

    use mqtt_broker_core::auth::{authenticate_fn_ok, AuthId, Authenticator};

    pub(super) fn authenticator() -> impl Authenticator<Error = Box<dyn StdError>> {
        authenticate_fn_ok(|_, _| Some(AuthId::Anonymous))
    }
}
