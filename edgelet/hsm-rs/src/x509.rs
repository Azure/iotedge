// Copyright (c) Microsoft. All rights reserved.

use std::ffi::CStr;
use std::ops::{Deref, Drop};
use std::os::raw::{c_char, c_uchar, c_void};
use std::ptr;
use std::slice;
use std::string::String;

use super::GetDeviceIdentityCertificate;
use super::*;
use crate::error::{Error, ErrorKind};

// Handles don't have thread-affinity
unsafe impl Send for X509 {}

/// Hsm for x509
/// create an instance of this to use the x509 interface of an HSM
///
#[derive(Debug)]
pub struct X509 {
    handle: HSM_CLIENT_HANDLE,
    interface: HSM_CLIENT_X509_INTERFACE,
}
// HSM x509

impl Drop for X509 {
    fn drop(&mut self) {
        if let Some(f) = self.interface.hsm_client_x509_destroy {
            unsafe {
                f(self.handle);
            }
        }
        unsafe { hsm_client_x509_deinit() };
    }
}

impl X509 {
    /// Create a new x509 implementation for the HSM API.
    pub fn new(auto_generated_cert_lifetime: u64) -> Result<Self, Error> {
        let result = unsafe { hsm_client_x509_init(auto_generated_cert_lifetime) as isize };
        if result != 0 {
            return Err(result.into());
        }
        let if_ptr = unsafe { hsm_client_x509_interface() };
        if if_ptr.is_null() {
            return Err(ErrorKind::NullResponse.into());
        }
        let interface = unsafe { *if_ptr };
        if let Some(handle) = interface.hsm_client_x509_create.map(|f| unsafe { f() }) {
            if handle.is_null() {
                return Err(ErrorKind::NullResponse.into());
            }
            Ok(X509 { handle, interface })
        } else {
            Err(ErrorKind::NullResponse.into())
        }
    }

    pub fn get_version(&self) -> Result<String, Error> {
        // We want to enforce Crypto::new is called before this, since ::new() initializes the libiothsm. So silence the allow_unused clippy lint.
        let _ = self;

        let version = unsafe {
            CStr::from_ptr(hsm_get_version())
                .to_string_lossy()
                .into_owned()
        };
        Ok(version)
    }
}

impl GetDeviceIdentityCertificate for X509 {
    /// Retrieves the certificate to be used for x509 communication.
    fn get_cert(&self) -> Result<X509Data, Error> {
        let key_fn = self
            .interface
            .hsm_client_get_cert
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle) };
        if result.is_null() {
            Err(ErrorKind::NullResponse.into())
        } else {
            Ok(X509Data::new(self.interface, result))
        }
    }

    /// Retrieves the alias key from the x509 certificate.
    fn get_key(&self) -> Result<X509Data, Error> {
        let key_fn = self.interface.hsm_client_get_key.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle) };
        if result.is_null() {
            Err(ErrorKind::NullResponse.into())
        } else {
            Ok(X509Data::new(self.interface, result))
        }
    }

    /// Retrieves the common name from the x509 certificate.
    fn get_common_name(&self) -> Result<String, Error> {
        let key_fn = self
            .interface
            .hsm_client_get_common_name
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe {
            let name = key_fn(self.handle);
            if name.is_null() {
                Err(ErrorKind::NullResponse)
            } else {
                Ok(X509Data::new(self.interface, name))
            }
        }?;
        let common_name = unsafe { CStr::from_ptr(*result).to_string_lossy().into_owned() };
        Ok(common_name)
    }

    /// Sign data using the device identity x509 certificate
    fn sign_with_private_key(&self, data: &[u8]) -> Result<PrivateKeySignDigest, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let sign_fn = self
            .interface
            .hsm_client_sign_with_private_key
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe {
            sign_fn(
                self.handle,
                data.as_ptr(),
                data.len(),
                &mut ptr,
                &mut key_ln,
            )
        };

        match result {
            0 => Ok(PrivateKeySignDigest::new(
                self.interface,
                ptr as *const _,
                key_ln,
            )),
            _ => Err(ErrorKind::PrivateKeySignFn.into()),
        }
    }

    /// Retrieves the certificate to be used for x509 communication.
    fn get_certificate_info(&self) -> Result<HsmCertificate, Error> {
        let if_fn = self
            .interface
            .hsm_client_get_certificate_info
            .ok_or(ErrorKind::NoneFn)?;
        let cert_info_handle = unsafe { if_fn(self.handle) };
        if cert_info_handle.is_null() {
            Err(ErrorKind::HsmCertificateFailure.into())
        } else {
            let handle = HsmCertificate::from(cert_info_handle);
            match handle {
                Ok(h) => Ok(h),
                Err(_) => Err(ErrorKind::HsmCertificateFailure.into()),
            }
        }
    }
}

