// Copyright (c) Microsoft. All rights reserved.

use std::{io, mem, ptr};

use failure::{Fail, ResultExt};
use openssl::pkey::{PKeyRef, Private};
use openssl::stack::StackRef;
use openssl::x509::{X509Ref, X509};
use winapi::shared::{bcrypt, minwindef};
use winapi::um::{ncrypt, wincrypt};

use crate::{Error, ErrorKind};

// On Windows, we *could* use `openssl::pkcs12::Pkcs12Builder` like we do for non-Windows because we ship with openssl anyway,
// but using the subsequent PKCS#12 blob with `native_tls::Identity::from_pkcs12` creates the private key file on disk
// under `C:\ProgramData\Microsoft\Crypto\RSA\S-1-5-18`, ie LocalSystem's persisted keys directory.
// Worse, dropping the `Tls*` types doesn't delete the key file, and retrying the TLS connection with the same key creates a new file,
// so the directory keeps getting new files over time.
//
// Therefore, rather than use openssl, we use winapi to construct the PKCS#12 blob instead, in a way that doesn't have this problem.
// The key is to mark the private key as "exportable", which is a Windows-specific attribute and is thus not doable with openssl.
#[cfg(windows)]
#[allow(
    // Conversions between usize and u* winapi types
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
)]
pub(crate) fn make_pkcs12(
    identity_cert: &X509Ref,
    key: &PKeyRef<Private>,
    ca_certs: &StackRef<X509>,
) -> Result<Vec<u8>, Error> {
    unsafe {
        let cert_store = {
            let cert_store =
                wincrypt::CertOpenStore(wincrypt::CERT_STORE_PROV_MEMORY, 0, 0, 0, ptr::null());
            if cert_store.is_null() {
                return Err(io::Error::last_os_error()
                    .context(ErrorKind::IdentityCertificate)
                    .into());
            }

            CertStore(cert_store)
        };

        let cert_context = {
            let identity_cert = identity_cert
                .to_der()
                .context(ErrorKind::IdentityCertificate)?;

            let mut cert_context = ptr::null();
            assert_true(wincrypt::CertAddEncodedCertificateToStore(
                cert_store.0,
                wincrypt::X509_ASN_ENCODING,
                identity_cert.as_ptr(),
                identity_cert.len() as _,
                wincrypt::CERT_STORE_ADD_NEW,
                &mut cert_context,
            ))?;

            CertContext(cert_context)
        };

        for cert in ca_certs {
            let cert = cert.to_der().context(ErrorKind::IdentityCertificate)?;

            let mut cert_context = ptr::null();
            assert_true(wincrypt::CertAddEncodedCertificateToStore(
                cert_store.0,
                wincrypt::X509_ASN_ENCODING,
                cert.as_ptr(),
                cert.len() as _,
                wincrypt::CERT_STORE_ADD_NEW,
                &mut cert_context,
            ))?;

            let _ = CertContext(cert_context);
        }

        let crypto_provider = {
            let mut crypto_provider = 0;
            let err = ncrypt::NCryptOpenStorageProvider(
                &mut crypto_provider,
                winapi2::um::ncrypt::MS_KEY_STORAGE_PROVIDER.as_ptr(),
                0,
            );
            if !bcrypt::BCRYPT_SUCCESS(err) {
                return Err(
                    io::Error::new(io::ErrorKind::Other, format!("0x{:08X}", err))
                        .context(ErrorKind::IdentityCertificate)
                        .into(),
                );
            }

            NCryptObject(crypto_provider)
        };

        let private_key = {
            let private_key_encoded_buf = key
                .private_key_to_der()
                .context(ErrorKind::IdentityCertificate)?;

            let private_key_decoded_buf = {
                let mut private_key_decoded_buf_len = 0;
                assert_true(wincrypt::CryptDecodeObjectEx(
                    wincrypt::X509_ASN_ENCODING,
                    wincrypt::PKCS_RSA_PRIVATE_KEY,
                    private_key_encoded_buf.as_ptr(),
                    private_key_encoded_buf.len() as _,
                    0,
                    ptr::null_mut(),
                    ptr::null_mut(),
                    &mut private_key_decoded_buf_len,
                ))?;

                let mut private_key_decoded_buf = vec![0_u8; private_key_decoded_buf_len as _];
                assert_true(wincrypt::CryptDecodeObjectEx(
                    wincrypt::X509_ASN_ENCODING,
                    wincrypt::PKCS_RSA_PRIVATE_KEY,
                    private_key_encoded_buf.as_ptr(),
                    private_key_encoded_buf.len() as _,
                    0,
                    ptr::null_mut(),
                    private_key_decoded_buf.as_mut_ptr() as _,
                    &mut private_key_decoded_buf_len,
                ))?;
                private_key_decoded_buf.resize(private_key_decoded_buf_len as _, 0);

                private_key_decoded_buf
            };

            let mut private_key = 0;
            let err = ncrypt::NCryptImportKey(
                crypto_provider.0,
                0,
                winapi2::shared::bcrypt::LEGACY_RSAPRIVATE_BLOB.as_ptr(),
                ptr::null(),
                &mut private_key,
                private_key_decoded_buf.as_ptr() as _,
                private_key_decoded_buf.len() as _,
                ncrypt::NCRYPT_SILENT_FLAG,
            );
            if !bcrypt::BCRYPT_SUCCESS(err) {
                return Err(
                    io::Error::new(io::ErrorKind::Other, format!("0x{:08X}", err))
                        .context(ErrorKind::IdentityCertificate)
                        .into(),
                );
            }

            NCryptObject(private_key)
        };

        {
            let export_policy_property_value = ncrypt::NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;
            let err = ncrypt::NCryptSetProperty(
                private_key.0,
                winapi2::um::ncrypt::NCRYPT_EXPORT_POLICY_PROPERTY.as_ptr(),
                &export_policy_property_value as *const _ as _,
                mem::size_of_val(&export_policy_property_value) as _,
                0,
            );
            if !bcrypt::BCRYPT_SUCCESS(err) {
                return Err(
                    io::Error::new(io::ErrorKind::Other, format!("0x{:08X}", err))
                        .context(ErrorKind::IdentityCertificate)
                        .into(),
                );
            }
        }

        let mut private_key_context = wincrypt::CERT_KEY_CONTEXT {
            cbSize: 0,
            u: mem::zeroed(),
            dwKeySpec: wincrypt::CERT_NCRYPT_KEY_SPEC,
        };
        private_key_context.cbSize = mem::size_of_val(&private_key_context) as _;
        *private_key_context.u.hNCryptKey_mut() = private_key.0;

        assert_true(wincrypt::CertSetCertificateContextProperty(
            cert_context.0,
            wincrypt::CERT_KEY_CONTEXT_PROP_ID,
            0,
            &private_key_context as *const _ as _,
        ))?;

        let mut private_key_data = wincrypt::CRYPT_DATA_BLOB {
            cbData: 0,
            pbData: ptr::null_mut(),
        };

        assert_true(wincrypt::PFXExportCertStoreEx(
            cert_store.0,
            &mut private_key_data,
            ptr::null(),
            ptr::null_mut(),
            wincrypt::EXPORT_PRIVATE_KEYS,
        ))?;

        let mut result = vec![0_u8; private_key_data.cbData as _];

        private_key_data.pbData = result.as_mut_ptr();

        assert_true(wincrypt::PFXExportCertStoreEx(
            cert_store.0,
            &mut private_key_data,
            ptr::null(),
            ptr::null_mut(),
            wincrypt::EXPORT_PRIVATE_KEYS,
        ))?;

        result.resize(private_key_data.cbData as _, 0);

        Ok(result)
    }
}

