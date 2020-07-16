use std::{fs, future::Future, path::Path};

use anyhow::{anyhow, Context, Result};
use native_tls::Identity;

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

pub async fn start_server<Z, F>(
    config: &BrokerConfig,
    broker: Broker<Z>,
    shutdown_signal: F,
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()> + Unpin,
{
    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.transports().tcp() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.tcp(tcp.addr(), authenticator);
    }

    if let Some(tls) = config.transports().tls() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        let identity = load_server_certificate(tls.cert_path())?;
        server.tls(tls.addr(), identity, authenticator)?;
    }

    let state = server.serve(shutdown_signal).await?;
    Ok(state)
}

fn load_server_certificate(path: Option<&Path>) -> Result<native_tls::Identity> {
    let path = path.ok_or(anyhow!("missing path to server certificate"))?;

    let cert_buffer = fs::read(&path).context(anyhow!(
        "unable to read server certificate {}",
        path.display()
    ))?;

    let identity = Identity::from_pkcs12(&cert_buffer, "").context(anyhow!(
        "unable to decode server certificate {}",
        path.display()
    ))?;
    Ok(identity)
}