/// When buffer data is returned from TPM interface, it is placed in this struct.
/// This is a buffer allocated by the C library.
#[derive(Debug)]
pub struct X509Buffer {
    interface: HSM_CLIENT_X509_INTERFACE,
    data: *const c_uchar,
    len: usize,
}

impl Drop for X509Buffer {
    fn drop(&mut self) {
        let free_fn = self
            .interface
            .hsm_client_free_buffer
            .expect("Unknown Free function for X509Buffer");
        unsafe { free_fn(self.data as *mut c_void) };
    }
}

impl X509Buffer {
    pub fn new(
        interface: HSM_CLIENT_X509_INTERFACE,
        data: *const c_uchar,
        len: usize,
    ) -> X509Buffer {
        X509Buffer {
            interface,
            data,
            len,
        }
    }
}

impl Deref for X509Buffer {
    type Target = [u8];
    fn deref(&self) -> &Self::Target {
        self.as_ref()
    }
}

pub type PrivateKeySignDigest = X509Buffer;

impl AsRef<[u8]> for X509Buffer {
    fn as_ref(&self) -> &[u8] {
        unsafe { slice::from_raw_parts(self.data, self.len) }
    }
}

/// When data is returned from x509 interface, it is placed in this struct.
/// This is a buffer allocated by the C library.
/// TODO: get this to return a &[u8] so you don't need to access a raw pointer.
#[derive(Debug)]
pub struct X509Data {
    interface: HSM_CLIENT_X509_INTERFACE,
    data: *const c_char,
}

impl Drop for X509Data {
    fn drop(&mut self) {
        let free_fn = self
            .interface
            .hsm_client_free_buffer
            .expect("Unknown Free function for X509 buffer");
        unsafe { free_fn(self.data as *mut c_void) };
    }
}

impl X509Data {
    fn new(interface: HSM_CLIENT_X509_INTERFACE, data: *mut c_char) -> X509Data {
        X509Data {
            interface,
            data: data as *const c_char,
        }
    }
}

impl Deref for X509Data {
    type Target = *const c_char;
    fn deref(&self) -> &Self::Target {
        &self.data
    }
}

#[cfg(test)]
mod tests {
    use std::ffi::{CStr, CString};
    use std::os::raw::{c_char, c_int, c_uchar, c_void};
    use std::ptr;
    use std::slice;

    use super::super::GetDeviceIdentityCertificate;
    use super::{X509Data, X509};
    use hsm_sys::*;

    extern "C" {
        pub fn malloc(size: usize) -> *mut c_void;
        pub fn memset(s: *mut c_void, c: c_int, size: usize) -> *mut c_void;
        pub fn memcpy(dest: *mut c_void, src: *const c_void, size: usize) -> *mut c_void;
        pub fn free(s: *mut c_void);
    }

    unsafe extern "C" fn real_buffer_destroy(b: *mut c_void) {
        free(b);
    }

    fn fake_good_x509_buffer_free() -> HSM_CLIENT_X509_INTERFACE {
        HSM_CLIENT_X509_INTERFACE {
            hsm_client_free_buffer: Some(real_buffer_destroy),
            ..HSM_CLIENT_X509_INTERFACE::default()
        }
    }

    #[test]
    fn x509_data_test() {
        let len = 100;
        let key = unsafe {
            let v = malloc(len);
            memset(v, 32 as c_int, len)
        };

        let key2 = X509Data::new(fake_good_x509_buffer_free(), key as *mut c_char);

        let slice1 = unsafe { slice::from_raw_parts(*key2, len) };

        assert_eq!(slice1[0], 32);
        assert_eq!(slice1[len - 1], 32);
    }

    #[test]
    #[should_panic(expected = "Unknown Free function for X509 buffer")]
    fn x509_data_free_fn_none() {
        let key = b"0123456789";
        let len = key.len();

        let key2 = X509Data::new(
            HSM_CLIENT_X509_INTERFACE::default(),
            key.as_ptr() as *mut c_char,
        );

        let slice1 = unsafe { slice::from_raw_parts(*key2, len) };

        assert_eq!(slice1[0], '0' as c_char);
        // function will panic on drop.
    }

    unsafe extern "C" fn fake_handle_create_good() -> HSM_CLIENT_HANDLE {
        ptr::null_mut()
    }
    unsafe extern "C" fn fake_handle_create_bad() -> HSM_CLIENT_HANDLE {
        1_isize as *mut c_void
    }
    unsafe extern "C" fn fake_buffer_destroy(_b: *mut c_void) {}

