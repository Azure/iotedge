// Copyright (c) Microsoft. All rights reserved.
//! iot-hsm-sys
//! Rust FFI to C library interface
//! Based off of https://github.com/Azure/azure-iot-hsm-c/inc/hsm_client_data.h
//! Commit id: b8b23c0e04d60f29e92c6fd56c51c229e61230a0
//!
//! Intitial version created through bindgen https://docs.rs/bindgen/

#![allow(non_upper_case_globals)]
#![allow(non_camel_case_types)]
#![allow(non_snake_case)]

use std::os::raw::{c_char, c_int, c_uchar, c_void};

/// C Handle to HSM instance
pub type HSM_CLIENT_HANDLE = *mut c_void;

// TPM
pub type HSM_CLIENT_CREATE_FN = unsafe extern "C" fn() -> HSM_CLIENT_HANDLE;
pub type HSM_CLIENT_CREATE = Option<HSM_CLIENT_CREATE_FN>;

pub type HSM_CLIENT_DESTROY_FN = unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE);
pub type HSM_CLIENT_DESTROY = Option<HSM_CLIENT_DESTROY_FN>;

pub type HSM_CLIENT_ACTIVATE_IDENTITY_KEY_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, key: *const c_uchar, key_len: usize) -> c_int;
pub type HSM_CLIENT_ACTIVATE_IDENTITY_KEY = Option<HSM_CLIENT_ACTIVATE_IDENTITY_KEY_FN>;

pub type HSM_CLIENT_GET_ENDORSEMENT_KEY_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, key: *mut *mut c_uchar, key_len: *mut usize)
        -> c_int;
pub type HSM_CLIENT_GET_ENDORSEMENT_KEY = Option<HSM_CLIENT_GET_ENDORSEMENT_KEY_FN>;

pub type HSM_CLIENT_GET_STORAGE_ROOT_KEY_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE, key: *mut *mut c_uchar, key_len: *mut usize)
        -> c_int;
pub type HSM_CLIENT_GET_STORAGE_ROOT_KEY = Option<HSM_CLIENT_GET_STORAGE_ROOT_KEY_FN>;

pub type HSM_CLIENT_SIGN_WITH_IDENTITY_FN = unsafe extern "C" fn(
    handle: HSM_CLIENT_HANDLE,
    data: *const c_uchar,
    data_len: usize,
    key: *mut *mut c_uchar,
    key_len: *mut usize,
) -> c_int;
pub type HSM_CLIENT_SIGN_WITH_IDENTITY = Option<HSM_CLIENT_SIGN_WITH_IDENTITY_FN>;

// x509
pub type HSM_CLIENT_GET_CERTIFICATE_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char;
pub type HSM_CLIENT_GET_CERTIFICATE = Option<HSM_CLIENT_GET_CERTIFICATE_FN>;

pub type HSM_CLIENT_GET_CERT_KEY_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char;
pub type HSM_CLIENT_GET_CERT_KEY = Option<HSM_CLIENT_GET_CERT_KEY_FN>;

pub type HSM_CLIENT_GET_COMMON_NAME_FN =
    unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char;
pub type HSM_CLIENT_GET_COMMON_NAME = Option<HSM_CLIENT_GET_COMMON_NAME_FN>;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_TPM_INTERFACE_TAG {
    pub hsm_client_tpm_create: HSM_CLIENT_CREATE,
    pub hsm_client_tpm_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_activate_identity_key: HSM_CLIENT_ACTIVATE_IDENTITY_KEY,
    pub hsm_client_get_ek: HSM_CLIENT_GET_ENDORSEMENT_KEY,
    pub hsm_client_get_srk: HSM_CLIENT_GET_STORAGE_ROOT_KEY,
    pub hsm_client_sign_with_identity: HSM_CLIENT_SIGN_WITH_IDENTITY,
}

/// A function table to implement HSM TPM functions
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
        }
    }
}
#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_X509_INTERFACE_TAG {
    pub hsm_client_x509_create: HSM_CLIENT_CREATE,
    pub hsm_client_x509_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_get_cert: HSM_CLIENT_GET_CERTIFICATE,
    pub hsm_client_get_key: HSM_CLIENT_GET_CERT_KEY,
    pub hsm_client_get_common_name: HSM_CLIENT_GET_COMMON_NAME,
}

/// A function table to implement HSM x509 functions
pub type HSM_CLIENT_X509_INTERFACE = HSM_CLIENT_X509_INTERFACE_TAG;

impl Default for HSM_CLIENT_X509_INTERFACE_TAG {
    fn default() -> HSM_CLIENT_X509_INTERFACE_TAG {
        HSM_CLIENT_X509_INTERFACE_TAG {
            hsm_client_x509_create: None,
            hsm_client_x509_destroy: None,
            hsm_client_get_cert: None,
            hsm_client_get_key: None,
            hsm_client_get_common_name: None,
        }
    }
}
extern "C" {
    pub fn initialize_hsm_system() -> c_int;
}
extern "C" {
    pub fn deinitialize_hsm_system();
}
extern "C" {
    pub fn hsm_client_tpm_interface() -> *const HSM_CLIENT_TPM_INTERFACE;
}
extern "C" {
    pub fn hsm_client_x509_interface() -> *const HSM_CLIENT_X509_INTERFACE;
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

// Tests created by bindgen
#[test]
fn bindgen_test_layout_hsm_client_tpm_interface_tag() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_TPM_INTERFACE_TAG>(),
        6_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_TPM_INTERFACE_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_TPM_INTERFACE_TAG>(),
        1_usize * ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_TPM_INTERFACE_TAG))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_TPM_INTERFACE_TAG>())).hsm_client_tpm_create
                as *const _ as usize
        },
        0usize,
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
        1_usize * ::std::mem::size_of::<usize>(),
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
}

#[test]
fn bindgen_test_layout_hsm_client_x509_interface_tag() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_X509_INTERFACE_TAG>(),
        5_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_X509_INTERFACE_TAG))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_X509_INTERFACE_TAG>(),
        1_usize * ::std::mem::size_of::<usize>(),
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
        1_usize * ::std::mem::size_of::<usize>(),
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
}
