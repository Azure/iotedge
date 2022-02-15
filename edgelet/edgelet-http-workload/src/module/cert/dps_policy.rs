// Copyright (c) Microsoft. All rights reserved.

use aziot_identity_common_http::get_provisioning_info::Response as ProvisioningInfo;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use test_common::client::IdentityClient;

pub(crate) async fn check_policy(
    client: &IdentityClient,
    csr: &openssl::x509::X509Req,
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

    // Check CSR extended key usage against policy type.
    let extensions = csr.extensions().ok()?;

    match policy.cert_type {
        aziot_identity_common::CertType::Server => {
            // Check that extended key usage has serverAuth set for server certificates.
            let mut has_server_auth = false;

            for extension in extensions {
                if let Some(ext_key_usage) = openssl2::extension::ExtKeyUsage::from_ext(&extension)
                {
                    has_server_auth = ext_key_usage.server_auth;

                    // extKeyUsage should only appear once in the list of extensions, so stop when
                    // it's found.
                    break;
                }
            }

            if !has_server_auth {
                return None;
            }
        }
        aziot_identity_common::CertType::None => return None,
    }

    Some(provisioning_info)
}

#[cfg(test)]
mod tests {
    use super::check_policy;
    use super::{IdentityClient, ProvisioningInfo};

    const CSR_NAME: &str = "testCsr";

    /// Generate a CSR with serverAuth set.
    fn server_cert_csr() -> openssl::x509::X509Req {
        let (csr, _) = test_common::credential::test_csr(
            CSR_NAME,
            Some(|csr| {
                let mut extensions = openssl::stack::Stack::new().unwrap();

                let ext_key_usage = openssl::x509::extension::ExtendedKeyUsage::new()
                    .server_auth()
                    .build()
                    .unwrap();

                extensions.push(ext_key_usage).unwrap();

                csr.add_extensions(&extensions).unwrap();
            }),
        );

        csr
    }

    #[tokio::test]
    #[allow(clippy::field_reassign_with_default)]
    async fn no_cert_policy() {
        let csr = server_cert_csr();

        // Error getting provisioning info.
        let mut client = IdentityClient::default();
        client.get_provisioning_info_ok = false;
        assert!(check_policy(&client, &csr).await.is_none());

        // Provisioning is not DPS-based.
        let mut client = IdentityClient::default();
        client.provisioning_info = ProvisioningInfo::Manual {
            auth: "x509".to_string(),
        };
        assert!(check_policy(&client, &csr).await.is_none());

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
        assert!(check_policy(&client, &csr).await.is_none());
    }

    #[tokio::test]
    async fn ext_key_usage_match() {
        let client = IdentityClient::default();

        // CSR with no extKeyUsage.
        let (csr, _) = test_common::credential::test_csr(CSR_NAME, None);
        assert!(check_policy(&client, &csr).await.is_none());

        // CSR with irrelevant extKeyUsage.
        let (csr, _) = test_common::credential::test_csr(
            CSR_NAME,
            Some(|csr| {
                let mut extensions = openssl::stack::Stack::new().unwrap();

                let ext_key_usage = openssl::x509::extension::ExtendedKeyUsage::new()
                    .client_auth()
                    .build()
                    .unwrap();

                extensions.push(ext_key_usage).unwrap();

                csr.add_extensions(&extensions).unwrap();
            }),
        );
        assert!(check_policy(&client, &csr).await.is_none());

        // CSR with serverAuth extKeyUsage.
        let csr = server_cert_csr();
        assert!(check_policy(&client, &csr).await.is_some());
    }
}
