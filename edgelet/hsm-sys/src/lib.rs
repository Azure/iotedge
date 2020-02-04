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
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self // bindgen-generated signatures
)]
#![allow(non_camel_case_types, non_snake_case, non_upper_case_globals)]

use std::os::raw::{c_char, c_int, c_uchar, c_void};

mod crypto;
mod tpm;
mod x509;

pub use crypto::HSM_CLIENT_CRYPTO_INTERFACE;
pub use tpm::HSM_CLIENT_TPM_INTERFACE;
pub use x509::HSM_CLIENT_X509_INTERFACE;

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
    assert_eq!(String::from("1.0.3"), result);
}

pub type HSM_CLIENT_HANDLE = *mut c_void;
#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct CERT_DATA_INFO {
    _unused: [u8; 0],
}
pub type CERT_INFO_HANDLE = *mut CERT_DATA_INFO;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct SIZED_BUFFER {
    pub buffer: *mut c_uchar,
    pub size: usize,
}

#[test]
fn bindgen_test_layout_SIZED_BUFFER() {
    assert_eq!(
        ::std::mem::size_of::<SIZED_BUFFER>(),
        2_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(SIZED_BUFFER))
    );
    assert_eq!(
        ::std::mem::align_of::<SIZED_BUFFER>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(SIZED_BUFFER))
    );
    assert_eq!(
        unsafe { &(*(::std::ptr::null::<SIZED_BUFFER>())).buffer as *const _ as usize },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(SIZED_BUFFER),
            "::",
            stringify!(buffer)
        )
    );
    assert_eq!(
        unsafe { &(*(::std::ptr::null::<SIZED_BUFFER>())).size as *const _ as usize },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(SIZED_BUFFER),
            "::",
            stringify!(size)
        )
    );
}

pub type HSM_CLIENT_CREATE = Option<unsafe extern "C" fn() -> HSM_CLIENT_HANDLE>;
pub type HSM_CLIENT_DESTROY = Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE)>;
pub type HSM_CLIENT_FREE_BUFFER = Option<unsafe extern "C" fn(buffer: *mut c_void)>;

pub type CRYPTO_ENCODING = u32;
pub const CRYPTO_ENCODING_PEM: CRYPTO_ENCODING = 0;

pub const PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_UNKNOWN: PRIVATE_KEY_TYPE = 0;
pub const PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_PAYLOAD: PRIVATE_KEY_TYPE = 1;
pub const PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_REFERENCE: PRIVATE_KEY_TYPE = 2;
pub type PRIVATE_KEY_TYPE = u32;

pub const CERTIFICATE_TYPE_CERTIFICATE_TYPE_UNKNOWN: CERTIFICATE_TYPE = 0;
pub const CERTIFICATE_TYPE_CERTIFICATE_TYPE_CLIENT: CERTIFICATE_TYPE = 1;
pub const CERTIFICATE_TYPE_CERTIFICATE_TYPE_SERVER: CERTIFICATE_TYPE = 2;
pub const CERTIFICATE_TYPE_CERTIFICATE_TYPE_CA: CERTIFICATE_TYPE = 3;
pub type CERTIFICATE_TYPE = u32;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CERT_PROPS {
    _unused: [u8; 0],
}
pub type CERT_PROPS_HANDLE = *mut HSM_CERT_PROPS;

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

extern "C" {
    pub fn certificate_info_get_common_name(handle: CERT_INFO_HANDLE) -> *const c_char;
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
    pub fn hsm_client_x509_init(auto_generated_cert_lifetime: u64) -> c_int;
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
    pub fn hsm_client_crypto_init(auto_generated_cert_lifetime: u64) -> c_int;
}
extern "C" {
    pub fn hsm_client_crypto_deinit();
}
