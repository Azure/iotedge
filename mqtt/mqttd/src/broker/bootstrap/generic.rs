use std::convert::TryInto;

use mqtt_broker::{Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Error, Server};
use mqtt_broker_core::auth::{
    authenticate_fn_ok, authorize_fn_ok, AuthId, Authorization, Authorizer,
};

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<impl Authorizer>, Error> {
    let broker = BrokerBuilder::default()
        .with_authorizer(authorize_fn_ok(|_| Authorization::Allowed))
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn server<Z>(config: &BrokerConfig, broker: Broker<Z>) -> Result<Server<Z>, Error>
where
    Z: Authorizer + Send + 'static,
{
    // Setup broker with previous state and with configured transports
    let mut server = Server::from_broker(broker);
    for config in config.transports() {
        let new_transport = config.clone().try_into()?;
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));

        server.transport(new_transport, authenticator);
    }

    Ok(server)
}
