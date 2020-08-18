use tracing::info;

pub struct Bridge {
    gateway_hostname: String,
}

impl Bridge {
    pub fn new(gateway_hostname: &str) -> Self {
        Bridge {
            gateway_hostname: gateway_hostname.to_string(),
        }
    }

    pub fn start(self) {
        info!("Starting nested bridge...{:?}", self.gateway_hostname);
    }
}