// These constants also exist in winapi, but are defined as a &[CHAR] which makes them useless for the APIs that they're used with.
//
// Ref: https://github.com/retep998/winapi-rs/pull/630
#[cfg(windows)]
mod winapi2 {
    macro_rules! wide {
        ($($expr:expr)*) => {
            &[$( ($expr as u16) ),* , 0_u16]
        };
    }

    pub mod shared {
        pub mod bcrypt {
            use winapi::um::winnt::WCHAR;

            #[rustfmt::skip]
            pub const LEGACY_RSAPRIVATE_BLOB: &[WCHAR] =
                wide!('C''A''P''I''P''R''I''V''A''T''E''B''L''O''B');
        }
    }

    pub mod um {
        pub mod ncrypt {
            use winapi::um::winnt::WCHAR;

            #[rustfmt::skip]
            pub const MS_KEY_STORAGE_PROVIDER: &[WCHAR] =
                wide!('M''i''c''r''o''s''o''f''t'' ''S''o''f''t''w''a''r''e'' ''K''e''y'' ''S''t''o''r''a''g''e'' ''P''r''o''v''i''d''e''r');

            #[rustfmt::skip]
            pub const NCRYPT_EXPORT_POLICY_PROPERTY: &[WCHAR] =
                wide!('E''x''p''o''r''t'' ''P''o''l''i''c''y');
        }
    }
}

struct CertStore(wincrypt::HCERTSTORE);

impl Drop for CertStore {
    fn drop(&mut self) {
        unsafe {
            wincrypt::CertCloseStore(self.0, 0);
        }
    }
}

struct CertContext(wincrypt::PCCERT_CONTEXT);

impl Drop for CertContext {
    fn drop(&mut self) {
        unsafe {
            wincrypt::CertFreeCertificateContext(self.0);
        }
    }
}

struct NCryptObject(ncrypt::NCRYPT_HANDLE);

impl Drop for NCryptObject {
    fn drop(&mut self) {
        unsafe {
            ncrypt::NCryptFreeObject(self.0);
        }
    }
}

fn assert_true(result: minwindef::BOOL) -> Result<(), Error> {
    if result == minwindef::TRUE {
        Ok(())
    } else {
        Err(io::Error::last_os_error()
            .context(ErrorKind::IdentityCertificate)
            .into())
    }
}