    const DEFAULT_BUF_LEN: usize = 10;

    unsafe extern "C" fn fake_handle_destroy(_h: HSM_CLIENT_HANDLE) {}

    unsafe extern "C" fn fake_get_cert(handle: HSM_CLIENT_HANDLE) -> *mut c_char {
        if handle as isize == 0 {
            malloc(DEFAULT_BUF_LEN) as *mut c_char
        } else {
            ptr::null_mut() as *mut c_char
        }
    }
    unsafe extern "C" fn fake_get_cert_key(handle: HSM_CLIENT_HANDLE) -> *mut c_char {
        if handle as isize == 0 {
            malloc(DEFAULT_BUF_LEN) as *mut c_char
        } else {
            ptr::null_mut() as *mut c_char
        }
    }

    unsafe extern "C" fn fake_get_name(handle: HSM_CLIENT_HANDLE) -> *mut c_char {
        if handle as isize == 0 {
            let s = malloc(DEFAULT_BUF_LEN);
            let c_str = CStr::from_bytes_with_nul(b"123456789\0").unwrap();
            memcpy(s, c_str.as_ptr() as *const c_void, DEFAULT_BUF_LEN);
            s as *mut c_char
        } else {
            ptr::null_mut() as *mut c_char
        }
    }

    static TEST_RSA_CERT: &str = "-----BEGIN CERTIFICATE-----\nMIICpDCCAYwCCQCgAJQdOd6dNzANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwHhcNMTcwMTIwMTkyNTMzWhcNMjcwMTE4MTkyNTMzWjAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDlJ3fRNWm05BRAhgUY7cpzaxHZIORomZaOp2Uua5yv+psdkpv35ExLhKGrUIK1AJLZylnue0ohZfKPFTnoxMHOecnaaXZ9RA25M7XGQvw85ePlGOZKKf3zXw3Ds58GFY6Sr1SqtDopcDuMmDSg/afYVvGHDjb2Fc4hZFip350AADcmjH5SfWuxgptCY2Jl6ImJoOpxt+imWsJCJEmwZaXw+eZBb87e/9PH4DMXjIUFZebShowAfTh/sinfwRkaLVQ7uJI82Ka/icm6Hmr56j7U81gDaF0DhC03ds5lhN7nMp5aqaKeEJiSGdiyyHAescfxLO/SMunNc/eG7iAirY7BAgMBAAEwDQYJKoZIhvcNAQELBQADggEBACU7TRogb8sEbv+SGzxKSgWKKbw+FNgC4Zi6Fz59t+4jORZkoZ8W87NM946wvkIpxbLKuc4F+7nTGHHksyHIiGC3qPpi4vWpqVeNAP+kfQptFoWEOzxD7jQTWIcqYhvssKZGwDk06c/WtvVnhZOZW+zzJKXA7mbwJrfp8VekOnN5zPwrOCumDiRX7BnEtMjqFDgdMgs9ohR5aFsI7tsqp+dToLKaZqBLTvYwCgCJCxdg3QvMhVD8OxcEIFJtDEwm3h9WFFO3ocabCmcMDyXUL354yaZ7RphCBLd06XXdaUU/eV6fOjY6T5ka4ZRJcYDJtjxSG04XPtxswQfrPGGoFhk=\n-----END CERTIFICATE-----";

    unsafe extern "C" fn fake_get_cert_handle(handle: HSM_CLIENT_HANDLE) -> CERT_INFO_HANDLE {
        let n = handle as isize;
        if n == 0 {
            let cert = CString::new(TEST_RSA_CERT).unwrap();
            let pk = CString::new("1234").unwrap();
            certificate_info_create(
                cert.as_ptr(),
                pk.as_ptr() as *const c_void,
                pk.to_bytes().len() as usize,
                1 as u32,
            )
        } else {
            ::std::ptr::null_mut()
        }
    }

    const DEFAULT_DIGEST_SIZE: usize = 5_usize;
    const DEFAULT_DIGEST: [u8; DEFAULT_DIGEST_SIZE] = [0, 1, 2, 3, 4];

