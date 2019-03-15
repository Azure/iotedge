// Copyright (c) Microsoft. All rights reserved.

//! iot-hsm-sys
//! Rust FFI to C library interface
//! Based off of <https://github.com/Azure/azure-iot-hsm-c/inc/hsm_client_data.h>
//! Commit id: 11dd77758c6ed1cb06b7c0ba40fdd49bd0d7d3f1
//!
//! Intitial version created through bindgen <https://docs.rs/bindgen/>

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // bindgen-generated docs
    clippy::use_self, // bindgen-generated signatures
)]
#![allow(non_camel_case_types, non_snake_case, non_upper_case_globals)]

use std::os::raw::{c_char, c_int, c_uchar, c_void};

extern "C" {
    pub fn hsm_get_device_ca_alias() -> *const c_char;
    pub fn hsm_get_version() -> *const c_char;
}

#[test]
fn bindgen_test_get_device_alias() {
    let result = unsafe {
        std::ffi::CStr::from_ptr(hsm_get_device_ca_alias())
            .to_string_lossy()
            .into_owned()
    };
    assert_eq!(String::from("device_ca_alias"), result);
}

#[test]
fn bindgen_test_supported_hsm_version() {
    let result = unsafe {
        std::ffi::CStr::from_ptr(hsm_get_version())
            .to_string_lossy()
            .into_owned()
    };
    assert_eq!(String::from("1.0.1"), result);
}

pub type HSM_CLIENT_HANDLE = *mut c_void;
#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CERTIFICATE_TAG {
    _unused: [u8; 0],
}
pub type CERT_INFO_HANDLE = *mut HSM_CERTIFICATE_TAG;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct SIZED_BUFFER_TAG {
    pub buffer: *mut c_uchar,
    pub size: usize,
}
pub type SIZED_BUFFER = SIZED_BUFFER_TAG;

#[test]
fn bindgen_test_layout_SIZED_BUFFER_TAG() {
    assert_eq!(
        ::std::mem::size_of::<SIZED_BUFFER_TAG>(),
        2_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(SIZED_BUFFER_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<SIZED_BUFFER_TAG>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(SIZED_BUFFER_TAG))
    );
    assert_eq!(
        unsafe { &(*(::std::ptr::null::<SIZED_BUFFER_TAG>())).buffer as *const _ as usize },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(SIZED_BUFFER_TAG),
            "::",
            stringify!(buffer)
        )
    );
    assert_eq!(
        unsafe { &(*(::std::ptr::null::<SIZED_BUFFER_TAG>())).size as *const _ as usize },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(SIZED_BUFFER_TAG),
            "::",
            stringify!(size)
        )
    );
}

pub type HSM_CLIENT_CREATE = Option<unsafe extern "C" fn() -> HSM_CLIENT_HANDLE>;
pub type HSM_CLIENT_DESTROY = Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE)>;
pub type HSM_CLIENT_FREE_BUFFER = Option<unsafe extern "C" fn(buffer: *mut c_void)>;
// TPM

pub type HSM_CLIENT_ACTIVATE_IDENTITY_KEY = Option<
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, key: *const c_uchar, key_len: usize) -> c_int,
>;
pub type HSM_CLIENT_GET_ENDORSEMENT_KEY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int,
>;
pub type HSM_CLIENT_GET_STORAGE_ROOT_KEY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int,
>;
pub type HSM_CLIENT_SIGN_WITH_IDENTITY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        data: *const c_uchar,
        data_len: usize,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int,
>;

/// API to derive the SAS key and use it to sign the data. The key
/// should never leave the HSM.
///
/// handle[in] -- A valid HSM client handle
/// data_to_be_signed[in] -- Data to be signed
/// data_to_be_signed_size[in] -- Length of the data to be signed
/// identity[in] -- Identity to be used to derive the SAS key
/// identity_size[in] -- Identity buffer size
/// digest[out]  -- Pointer to a buffer to be filled with the signed digest
/// digest_size[out]  -- Length of signed digest
///
/// @note: If digest is NULL the API will return the size of the required
/// buffer to hold the digest contents.
///
/// Return
/// 0  -- On success
/// Non 0 -- otherwise
pub type HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        data_to_be_signed: *const c_uchar,
        data_to_be_signed_size: usize,
        identity: *const c_uchar,
        identity_size: usize,
        digest: *mut *mut c_uchar,
        digest_size: *mut usize,
    ) -> c_int,
>;

// x509

pub type HSM_CLIENT_GET_CERTIFICATE =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;
pub type HSM_CLIENT_GET_CERT_KEY =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;
pub type HSM_CLIENT_GET_COMMON_NAME =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;

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

