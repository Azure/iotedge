// Copyright (c) Microsoft. All rights reserved.

use std::os::raw::{c_char, c_int, c_uchar};

use crate::{
    CERT_INFO_HANDLE, CERT_PROPS_HANDLE, HSM_CLIENT_CREATE, HSM_CLIENT_DESTROY,
    HSM_CLIENT_FREE_BUFFER, HSM_CLIENT_HANDLE, SIZED_BUFFER,
};

/// API to return the limits of a random number generated from HSM hardware.
/// The API to return a random number is HSM_CLIENT_GET_RANDOM_NUMBER. The
/// number is expected to be a number between a min and max both inclusive.
///
/// handle[in] -- A valid HSM client handle
/// min_random_num[out] -- Min random number limit will be returned via this parameter
/// max_random_num[out] -- Max random number limit will be returned via this parameter
///
/// Return
/// 0  -- On success
/// Non 0 -- otherwise
pub type HSM_CLIENT_GET_RANDOM_BYTES = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        buffer: *mut c_uchar,
        buffer_size: usize,
    ) -> c_int,
>;

/// API to provision a master symmetric encryption key in the HSM.
/// This key will be used to derive all the module and edge runtime
/// specific encryption keys. This is expected to be called once
/// at provisioning.
///
/// handle[in] -- A valid HSM client handle
///
/// Return
/// 0  -- On success
/// Non 0 -- otherwise
pub type HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> c_int>;

/// API to remove the master encryption key from the HSM. This is expected
/// to be called once during de-provisioning of the Edge device.
///
/// @note: Once this is erased, all encrypted data is lost.
///
/// handle[in] -- A valid HSM client handle
///
/// Return
/// 0  -- On success
/// Non 0 -- otherwise
pub type HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> c_int>;

/// API generates a X.509 certificate and private key pair using the supplied
/// certificate properties. Any CA certificates are expected to by issued by
/// the Device CA. Other certificates may be issued by any intermediate CA
/// certs or the device CA certificate.
///
/// @note: Specifying the type of public-private key (ex. RSA, ECC etc.)
/// to be used to generate the certificate is not specified via this API.
/// This works because the key type should be similar to the one used to
/// create the Device CA certificate. The key types must be
/// the same so that the generated certificate could be used for TLS based
/// communication. Since details of the Device CA are not publicly available
/// outside of the HSM/crypto layer there is no need to specify the key type.
///
/// @note: Either the private key contents itself could be returned or a
/// reference to the private key. This can be controlled by setting
/// "exportable_keys" as "true". If this is unspecified keys will not be
/// exported.
///
/// handle[in] -- A valid HSM client handle
/// certificate_props[in]   -- Handle to certificate properties
///
/// Sample code
/// CERT_PROPS_HANDLE props_handle = create_certificate_props();
/// set_validity_in_mins(props_handle, 120);
/// // this could be "$edgeHub", "<Hostname>", "$edgeAgent", Module ID
/// set_common_name(props_handle, common_name);
/// // "server", "client", "ca"
/// set_certificate_type(props_handle, cert_type);
/// // this should be HSM alias of issuer. Ex. "device ca"
/// set_issuer_alias(props_handle, issuer_alias);
/// HSM alias of issuer. Ex. "device ca"
/// Unique alias similar to a file name to associate a reference to HSM resources
/// set_alias(props_handle, unique_id);
///
/// CERT_INFO_HANDLE h = hsm_create_certificate(hsm_handle, props_handle);
/// destroy_certificate_props(props_handle);
///
/// Return
/// CERT_INFO_HANDLE -- Valid non NULL handle on success
/// NULL -- otherwise
pub type HSM_CLIENT_CREATE_CERTIFICATE = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        certificate_props: CERT_PROPS_HANDLE,
    ) -> CERT_INFO_HANDLE,
>;

/// This API deletes any crypto assets associated with the id.
///
/// handle[in]   -- Valid handle to certificate resources
pub type HSM_CLIENT_DESTROY_CERTIFICATE =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, alias: *const c_char)>;

/// API to encrypt a blob of plaintext data and return its corresponding
/// cipher text.
///
/// handle[in]       -- A valid HSM client handle
/// client_id[in]    -- Module or client identity string used in key generation
/// plaintext[in]    -- Plaintext payload to encrypt
/// initialization_vector[in] -- Initialization vector used for any CBC cipher
/// ciphertext[out]  -- Encrypted cipher text
///
/// @note: The encryption/decryption algorithm ex. AES128CBC is not specified
/// via this API and is left up to OEM/HSM implementors.
///
/// Return
/// 0 - Success
/// Non 0 otherwise
pub type HSM_CLIENT_ENCRYPT_DATA = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        client_id: *const SIZED_BUFFER,
        plaintext: *const SIZED_BUFFER,
        initialization_vector: *const SIZED_BUFFER,
        ciphertext: *mut SIZED_BUFFER,
    ) -> c_int,
>;

