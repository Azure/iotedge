use mqtt_broker_core::auth::{authorize_fn_ok, Authenticator, Authorizer};

pub fn authenticator() -> impl Authenticator {
    imp::authenticator()
}

pub fn authorizer() -> impl Authorizer {
    authorize_fn_ok(|_| true)
}

#[cfg(feature = "edgehub")]
mod imp {
    use mqtt_broker_core::auth::Authenticator;

    pub(super) fn authenticator() -> impl Authenticator {
        let url = "http://localhost:7120/authenticate/".into();
        mqtt_edgehub::authentication::EdgeHubAuthenticator::new(url)
    }
}

#[cfg(not(feature = "edgehub"))]
mod imp {
    use mqtt_broker_core::auth::{authenticate_fn_ok, AuthId, Authenticator};

    pub(super) fn authenticator() -> impl Authenticator {
        authenticate_fn_ok(|_, _| Some(AuthId::Anonymous))
    }
}
