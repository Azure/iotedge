// Copyright (c) Microsoft. All rights reserved.

use std::ops::{Deref, Drop};
use std::os::raw::{c_uchar, c_void};
use std::ptr;
use std::slice;

use error::{Error, ErrorKind};
use hsm_sys::*;
use super::{ManageTpmKeys, SignWithTpm};

extern "C" {
    pub fn free(__ptr: *mut c_void);
}

/// Hsm for TPM
/// create an instance of this to use the TPM interface of an HSM
///
#[derive(Debug)]
pub struct HsmTpm {
    handle: HSM_CLIENT_HANDLE,
    interface: HSM_CLIENT_TPM_INTERFACE,
}

// HSM TPM

impl Drop for HsmTpm {
    fn drop(&mut self) {
        self.interface
            .hsm_client_tpm_destroy
            .map(|f| unsafe { f(self.handle) });
    }
}

impl HsmTpm {
    /// Create a new TPM implementation for the HSM API. Will panic if
    /// interface not found.
    pub fn new() -> HsmTpm {
        // If we can't get the interface, this is a critical failure, so
        // we should let this function panic.
        let interface = unsafe { *hsm_client_tpm_interface() };
        HsmTpm {
            handle: interface
                .hsm_client_tpm_create
                .map(|f| unsafe { f() })
                .unwrap(),
            interface,
        }
    }
}

impl ManageTpmKeys for HsmTpm {
    /// Imports key that has been previously encrypted with the endorsement key and storage root key into the TPM key storage.
    fn activate_identity_key(&self, key: &[u8]) -> Result<(), Error> {
        let key_fn = self.interface
            .hsm_client_activate_identity_key
            .ok_or(ErrorKind::NoneFn)?;

        let result = unsafe { key_fn(self.handle, key.as_ptr(), key.len()) };
        if result == 0 {
            Ok(())
        } else {
            Err(result)?
        }
    }

    /// Retrieves the endorsement key of the TPM .
    fn get_ek(&self) -> Result<TpmKey, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self.interface.hsm_client_get_ek.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle, &mut ptr, &mut key_ln) };
        if result == 0 {
            Ok(TpmKey::new(ptr as *const _, key_ln))
        } else {
            Err(result)?
        }
    }

    /// Retrieves the storage root key of the TPM
    fn get_srk(&self) -> Result<TpmKey, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self.interface.hsm_client_get_srk.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle, &mut ptr, &mut key_ln) };

        if result == 0 {
            Ok(TpmKey::new(ptr as *const _, key_ln))
        } else {
            Err(result)?
        }
    }
}

impl SignWithTpm for HsmTpm {
    /// Hashes the parameter data with the key previously stored in the TPM and returns the value
    fn sign_with_identity(&self, data: &[u8]) -> Result<TpmKey, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self.interface
            .hsm_client_sign_with_identity
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe {
            key_fn(
                self.handle,
                data.as_ptr(),
                data.len(),
                &mut ptr,
                &mut key_ln,
            )
        };
        if result == 0 {
            Ok(TpmKey::new(ptr as *const _, key_ln))
        } else {
            Err(result)?
        }
    }
}

/// When buffer data is returned from TPM interface, it is placed in this struct.
/// This is a buffer allocated by the C library.
#[derive(Debug)]
pub struct TpmKey {
    key: *const c_uchar,
    len: usize,
}

impl Drop for TpmKey {
    fn drop(&mut self) {
        unsafe {
            free(self.key as *mut c_void);
        }
    }
}

impl TpmKey {
    pub fn new(key: *const c_uchar, len: usize) -> TpmKey {
        TpmKey { key, len }
    }
}

impl Deref for TpmKey {
    type Target = [u8];
    fn deref(&self) -> &Self::Target {
        unsafe { &slice::from_raw_parts(self.key, self.len) }
    }
}

#[cfg(test)]
mod tests {
    use std::os::raw::{c_int, c_uchar, c_void};

    use hsm_sys::*;
    use super::{HsmTpm, TpmKey};
    use super::super::{ManageTpmKeys, SignWithTpm};

    extern "C" {
        pub fn malloc(size: usize) -> *mut c_void;
        pub fn memset(s: *mut c_void, c: c_int, size: usize) -> *mut c_void;
    }
    #[test]
    fn tpm_data_test() {
        let len = 10;
        let key = unsafe {
            let v = malloc(len);
            memset(v, 6 as c_int, len)
        };

        let key2 = TpmKey::new(key as *const c_uchar, len);

        let slice1 = &key2;

        assert_eq!(slice1.len(), len);
        assert_eq!(slice1[0], 6);
        assert_eq!(slice1[len - 1], 6);
    }

