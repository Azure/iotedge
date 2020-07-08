use std::convert::TryInto;

use mqtt_broker::{
    Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Error, Server, TransportBuilder,
};
use mqtt_broker_core::auth::Authorizer;
use mqtt_edgehub::auth::{
    EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer,
};

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<LocalAuthorizer<EdgeHubAuthorizer>>, Error> {
    let broker = BrokerBuilder::default()
        .with_authorizer(LocalAuthorizer::new(EdgeHubAuthorizer::default()))
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

        // TODO read from config
        let url = "http://localhost:7120/authenticate/".into();
        let authenticator = EdgeHubAuthenticator::new(url);

        server.transport(new_transport, authenticator);
    }

    // Add additional transport for internal communication
    // TODO read from config
    let new_transport = TransportBuilder::Tcp("localhost:1882".to_string());
    let authenticator = LocalAuthenticator::new();
    server.transport(new_transport, authenticator);

    Ok(server)
}
