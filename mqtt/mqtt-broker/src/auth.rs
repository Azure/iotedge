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
    use mqtt_edgehub::auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthorizer};

    pub(super) fn authenticator() -> EdgeHubAuthenticator {
        let url = "http://localhost:7120/authenticate/".into();
        EdgeHubAuthenticator::new(url)
    }

    pub(super) fn authorizer() -> LocalAuthorizer<EdgeHubAuthorizer> {
        LocalAuthorizer::new(EdgeHubAuthorizer::default())
    }
}

#[cfg(not(feature = "edgehub"))]
mod imp {
    use std::error::Error as StdError;

    use mqtt_broker_core::auth::{
        authenticate_fn_ok, authorize_fn_ok, AuthId, Authenticator, Authorization, Authorizer,
    };

    pub(super) fn authenticator() -> impl Authenticator<Error = Box<dyn StdError>> {
        authenticate_fn_ok(|_| Some(AuthId::Anonymous))
    }

    pub(super) fn authorizer() -> impl Authorizer {
        authorize_fn_ok(|_| Authorization::Allowed)
    }
}