    unsafe extern "C" fn fake_handle_create_good() -> HSM_CLIENT_HANDLE {
        0_isize as *mut c_void
    }
    unsafe extern "C" fn fake_handle_create_bad() -> HSM_CLIENT_HANDLE {
        1_isize as *mut c_void
    }

    unsafe extern "C" fn fake_handle_destroy(_h: HSM_CLIENT_HANDLE) {}

    unsafe extern "C" fn fake_activate_id_key(
        handle: HSM_CLIENT_HANDLE,
        _key: *const c_uchar,
        _key_len: usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            0
        } else {
            1
        }
    }

    const DEFAULT_KEY_LEN: usize = 10_usize;

    unsafe extern "C" fn fake_ek(
        handle: HSM_CLIENT_HANDLE,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            *key = malloc(DEFAULT_KEY_LEN) as *mut c_uchar;
            *key_len = DEFAULT_KEY_LEN;
            0
        } else {
            1
        }
    }

    unsafe extern "C" fn fake_srk(
        handle: HSM_CLIENT_HANDLE,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            *key = malloc(DEFAULT_KEY_LEN) as *mut c_uchar;
            *key_len = DEFAULT_KEY_LEN;
            0
        } else {
            1
        }
    }

    unsafe extern "C" fn fake_sign(
        handle: HSM_CLIENT_HANDLE,
        _data: *const c_uchar,
        _data_len: usize,
        key: *mut *mut c_uchar,
        key_len: *mut usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            *key = malloc(DEFAULT_KEY_LEN) as *mut c_uchar;
            *key_len = DEFAULT_KEY_LEN;
            0
        } else {
            1
        }
    }

    fn fake_no_if_tpm_hsm() -> HsmTpm {
        HsmTpm {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                ..HSM_CLIENT_TPM_INTERFACE_TAG::default()
            },
        }
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn tpm_no_activate_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let key = b"key data";
        let result = hsm_tpm.activate_identity_key(key).unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn tpm_no_getek_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let result = hsm_tpm.get_ek().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn tpm_no_getsrk_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let result = hsm_tpm.get_srk().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API Not Implemented")]
    fn tpm_no_sign_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let key = b"key data";
        let result = hsm_tpm.sign_with_identity(key).unwrap();
        println!("You should never see this print {:?}", result);
    }

    fn fake_good_tpm_hsm() -> HsmTpm {
        HsmTpm {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                hsm_client_activate_identity_key: Some(fake_activate_id_key),
                hsm_client_get_ek: Some(fake_ek),
                hsm_client_get_srk: Some(fake_srk),
                hsm_client_sign_with_identity: Some(fake_sign),
            },
        }
    }

    #[test]
    fn tpm_success() {
        let hsm_tpm = fake_good_tpm_hsm();
        let k1 = b"A fake key";
        let result1 = hsm_tpm.activate_identity_key(k1).unwrap();
        assert_eq!(result1, ());

        let result2 = hsm_tpm.get_ek().unwrap();
        let buf2 = &result2;
        assert_eq!(buf2.len(), DEFAULT_KEY_LEN);

        let result3 = hsm_tpm.get_srk().unwrap();
        let buf3 = &result3;
        assert_eq!(buf3.len(), DEFAULT_KEY_LEN);

        let k2 = b"a buffer";
        let result4 = hsm_tpm.sign_with_identity(k2).unwrap();
        let buf4 = &result4;
        assert_eq!(buf4.len(), DEFAULT_KEY_LEN);
    }

    fn fake_bad_tpm_hsm() -> HsmTpm {
        HsmTpm {
            handle: unsafe { fake_handle_create_bad() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                hsm_client_activate_identity_key: Some(fake_activate_id_key),
                hsm_client_get_ek: Some(fake_ek),
                hsm_client_get_srk: Some(fake_srk),
                hsm_client_sign_with_identity: Some(fake_sign),
            },
        }
    }

    #[test]
    #[should_panic(expected = "HSM API failure occured")]
    fn tpm_activate_identity_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let k1 = b"A fake key";
        let result = hsm_tpm.activate_identity_key(k1).unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API failure occured")]
    fn tpm_getek_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let result = hsm_tpm.get_ek().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API failure occured")]
    fn tpm_getsrk_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let result = hsm_tpm.get_srk().unwrap();
        println!("You should never see this print {:?}", result);
    }

    #[test]
    #[should_panic(expected = "HSM API failure occured")]
    fn tpm_sign_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let k1 = b"A fake buffer";
        let result = hsm_tpm.sign_with_identity(k1).unwrap();
        println!("You should never see this print {:?}", result);
    }

}
