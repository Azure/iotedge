// Copyright (c) Microsoft. All rights reserved.

pub(crate) enum CertKey {
    Rsa(u32),
    Ec(openssl::nid::Nid),
}

impl CertKey {
    pub fn generate(
        self,
    ) -> Result<
        (
            openssl::pkey::PKey<openssl::pkey::Private>,
            openssl::pkey::PKey<openssl::pkey::Public>,
        ),
        openssl::error::ErrorStack,
    > {
        let private_key = match self {
            CertKey::Rsa(key_length) => {
                let rsa = openssl::rsa::Rsa::generate(key_length)?;

                openssl::pkey::PKey::from_rsa(rsa)?
            }

            CertKey::Ec(curve) => {
                let group = openssl::ec::EcGroup::from_curve_name(curve)?;

                let private_key = openssl::ec::EcKey::generate(&group)?;

                openssl::pkey::PKey::from_ec_key(private_key)?
            }
        };

        let public_key = private_key.public_key_to_pem()?;
        let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key)?;

        Ok((private_key, public_key))
    }
}
