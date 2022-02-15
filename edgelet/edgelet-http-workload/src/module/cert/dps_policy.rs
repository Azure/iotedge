// Copyright (c) Microsoft. All rights reserved.

use aziot_identity_common_http::get_provisioning_info::Response as ProvisioningInfo;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use test_common::client::IdentityClient;

pub(crate) async fn check_policy(
    client: &IdentityClient,
    cert_type: aziot_identity_common::CertType,
) -> Option<ProvisioningInfo> {
    let provisioning_info = match client.get_provisioning_info().await {
        Ok(provisioning_info) => provisioning_info,
        Err(err) => {
            log::warn!("Could not query provisioning info: {}", err);

            return None;
        }
    };

    // Check for issuance policy.
    // if lets cannot be collapsed because of dependency on cert_policy.
    #[allow(clippy::collapsible_match)]
    let policy = if let ProvisioningInfo::Dps { cert_policy, .. } = &provisioning_info {
        if let Some(policy) = cert_policy {
            policy
        } else {
            return None;
        }
    } else {
        return None;
    };

    // Check cert type against DPS policy.
    if policy.cert_type == cert_type {
        Some(provisioning_info)
    } else {
        None
    }
}

#[cfg(test)]
mod tests {
    use aziot_identity_common::CertType;

    use super::check_policy;
    use super::{IdentityClient, ProvisioningInfo};

    #[tokio::test]
    #[allow(clippy::field_reassign_with_default)]
    async fn no_cert_policy() {
        // Error getting provisioning info.
        let mut client = IdentityClient::default();
        client.get_provisioning_info_ok = false;
        assert!(check_policy(&client, CertType::Server).await.is_none());

        // Provisioning is not DPS-based.
        let mut client = IdentityClient::default();
        client.provisioning_info = ProvisioningInfo::Manual {
            auth: "x509".to_string(),
        };
        assert!(check_policy(&client, CertType::Server).await.is_none());

        // Provisioning info does not have cert issuance policy.
        let mut client = IdentityClient::default();
        if let ProvisioningInfo::Dps {
            auth,
            endpoint,
            scope_id,
            registration_id,
            cert_policy: _,
        } = client.provisioning_info
        {
            client.provisioning_info = ProvisioningInfo::Dps {
                auth,
                endpoint,
                scope_id,
                registration_id,
                cert_policy: None,
            }
        } else {
            panic!("Default identity client has wrong provisioning policy");
        }
        assert!(check_policy(&client, CertType::Server).await.is_none());
    }
}