pub type CRYPTO_ENCODING_TAG = u32;
pub const CRYPTO_ENCODING_TAG_PEM: CRYPTO_ENCODING_TAG = 0;

pub const PRIVATE_KEY_TYPE_TAG_PRIVATE_KEY_TYPE_UNKNOWN: PRIVATE_KEY_TYPE_TAG = 0;
pub const PRIVATE_KEY_TYPE_TAG_PRIVATE_KEY_TYPE_PAYLOAD: PRIVATE_KEY_TYPE_TAG = 1;
pub const PRIVATE_KEY_TYPE_TAG_PRIVATE_KEY_TYPE_REFERENCE: PRIVATE_KEY_TYPE_TAG = 2;
pub type PRIVATE_KEY_TYPE_TAG = u32;
pub use self::PRIVATE_KEY_TYPE_TAG as PRIVATE_KEY_TYPE;

pub const CERTIFICATE_TYPE_TAG_CERTIFICATE_TYPE_UNKNOWN: CERTIFICATE_TYPE_TAG = 0;
pub const CERTIFICATE_TYPE_TAG_CERTIFICATE_TYPE_CLIENT: CERTIFICATE_TYPE_TAG = 1;
pub const CERTIFICATE_TYPE_TAG_CERTIFICATE_TYPE_SERVER: CERTIFICATE_TYPE_TAG = 2;
pub const CERTIFICATE_TYPE_TAG_CERTIFICATE_TYPE_CA: CERTIFICATE_TYPE_TAG = 3;
pub type CERTIFICATE_TYPE_TAG = u32;
pub use self::CERTIFICATE_TYPE_TAG as CERTIFICATE_TYPE;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CERTIFICATE_PROPS_TAG {
    _unused: [u8; 0],
}
pub type CERT_PROPS_HANDLE = *mut HSM_CERTIFICATE_PROPS_TAG;

