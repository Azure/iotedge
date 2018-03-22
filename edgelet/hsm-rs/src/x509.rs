// Copyright (c) Microsoft. All rights reserved.

use std::ffi::CStr;
use std::ops::{Deref, Drop};
use std::os::raw::{c_char, c_void};
use std::string::String;

use error::{Error, ErrorKind};
use hsm_sys::*;
use super::GetCerts;

extern "C" {
    pub fn free(__ptr: *mut c_void);
}

/// Hsm for x509
/// create an instance of this to use the x509 interface of an HSM
///
#[derive(Debug)]
pub struct HsmX509 {
    handle: HSM_CLIENT_HANDLE,
    interface: HSM_CLIENT_X509_INTERFACE,
}
// HSM x509

impl Drop for HsmX509 {
    fn drop(&mut self) {
        self.interface
            .hsm_client_x509_destroy
            .map(|f| unsafe { f(self.handle) });
    }
}

impl HsmX509 {
    /// Create a new x509 implementation for the HSM API. Will panic if
    /// interface is not found.
    pub fn new() -> HsmX509 {
        // If we can't get the interface, this is a critical failure, so
        // we should let this function panic.
        let interface = unsafe { *hsm_client_x509_interface() };
        HsmX509 {
            handle: interface
                .hsm_client_x509_create
                .map(|f| unsafe { f() })
                .unwrap(),
            interface,
        }
    }
}

impl GetCerts for HsmX509 {
    /// Retrieves the certificate to be used for x509 communication.
    fn get_cert(&self) -> Result<X509Data, Error> {
        let key_fn = self.interface.hsm_client_get_cert.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle) };
        if result.is_null() {
            Err(ErrorKind::NullResponse)?
        } else {
            Ok(X509Data::new(result))
        }
    }

    /// Retrieves the alias key from the x509 certificate.
    fn get_key(&self) -> Result<X509Data, Error> {
        let key_fn = self.interface.hsm_client_get_key.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle) };
        if result.is_null() {
            Err(ErrorKind::NullResponse)?
        } else {
            Ok(X509Data::new(result))
        }
    }

    /// Retrieves the common name from the x509 certificate.
    fn get_common_name(&self) -> Result<String, Error> {
        let key_fn = self.interface
            .hsm_client_get_common_name
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe {
            let name = key_fn(self.handle);
            if name.is_null() {
                Err(ErrorKind::NullResponse)
            } else {
                Ok(X509Data::new(name))
            }
        }?;
        let common_name = unsafe { CStr::from_ptr(*result).to_string_lossy().into_owned() };
        Ok(common_name)
    }
}

/// When data is returned from x509 interface, it is placed in this struct.
/// This is a buffer allocated by the C library.
/// TODO: get this to return a &[u8] so you don't need to access a raw pointer.
#[derive(Debug)]
pub struct X509Data {
    data: *const c_char,
}

impl Drop for X509Data {
    fn drop(&mut self) {
        unsafe {
            free(self.data as *mut c_void);
        }
    }
}

impl X509Data {
    pub fn new(data: *mut c_char) -> X509Data {
        X509Data {
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
    use std::ffi::CStr;
    use std::os::raw::{c_char, c_int, c_void};
    use std::ptr;
    use std::slice;

    use hsm_sys::*;
    use super::{HsmX509, X509Data};
    use super::super::GetCerts;

    extern "C" {
        pub fn malloc(size: usize) -> *mut c_void;
        pub fn memset(s: *mut c_void, c: c_int, size: usize) -> *mut c_void;
        pub fn memcpy(dest: *mut c_void, src: *const c_void, size: usize) -> *mut c_void;
    }

    #[test]
    fn x509_data_test() {
        let len = 100;
        let key = unsafe {
            let v = malloc(len);
            memset(v, 32 as c_int, len)
        };

        let key2 = X509Data::new(key as *mut c_char);

        let slice1 = unsafe { slice::from_raw_parts(*key2, len) };

        assert_eq!(slice1[0], 32);
        assert_eq!(slice1[len - 1], 32);
    }

    unsafe extern "C" fn fake_handle_create_good() -> HSM_CLIENT_HANDLE {
        0_isize as *mut c_void
    }
    unsafe extern "C" fn fake_handle_create_bad() -> HSM_CLIENT_HANDLE {
        1_isize as *mut c_void
    }

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

    fn fake_no_if_x509_hsm() -> HsmX509 {
        HsmX509 {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_good),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                ..HSM_CLIENT_X509_INTERFACE::default()
            },
        }
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn x509_no_getcert_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let result = hsm_x509.get_cert().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn x509_no_getkey_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let result = hsm_x509.get_key().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn x509_no_getname_function_fail() {
        let hsm_x509 = fake_no_if_x509_hsm();
        let result = hsm_x509.get_common_name().unwrap();
        println!("You should never see this print {:?}", result);
    }

    fn fake_good_x509_hsm() -> HsmX509 {
        HsmX509 {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_good),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                hsm_client_get_cert: Some(fake_get_cert),
                hsm_client_get_key: Some(fake_get_cert_key),
                hsm_client_get_common_name: Some(fake_get_name),
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
            assert!(false);
        }
    }

    fn fake_bad_x509_hsm() -> HsmX509 {
        HsmX509 {
            handle: unsafe { fake_handle_create_bad() },
            interface: HSM_CLIENT_X509_INTERFACE {
                hsm_client_x509_create: Some(fake_handle_create_bad),
                hsm_client_x509_destroy: Some(fake_handle_destroy),
                hsm_client_get_cert: Some(fake_get_cert),
                hsm_client_get_key: Some(fake_get_cert_key),
                hsm_client_get_common_name: Some(fake_get_name),
            },
        }
    }

    #[test]
    #[should_panic(expected = "HSM API returned an invalid null response")]
    fn get_cert_null() {
        let hsm_x509 = fake_bad_x509_hsm();
        let result1 = hsm_x509.get_cert().unwrap();
        let string1 = &result1;
        assert!(string1.is_null());
    }

    #[test]
    #[should_panic(expected = "HSM API returned an invalid null response")]
    fn get_key_null() {
        let hsm_x509 = fake_bad_x509_hsm();
        let result2 = hsm_x509.get_key().unwrap();
        let string2 = &result2;
        assert!(string2.is_null());
    }

    #[test]
    #[should_panic(expected = "HSM API returned an invalid null response")]
    fn common_name_error() {
        let hsm_x509 = fake_bad_x509_hsm();
        let result3 = hsm_x509.get_common_name().unwrap();
        println!("This string should not be displayed {:?}", result3);
    }
}
