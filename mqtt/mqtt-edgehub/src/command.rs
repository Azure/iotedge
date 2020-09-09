use serde::{Deserialize, Serialize};
use serde_json::error::Error as SerdeError;
use std::{collections::HashMap, fmt};
use tracing::{debug, info};

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, ClientId, Error, Message, SystemEvent};

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";
const AUTHORIZED_IDENTITIES_TOPIC: &str = "$internal/identities";

pub trait Command {
    fn topic(&self) -> String;
    fn handle(
        &self,
        broker_handle: &mut BrokerHandle,
        received_publication: &ReceivedPublication,
    ) -> Result<(), HandleEventError>;
}

struct Disconnect {
    topic: String,
}

struct AuthorizedIdentities {
    topic: String,
}

impl Command for Disconnect {
    fn topic(&self) -> String {
        self.topic.clone()
    }

    fn handle(
        &self,
        broker_handle: &mut BrokerHandle,
        received_publication: &ReceivedPublication,
    ) -> Result<(), HandleEventError> {
        let client_id: ClientId = serde_json::from_slice(&received_publication.payload)
            .map_err(HandleEventError::ParseClientId)?;

        info!("received disconnection request for client {}", client_id);

        if let Err(e) = broker_handle.send(Message::System(SystemEvent::ForceClientDisconnect(
            client_id.clone(),
        ))) {
            return Err(HandleEventError::DisconnectSignal(e));
        }

        info!(
            "succeeded sending broker signal to disconnect client{}",
            client_id
        );
        Ok(())
    }
}

impl Command for AuthorizedIdentities {
    fn topic(&self) -> String {
        self.topic.clone()
    }

    fn handle(
        &self,
        broker_handle: &mut BrokerHandle,
        publication: &ReceivedPublication,
    ) -> Result<(), HandleEventError> {
        info!("received authorized identities from edgeHub.");
        let array: Vec<ServiceIdentity> = serde_json::from_slice(&publication.payload)
            .map_err(HandleEventError::ParseAuthorizedIdentities)?;
        debug!("authorized identities: {:?}.", array);
        if let Err(e) = broker_handle.send(Message::System(SystemEvent::AuthorizationUpdate(
            Box::new(array),
        ))) {
            return Err(HandleEventError::SendAuthorizedIdentitiesToBroker(e));
        }

        info!("succeeded sending authorized identity scopes to broker",);
        Ok(())
    }
}

pub fn init_commands() -> HashMap<String, Box<dyn Command + Send>> {
    let mut commands: HashMap<String, Box<dyn Command + Send>> = HashMap::new();
    commands.insert(
        DISCONNECT_TOPIC.to_string(),
        Box::new(Disconnect {
            topic: DISCONNECT_TOPIC.to_string(),
        }),
    );
    commands.insert(
        AUTHORIZED_IDENTITIES_TOPIC.to_string(),
        Box::new(AuthorizedIdentities {
            topic: AUTHORIZED_IDENTITIES_TOPIC.to_string(),
        }),
    );
    commands
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ServiceIdentity {
    #[serde(rename = "Identity")]
    identity: String,
    #[serde(rename = "AuthChain")]
    auth_chain: Option<String>,
}

impl fmt::Display for ServiceIdentity {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match &self.auth_chain {
            Some(auth_chain) => {
                write!(f, "Identity: {}; Auth_Chain: {}", self.identity, auth_chain)
            }
            None => write!(f, "Identity: {}", self.identity),
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum HandleEventError {
    #[error("failed to parse client id from message payload")]
    ParseClientId(SerdeError),

    #[error("failed to parse authorized identities from message payload")]
    ParseAuthorizedIdentities(SerdeError),

    #[error("failed while sending authorized identities to broker")]
    SendAuthorizedIdentitiesToBroker(Error),

    #[error("failed while sending broker signal to disconnect client")]
    DisconnectSignal(Error),
}

#[cfg(test)]
mod tests {
    use crate::command::ServiceIdentity;

    #[test]
    fn deserialize_broker_service_identity() {
        let data = r#"
            [{
                "Identity": "testIdentity",
                "AuthChain": "testAuthChain"
            },
            {
                "Identity": "testIdentity2",
                "AuthChain": "testAuthChain2"
            }]"#;

        let res: Vec<ServiceIdentity> = serde_json::from_str(data).unwrap();
        assert_eq!(res[0].identity, String::from("testIdentity"));
        assert_eq!(
            res[0].auth_chain.as_ref().unwrap(),
            &String::from("testAuthChain")
        );
        assert_eq!(res[1].identity, String::from("testIdentity2"));
        assert_eq!(
            res[1].auth_chain.as_ref().unwrap(),
            &String::from("testAuthChain2")
        );
    }

    #[test]
    fn serialize_broker_service_identity() {
        let id1 = ServiceIdentity {
            identity: String::from("testIdentity"),
            auth_chain: Option::from(String::from("testAuthChain")),
        };
        let id2 = ServiceIdentity {
            identity: String::from("testIdentity2"),
            auth_chain: Option::from(String::from("testAuthChain2")),
        };

        let array: Vec<ServiceIdentity> = vec![id1, id2];
        let result_string = serde_json::to_string(&array).unwrap();
        assert_eq!(result_string, String::from("[{\"Identity\":\"testIdentity\",\"AuthChain\":\"testAuthChain\"},{\"Identity\":\"testIdentity2\",\"AuthChain\":\"testAuthChain2\"}]"));
    }
}
