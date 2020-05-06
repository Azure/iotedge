// Copyright (c) Microsoft. All rights reserved.

use std::os::raw::{c_char, c_int, c_uchar};

use crate::{
    CERT_INFO_HANDLE, HSM_CLIENT_CREATE, HSM_CLIENT_DESTROY, HSM_CLIENT_FREE_BUFFER,
    HSM_CLIENT_HANDLE,
};

pub type HSM_CLIENT_GET_CERTIFICATE =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;
pub type HSM_CLIENT_GET_CERT_KEY =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;
pub type HSM_CLIENT_GET_COMMON_NAME =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> *mut c_char>;
pub type HSM_CLIENT_SIGN_WITH_PRIVATE_KEY = Option<
    unsafe extern "C" fn(
        handle: HSM_CLIENT_HANDLE,
        data_to_be_signed: *const c_uchar,
        data_to_be_signed_size: usize,
        digest: *mut *mut c_uchar,
        digest_size: *mut usize,
    ) -> c_int,
>;
pub type HSM_CLIENT_GET_CERTIFICATE_INFO =
    Option<unsafe extern "C" fn(handle: HSM_CLIENT_HANDLE) -> CERT_INFO_HANDLE>;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct HSM_CLIENT_X509_INTERFACE {
    pub hsm_client_x509_create: HSM_CLIENT_CREATE,
    pub hsm_client_x509_destroy: HSM_CLIENT_DESTROY,
    pub hsm_client_get_cert: HSM_CLIENT_GET_CERTIFICATE,
    pub hsm_client_get_key: HSM_CLIENT_GET_CERT_KEY,
    pub hsm_client_get_common_name: HSM_CLIENT_GET_COMMON_NAME,
    pub hsm_client_free_buffer: HSM_CLIENT_FREE_BUFFER,
    pub hsm_client_sign_with_private_key: HSM_CLIENT_SIGN_WITH_PRIVATE_KEY,
    pub hsm_client_get_certificate_info: HSM_CLIENT_GET_CERTIFICATE_INFO,
}

impl Default for HSM_CLIENT_X509_INTERFACE {
    fn default() -> HSM_CLIENT_X509_INTERFACE {
        HSM_CLIENT_X509_INTERFACE {
            hsm_client_x509_create: None,
            hsm_client_x509_destroy: None,
            hsm_client_get_cert: None,
            hsm_client_get_key: None,
            hsm_client_get_common_name: None,
            hsm_client_free_buffer: None,
            hsm_client_sign_with_private_key: None,
            hsm_client_get_certificate_info: None,
        }
    }
}

#[test]
fn bindgen_test_layout_HSM_CLIENT_X509_INTERFACE() {
    assert_eq!(
        ::std::mem::size_of::<HSM_CLIENT_X509_INTERFACE>(),
        8_usize * ::std::mem::size_of::<usize>(),
        concat!("Size of: ", stringify!(HSM_CLIENT_X509_INTERFACE))
    );
    assert_eq!(
        ::std::mem::align_of::<HSM_CLIENT_X509_INTERFACE>(),
        ::std::mem::size_of::<usize>(),
        concat!("Alignment of ", stringify!(HSM_CLIENT_X509_INTERFACE))
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_x509_create as *const _
                as usize
        },
        0_usize,
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_x509_create)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_x509_destroy
                as *const _ as usize
        },
        ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_x509_destroy)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_get_cert as *const _
                as usize
        },
        2_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_get_cert)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_get_key as *const _
                as usize
        },
        3_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_get_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_get_common_name
                as *const _ as usize
        },
        4_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_get_common_name)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_free_buffer as *const _
                as usize
        },
        5_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_free_buffer)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_sign_with_private_key
                as *const _ as usize
        },
        6_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_sign_with_private_key)
        )
    );
    assert_eq!(
        unsafe {
            &(*(::std::ptr::null::<HSM_CLIENT_X509_INTERFACE>())).hsm_client_get_certificate_info
                as *const _ as usize
        },
        7_usize * ::std::mem::size_of::<usize>(),
        concat!(
            "Offset of field: ",
            stringify!(HSM_CLIENT_X509_INTERFACE),
            "::",
            stringify!(hsm_client_get_certificate_info)
        )
    );
}
