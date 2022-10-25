#[derive(Debug, clap::Args)]
pub(crate) struct Options {
    /// Address of the MQTT server.
    #[arg(long)]
    pub server: std::net::SocketAddr,

    /// Client ID used to identify this application to the server. If not given,
    /// a server-generated ID will be used.
    #[arg(long)]
    pub client_id: Option<String>,

    /// Username used to authenticate with the server, if any.
    #[arg(long)]
    pub username: Option<String>,

    /// Password used to authenticate with the server, if any.
    #[arg(long)]
    pub password: Option<String>,

    /// Maximum back-off time between reconnections to the server, in seconds.
    #[arg(long, default_value = "30", value_parser = duration_from_secs_str)]
    pub max_reconnect_back_off: std::time::Duration,

    /// Keep-alive time advertised to the server, in seconds.
    #[arg(long, default_value = "5", value_parser = duration_from_secs_str)]
    pub keep_alive: std::time::Duration,
}

pub(crate) fn duration_from_secs_str(
    s: &str,
) -> Result<std::time::Duration, <u64 as std::str::FromStr>::Err> {
    Ok(std::time::Duration::from_secs(s.parse()?))
}

pub(crate) fn qos_from_str(s: &str) -> Result<mqtt3::proto::QoS, String> {
    match s {
        "0" | "AtMostOnce" => Ok(mqtt3::proto::QoS::AtMostOnce),
        "1" | "AtLeastOnce" => Ok(mqtt3::proto::QoS::AtLeastOnce),
        "2" | "ExactlyOnce" => Ok(mqtt3::proto::QoS::ExactlyOnce),
        s => Err(format!(
            "unrecognized QoS {:?}: must be one of 0, 1, 2, AtMostOnce, AtLeastOnce, ExactlyOnce",
            s
        )),
    }
}