extern "C" {
    pub fn cert_properties_create() -> CERT_PROPS_HANDLE;
}
extern "C" {
    pub fn cert_properties_destroy(handle: CERT_PROPS_HANDLE);
}
extern "C" {
    pub fn set_validity_seconds(handle: CERT_PROPS_HANDLE, validity_seconds: u64) -> c_int;
}
extern "C" {
    pub fn get_validity_seconds(handle: CERT_PROPS_HANDLE) -> u64;
}
extern "C" {
    pub fn set_common_name(handle: CERT_PROPS_HANDLE, common_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_common_name(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_country_name(handle: CERT_PROPS_HANDLE, country_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_country_name(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_state_name(handle: CERT_PROPS_HANDLE, country_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_state_name(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_locality(handle: CERT_PROPS_HANDLE, country_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_locality(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_organization_name(handle: CERT_PROPS_HANDLE, country_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_organization_name(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_organization_unit(handle: CERT_PROPS_HANDLE, country_name: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_organization_unit(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_certificate_type(handle: CERT_PROPS_HANDLE, type_: CERTIFICATE_TYPE) -> c_int;
}
extern "C" {
    pub fn get_certificate_type(handle: CERT_PROPS_HANDLE) -> CERTIFICATE_TYPE;
}
extern "C" {
    pub fn set_issuer_alias(handle: CERT_PROPS_HANDLE, issuer_alias: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_issuer_alias(handle: CERT_PROPS_HANDLE) -> *const c_char;
}
extern "C" {
    pub fn set_alias(handle: CERT_PROPS_HANDLE, alias: *const c_char) -> c_int;
}
extern "C" {
    pub fn get_alias(handle: CERT_PROPS_HANDLE) -> *const c_char;
}

extern "C" {
    pub fn set_san_entries(
        handle: CERT_PROPS_HANDLE,
        san_list: *const *const c_char,
        num_entries: usize,
    ) -> c_int;
}

extern "C" {
    pub fn get_san_entries(
        handle: CERT_PROPS_HANDLE,
        num_entries: *mut usize,
    ) -> *const *const c_char;
}

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

pub type HSM_CLIENT_GET_TRUST_BUNDLE =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> CERT_INFO_HANDLE>;

extern "C" {
    /// Creates the certificate information object and initializes the values
    ///
    ///
    /// @param certificate    The certificate in PEM format
    /// @param private_key    A value or reference to the certificate private key
    /// @param pk_len         The length of the private key
    /// @param pk_type        Indicates the type of the private key either the value or reference
    ///
    ///  @return           On success a valid CERT_INFO_HANDLE or NULL on failure
    pub fn certificate_info_create(
        certificate: *const c_char,
        private_key: *const c_void,
        priv_key_len: usize,
        pk_type: PRIVATE_KEY_TYPE,
    ) -> CERT_INFO_HANDLE;
}

extern "C" {
    /// Obtain certificate associated with the supplied CERT_INFO_HANDLE.
    ///
    /// handle[in]   -- Valid handle to certificate
    /// cert_buffer[out]  -- Return parameter containing the cert buffer and size
    /// enc[out]     -- Return parameter containing the encoding of the buffer
    ///
    /// Return
    /// 0  -- On success
    /// Non 0 -- otherwise
    pub fn certificate_info_get_certificate(handle: CERT_INFO_HANDLE) -> *const c_char;
}

extern "C" {
    /// Obtain certificate chain associated with the supplied CERT_INFO_HANDLE.
    /// Ex. [Owner CA -> (intermediate certs)* -> Device CA]
    ///
    /// handle[in]   -- Valid handle to certificate
    /// cert_buffer[out]  -- Return parameter containing the chain buffer and size
    /// enc[out]     -- Return parameter containing the encoding of the buffer
    ///
    /// Return
    /// 0  -- On success
    /// Non 0 -- otherwise
    pub fn certificate_info_get_chain(handle: CERT_INFO_HANDLE) -> *const c_char;
}

extern "C" {
    /// Obtain private key or reference associated with the supplied CERT_INFO_HANDLE.
    ///
    /// handle[in] -- Valid handle to certificate
    /// key_buffer[out]  -- Return parameter containing the key buffer and size
    /// enc[out]   -- Return parameter containing the encoding of the buffer
    /// type[out]  -- Private key type reference or actual payload
    /// will be returned via this parameter
    ///
    /// @note: Private key returned by reference will be encoded as
    /// ASCII and will be null terminated.
    ///
    /// Return
    /// 0  -- On success
    /// Non 0 -- otherwise
    pub fn certificate_info_get_private_key(
        handle: CERT_INFO_HANDLE,
        key_size: *mut usize,
    ) -> *const c_void;
}

extern "C" {
    pub fn certificate_info_get_valid_to(handle: CERT_INFO_HANDLE) -> i64;
}

extern "C" {
    pub fn certificate_info_private_key_type(handle: CERT_INFO_HANDLE) -> PRIVATE_KEY_TYPE;
}

extern "C" {
    pub fn certificate_info_destroy(handle: CERT_INFO_HANDLE);
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_TPM_INTERFACE_TAG {
    pub hsm_client_tpm_create: HSM_CLIENT_CREATE,
    pub hsm_client_tpm_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_activate_identity_key: HSM_CLIENT_ACTIVATE_IDENTITY_KEY,
    pub hsm_client_get_ek: HSM_CLIENT_GET_ENDORSEMENT_KEY,
    pub hsm_client_get_srk: HSM_CLIENT_GET_STORAGE_ROOT_KEY,
    pub hsm_client_sign_with_identity: HSM_CLIENT_SIGN_WITH_IDENTITY,
    pub hsm_client_derive_and_sign_with_identity: HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY,
    pub hsm_client_free_buffer: HSM_CLIENT_FREE_BUFFER,
}

pub type HSM_CLIENT_TPM_INTERFACE = HSM_CLIENT_TPM_INTERFACE_TAG;

impl Default for HSM_CLIENT_TPM_INTERFACE_TAG {
    fn default() -> HSM_CLIENT_TPM_INTERFACE_TAG {
        HSM_CLIENT_TPM_INTERFACE_TAG {
            hsm_client_tpm_create: None,
            hsm_client_tpm_destroy: None,
            hsm_client_activate_identity_key: None,
            hsm_client_get_ek: None,
            hsm_client_get_srk: None,
            hsm_client_sign_with_identity: None,
            hsm_client_derive_and_sign_with_identity: None,
            hsm_client_free_buffer: None,
        }
    }
}

#[test]
fn bindgen_test_layout_HSM_CLIENT_TPM_INTERFACE_TAG() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_TPM_INTERFACE_TAG>(),
        8_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_TPM_INTERFACE_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_TPM_INTERFACE_TAG>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_TPM_INTERFACE_TAG))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_tpm_create
                as *const _ as usize
        },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_tpm_create)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_tpm_destroy
                as *const _ as usize
        },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_tpm_destroy)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>()))
                .hsm_client_activate_identity_key as *const _ as usize
        },
        2_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_activate_identity_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_get_ek as *const _
                as usize
        },
        3_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_ek)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_get_srk as *const _
                as usize
        },
        4_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_srk)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_sign_with_identity
                as *const _ as usize
        },
        5_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_sign_with_identity)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>()))
                .hsm_client_derive_and_sign_with_identity as *const _ as usize
        },
        6_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_derive_and_sign_with_identity)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_free_buffer
                as *const _ as usize
        },
        7_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_TPM_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_free_buffer)
        )
    );
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_X509_INTERFACE_TAG {
    pub hsm_client_x509_create: HSM_CLIENT_CREATE,
    pub hsm_client_x509_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_get_cert: HSM_CLIENT_GET_CERTIFICATE,
    pub hsm_client_get_key: HSM_CLIENT_GET_CERT_KEY,
    pub hsm_client_get_common_name: HSM_CLIENT_GET_COMMON_NAME,
    pub hsm_client_free_buffer: HSM_CLIENT_FREE_BUFFER,
}