/// API to decrypt a blob of cipher text data and return its corresponding
/// plain text.
///
/// handle[in]      -- A valid HSM client handle
/// client_id[in]   -- Module or client identity string used in key generation
/// ciphertext[in]  -- Cipher text payload to decrypt
/// initialization_vector[in] -- Initialization vector used for any CBC cipher
/// plaintext[out]  -- Decrypted plain text
///
/// @note: The encryption/decryption algorithm ex. AES128CBC is not specified
/// via this API and is left up to OEM/HSM implementors.
///
/// Return
/// 0 - Success
/// Non 0 otherwise
pub type HSM_CLIENT_DECRYPT_DATA = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        client_id: *const SIZED_BUFFER,
        ciphertext: *const SIZED_BUFFER,
        initialization_vector: *const SIZED_BUFFER,
        plaintext: *mut SIZED_BUFFER,
    ) -> c_int,
>;

/// API to get the Trust Bundle to validate Edge Sever certificate.
///
/// Return
/// Non null CERT_INFO_HANDLE - Success
/// Null otherwise
pub type HSM_CLIENT_GET_TRUST_BUNDLE =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> CERT_INFO_HANDLE>;

pub type HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        alias: *const c_char,
        data: *const c_uchar,
        data_len: usize,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int,
>;

/// API to get the a certificate by alias.
///
/// Return
/// Non null CERT_INFO_HANDLE - Success
/// Null otherwise
pub type HSM_CLIENT_CRYPTO_GET_CERTIFICATE = Option<
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, alias: *const c_char) -> CERT_INFO_HANDLE,
>;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_CRYPTO_INTERFACE {
    pub hsm_client_crypto_create: HSM_CLIENT_CREATE,
    pub hsm_client_crypto_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_get_random_bytes: HSM_CLIENT_GET_RANDOM_BYTES,
    pub hsm_client_create_master_encryption_key: HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY,
    pub hsm_client_destroy_master_encryption_key: HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY,
    pub hsm_client_create_certificate: HSM_CLIENT_CREATE_CERTIFICATE,
    pub hsm_client_destroy_certificate: HSM_CLIENT_DESTROY_CERTIFICATE,
    pub hsm_client_encrypt_data: HSM_CLIENT_ENCRYPT_DATA,
    pub hsm_client_decrypt_data: HSM_CLIENT_DECRYPT_DATA,
    pub hsm_client_get_trust_bundle: HSM_CLIENT_GET_TRUST_BUNDLE,
    pub hsm_client_free_buffer: HSM_CLIENT_FREE_BUFFER,
    pub hsm_client_crypto_sign_with_private_key: HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY,
    pub hsm_client_crypto_get_certificate: HSM_CLIENT_CRYPTO_GET_CERTIFICATE,
}

impl Default for HSM_CLIENT_CRYPTO_INTERFACE {
    fn default() -> HSM_CLIENT_CRYPTO_INTERFACE {
        HSM_CLIENT_CRYPTO_INTERFACE {
            hsm_client_crypto_create: None,
            hsm_client_crypto_destroy: None,
            hsm_client_get_random_bytes: None,
            hsm_client_create_master_encryption_key: None,
            hsm_client_destroy_master_encryption_key: None,
            hsm_client_create_certificate: None,
            hsm_client_destroy_certificate: None,
            hsm_client_encrypt_data: None,
            hsm_client_decrypt_data: None,
            hsm_client_get_trust_bundle: None,
            hsm_client_free_buffer: None,
            hsm_client_crypto_sign_with_private_key: None,
            hsm_client_crypto_get_certificate: None,
        }
    }
}

#[test]
fn bindgen_test_layout_HSM_CLIENT_CRYPTO_INTERFACE() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_CRYPTO_INTERFACE>(),
        13_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_CRYPTO_INTERFACE))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_CRYPTO_INTERFACE>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_CRYPTO_INTERFACE))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_crypto_create
                as *const _ as usize
        },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_crypto_create)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_crypto_destroy
                as *const _ as usize
        },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_crypto_destroy)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_get_random_bytes
                as *const _ as usize
        },
        2_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_get_random_bytes)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>()))
                .hsm_client_create_master_encryption_key as *const _ as usize
        },
        3_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_create_master_encryption_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>()))
                .hsm_client_destroy_master_encryption_key as *const _ as usize
        },
        4_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_destroy_master_encryption_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_create_certificate
                as *const _ as usize
        },
        5_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_create_certificate)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_destroy_certificate
                as *const _ as usize
        },
        6_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_destroy_certificate)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_encrypt_data
                as *const _ as usize
        },
        7_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_encrypt_data)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_decrypt_data
                as *const _ as usize
        },
        8_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_decrypt_data)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_get_trust_bundle
                as *const _ as usize
        },
        9_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_get_trust_bundle)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>())).hsm_client_free_buffer
                as *const _ as usize
        },
        10_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_free_buffer)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>()))
                .hsm_client_crypto_sign_with_private_key as *const _ as usize
        },
        11_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_crypto_sign_with_private_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE>()))
                .hsm_client_crypto_get_certificate as *const _ as usize
        },
        12_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE),
            "::",
            stringify!(hsm_client_crypto_get_certificate)
        )
    );
}