    unsafe extern "C" fn fake_private_key_sign(
        handle: HSM_CLIENT_HANDLE,
        _data_to_be_signed: *const c_uchar,
        _data_to_be_signed_size: usize,
        digest: *mut *mut c_uchar,
        digest_size: *mut usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            let s = malloc(DEFAULT_DIGEST_SIZE);
            memcpy(
                s,
                DEFAULT_DIGEST.as_ptr() as *const c_void,
                DEFAULT_DIGEST_SIZE,
            );
            *digest = s as *mut c_uchar;
            *digest_size = DEFAULT_DIGEST_SIZE;
            0
        } else {
            1
        }
    }

    fn fake_no_if_x509_hsm() -> X509 {
        X509 {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_good),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                ..HSM_CLIENT_X509_INTERFACE::default()
            },
        }
    }

    #[test]
    fn x509_no_getcert_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let err = hsm_x509.get_cert().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn x509_no_getkey_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let err = hsm_x509.get_key().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn x509_no_getname_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let err = hsm_x509.get_common_name().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn x509_no_sign_with_private_key_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let err = hsm_x509.sign_with_private_key(b"aabb").unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    fn fake_good_x509_hsm() -> X509 {
        X509 {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_good),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                hsm_client_get_cert: Some(fake_get_cert),
                hsm_client_get_key: Some(fake_get_cert_key),
                hsm_client_get_common_name: Some(fake_get_name),
                hsm_client_free_buffer: Some(fake_buffer_destroy),
                hsm_client_sign_with_private_key: Some(fake_private_key_sign),
                hsm_client_get_certificate_info: Some(fake_get_cert_handle),
            },
        }
    }

    #[test]
    fn certs_success() {
        let hsm_x509 = fake_good_x509_hsm();
        let result1 = hsm_x509.get_cert().unwrap();
        let string1 = &result1;
        assert!(!string1.is_null());

        let result2 = hsm_x509.get_key().unwrap();
        let string2 = &result2;
        assert!(!string2.is_null());

        let result3 = hsm_x509.get_common_name();
        if let Ok(string3) = result3 {
            assert_eq!(string3, "123456789".to_string());
        } else {
            unreachable!();
        }

        let result4 = hsm_x509.sign_with_private_key(b"aabbcc");
        if let Ok(x509_buffer) = result4 {
            assert_eq!(x509_buffer.as_ref(), DEFAULT_DIGEST);
        } else {
            panic!("Unexpected result");
        }

        let result5 = hsm_x509.get_certificate_info();
        if let Ok(cert_handle) = result5 {
            assert_eq!(TEST_RSA_CERT, cert_handle.pem().unwrap());
        } else {
            panic!("Unexpected result");
        }

        let result4 = hsm_x509.sign_with_private_key(b"aabbcc");
        if let Ok(x509_buffer) = result4 {
            assert_eq!(x509_buffer.as_ref(), DEFAULT_DIGEST);
        } else {
            unreachable!("Sign with device id private key failed");
        }

        let result5 = hsm_x509.get_certificate_info();
        if let Ok(cert_handle) = result5 {
            assert_eq!(TEST_RSA_CERT, cert_handle.pem().unwrap());
        } else {
            unreachable!("Get device id certificate info failed");
        }
    }

    fn fake_bad_x509_hsm() -> X509 {
        X509 {
            handle: unsafe { fake_handle_create_bad() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_bad),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                hsm_client_get_cert: Some(fake_get_cert),
                hsm_client_get_key: Some(fake_get_cert_key),
                hsm_client_get_common_name: Some(fake_get_name),
                hsm_client_free_buffer: Some(fake_buffer_destroy),
                hsm_client_sign_with_private_key: Some(fake_private_key_sign),
                hsm_client_get_certificate_info: Some(fake_get_cert_handle),
            },
        }
    }

    #[test]
    fn get_cert_null() {
        let hsm_x509 = fake_bad_x509_hsm();
        let err = hsm_x509.get_cert().unwrap_err();
        assert!(failure::Fail::iter_chain(&err).any(|err| err
            .to_string()
            .contains("HSM API returned an invalid null response")));
    }

    #[test]
    fn get_key_null() {
        let hsm_x509 = fake_bad_x509_hsm();
        let err = hsm_x509.get_key().unwrap_err();
        assert!(failure::Fail::iter_chain(&err).any(|err| err
            .to_string()
            .contains("HSM API returned an invalid null response")));
    }

    #[test]
    fn common_name_error() {
        let hsm_x509 = fake_bad_x509_hsm();
        let err = hsm_x509.get_common_name().unwrap_err();
        assert!(failure::Fail::iter_chain(&err).any(|err| err
            .to_string()
            .contains("HSM API returned an invalid null response")));
    }

    #[test]
    fn sign_with_private_key_error() {
        let hsm_x509 = fake_bad_x509_hsm();
        let err = hsm_x509.sign_with_private_key(b"aabbcc").unwrap_err();
        assert!(failure::Fail::iter_chain(&err).any(|err| err
            .to_string()
            .contains("HSM API sign with private key failed")));
    }

    #[test]
    fn get_certificate_info_error() {
        let hsm_x509 = fake_bad_x509_hsm();
        let err = hsm_x509.get_certificate_info().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM certificate info get failed")));
    }
}