pub type HSM_CLIENT_X509_INTERFACE = HSM_CLIENT_X509_INTERFACE_TAG;

impl Default for HSM_CLIENT_X509_INTERFACE_TAG {
    fn default() -> HSM_CLIENT_X509_INTERFACE_TAG {
        HSM_CLIENT_X509_INTERFACE_TAG {
            hsm_client_x509_create: None,
            hsm_client_x509_destroy: None,
            hsm_client_get_cert: None,
            hsm_client_get_key: None,
            hsm_client_get_common_name: None,
            hsm_client_free_buffer: None,
        }
    }
}

#[test]
fn bindgen_test_layout_HSM_CLIENT_X509_INTERFACE_TAG() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_X509_INTERFACE_TAG>(),
        6_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_X509_INTERFACE_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_X509_INTERFACE_TAG>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_X509_INTERFACE_TAG))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_x509_create
                as *const _ as usize
        },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_x509_create)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_x509_destroy
                as *const _ as usize
        },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_x509_destroy)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_get_cert
                as *const _ as usize
        },
        2_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_cert)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_get_key as *const _
                as usize
        },
        3_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_get_common_name
                as *const _ as usize
        },
        4_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_common_name)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE_TAG>())).hsm_client_free_buffer
                as *const _ as usize
        },
        5_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_free_buffer)
        )
    );
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_CRYPTO_INTERFACE_TAG {
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
}
pub type HSM_CLIENT_CRYPTO_INTERFACE = HSM_CLIENT_CRYPTO_INTERFACE_TAG;

impl Default for HSM_CLIENT_CRYPTO_INTERFACE_TAG {
    fn default() -> HSM_CLIENT_CRYPTO_INTERFACE_TAG {
        HSM_CLIENT_CRYPTO_INTERFACE_TAG {
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
        }
    }
}

#[test]
fn bindgen_test_layout_HSM_CLIENT_CRYPTO_INTERFACE_TAG() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>(),
        11_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_crypto_create
                as *const _ as usize
        },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_crypto_create)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_crypto_destroy
                as *const _ as usize
        },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_crypto_destroy)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_get_random_bytes
                as *const _ as usize
        },
        2_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_random_bytes)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>()))
                .hsm_client_create_master_encryption_key as *const _ as usize
        },
        3_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_create_master_encryption_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>()))
                .hsm_client_destroy_master_encryption_key as *const _ as usize
        },
        4_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_destroy_master_encryption_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>()))
                .hsm_client_create_certificate as *const _ as usize
        },
        5_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_create_certificate)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>()))
                .hsm_client_destroy_certificate as *const _ as usize
        },
        6_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_destroy_certificate)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_encrypt_data
                as *const _ as usize
        },
        7_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_encrypt_data)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_decrypt_data
                as *const _ as usize
        },
        8_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_decrypt_data)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_get_trust_bundle
                as *const _ as usize
        },
        9_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_get_trust_bundle)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_CRYPTO_INTERFACE_TAG>())).hsm_client_free_buffer
                as *const _ as usize
        },
        10_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_CRYPTO_INTERFACE_TAG),
            "::",
            stringify!(hsm_client_free_buffer)
        )
    );
}

extern "C" {
    pub fn hsm_client_tpm_interface() -> *const HSM_CLIENT_TPM_INTERFACE;
}
extern "C" {
    pub fn hsm_client_x509_interface() -> *const HSM_CLIENT_X509_INTERFACE;
}
extern "C" {
    pub fn hsm_client_crypto_interface() -> *const HSM_CLIENT_CRYPTO_INTERFACE;
}
extern "C" {
    pub fn hsm_client_x509_init() -> c_int;
}
extern "C" {
    pub fn hsm_client_x509_deinit();
}
extern "C" {
    pub fn hsm_client_tpm_init() -> c_int;
}
extern "C" {
    pub fn hsm_client_tpm_deinit();
}
extern "C" {
    pub fn hsm_client_crypto_init() -> c_int;
}
extern "C" {
    pub fn hsm_client_crypto_deinit();
}
