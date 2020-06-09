use std::error::Error as StdError;

use mqtt_broker_core::auth::{Authenticator, Authorizer};

pub fn authenticator() -> impl Authenticator<Error = Box<dyn StdError>> {
    imp::authenticator()
}

pub fn authorizer() -> impl Authorizer {
    imp::authorizer()
}

#[cfg(feature = "edgehub")]
mod imp {
    use mqtt_broker_core::auth::{authorize_fn_ok, Authorizer};
    use mqtt_edgehub::auth::{EdgeHubAuthenticator, LocalAuthorizer};

    pub(super) fn authenticator() -> EdgeHubAuthenticator {
        let url = "http://localhost:7120/authenticate/".into();
        EdgeHubAuthenticator::new(url)
    }

    pub(super) fn authorizer() -> impl Authorizer {
        LocalAuthorizer::new(authorize_fn_ok(|_| true))
    }
}

#[cfg(not(feature = "edgehub"))]
mod imp {
    use std::error::Error as StdError;

    use mqtt_broker_core::auth::{
        authenticate_fn_ok, authorize_fn_ok, AuthId, Authenticator, Authorizer,
    };

    pub(super) fn authenticator() -> impl Authenticator<Error = Box<dyn StdError>> {
        authenticate_fn_ok(|_| Some(AuthId::Anonymous))
    }

    pub(super) fn authorizer() -> impl Authorizer {
        authorize_fn_ok(|_| true)
    }
}
