#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

pub mod auth;

use auth::AuthId;
use serde::{Deserialize, Serialize};
use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    sync::Arc,
};

#[derive(Clone, Debug, Eq, Hash, PartialEq, Serialize, Deserialize)]
pub struct ClientId(Arc<String>);

impl ClientId {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl<T: Into<String>> From<T> for ClientId {
    fn from(s: T) -> ClientId {
        ClientId(Arc::new(s.into()))
    }
}

impl Display for ClientId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

#[derive(Debug, Clone)]
pub struct ClientInfo {
    peer_addr: SocketAddr,
    auth_id: AuthId,
}

impl ClientInfo {
    pub fn new(peer_addr: SocketAddr, auth_id: impl Into<AuthId>) -> Self {
        Self {
            peer_addr,
            auth_id: auth_id.into(),
        }
    }

    pub fn peer_addr(&self) -> SocketAddr {
        self.peer_addr
    }

    pub fn auth_id(&self) -> &AuthId {
        &self.auth_id
    }
}
