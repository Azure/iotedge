// Copyright (c) Microsoft. All rights reserved.

use std::convert::AsRef;
use std::ffi::CStr;
use std::ops::{Deref, Drop};
use std::os::raw::{c_uchar, c_void};
use std::ptr;
use std::slice;

use super::*;
use super::{ManageTpmKeys, SignWithTpm};
use crate::error::{Error, ErrorKind};

/// Hsm for TPM
/// create an instance of this to use the TPM interface of an HSM
///
#[derive(Debug)]
pub struct Tpm {
    handle: HSM_CLIENT_HANDLE,
    interface: HSM_CLIENT_TPM_INTERFACE,
}

// Handles don't have thread-affinity
unsafe impl Send for Tpm {}

// HSM TPM

impl Drop for Tpm {
    fn drop(&mut self) {
        if let Some(f) = self.interface.hsm_client_tpm_destroy {
            unsafe {
                f(self.handle);
            }
        }
        //TODO: unit tests are calling this function, and should avoid doing so.
        unsafe { hsm_client_tpm_deinit() };
    }
}

impl Tpm {
    /// Create a new TPM implementation for the HSM API.
    pub fn new() -> Result<Tpm, Error> {
        let result = unsafe { hsm_client_tpm_init() as isize };
        if result != 0 {
            Err(result)?
        }
        let if_ptr = unsafe { hsm_client_tpm_interface() };
        if if_ptr.is_null() {
            unsafe { hsm_client_tpm_deinit() };
            Err(ErrorKind::NullResponse)?
        }
        let interface = unsafe { *if_ptr };
        if let Some(handle) = interface.hsm_client_tpm_create.map(|f| unsafe { f() }) {
            if handle.is_null() {
                unsafe { hsm_client_tpm_deinit() };
                Err(ErrorKind::NullResponse)?
            }
            Ok(Tpm { handle, interface })
        } else {
            unsafe { hsm_client_tpm_deinit() };
            Err(ErrorKind::NullResponse)?
        }
    }

    pub fn get_version(&self) -> Result<String, Error> {
        let version = unsafe {
            CStr::from_ptr(hsm_get_version())
                .to_string_lossy()
                .into_owned()
        };
        Ok(version)
    }
}

impl ManageTpmKeys for Tpm {
    /// Imports key that has been previously encrypted with the endorsement key and storage root key into the TPM key storage.
    fn activate_identity_key(&self, key: &[u8]) -> Result<(), Error> {
        let key_fn = self
            .interface
            .hsm_client_activate_identity_key
            .ok_or(ErrorKind::NoneFn)?;

        let result = unsafe { key_fn(self.handle, key.as_ptr(), key.len()) };
        match result {
            0 => Ok(()),
            r => Err(r)?,
        }
    }

    /// Retrieves the endorsement key of the TPM .
    fn get_ek(&self) -> Result<TpmKey, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self.interface.hsm_client_get_ek.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle, &mut ptr, &mut key_ln) };
        match result {
            0 => Ok(TpmKey::new(self.interface, ptr as *const _, key_ln)),
            r => Err(r)?,
        }
    }

    /// Retrieves the storage root key of the TPM
    fn get_srk(&self) -> Result<TpmKey, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self.interface.hsm_client_get_srk.ok_or(ErrorKind::NoneFn)?;
        let result = unsafe { key_fn(self.handle, &mut ptr, &mut key_ln) };
        match result {
            0 => Ok(TpmKey::new(self.interface, ptr as *const _, key_ln)),
            r => Err(r)?,
        }
    }
}

impl SignWithTpm for Tpm {
    /// Hashes the parameter data with the key previously stored in the TPM and returns the value
    fn sign_with_identity(&self, data: &[u8]) -> Result<TpmDigest, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();

        let key_fn = self
            .interface
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
        match result {
            0 => Ok(TpmDigest::new(self.interface, ptr as *const _, key_ln)),
            r => Err(r)?,
        }
    }

    fn derive_and_sign_with_identity(
        &self,
        data: &[u8],
        identity: &[u8],
    ) -> Result<TpmDigest, Error> {
        let mut key_ln: usize = 0;
        let mut ptr = ptr::null_mut();
        let key_fn = self
            .interface
            .hsm_client_derive_and_sign_with_identity
            .ok_or(ErrorKind::NoneFn)?;
        let result = unsafe {
            key_fn(
                self.handle,
                data.as_ptr(),
                data.len(),
                identity.as_ptr(),
                identity.len(),
                &mut ptr,
                &mut key_ln,
            )
        };
        if result == 0 {
            Ok(TpmDigest::new(self.interface, ptr as *const _, key_ln))
        } else {
            Err(result)?
        }
    }
}

/// When buffer data is returned from TPM interface, it is placed in this struct.
/// This is a buffer allocated by the C library.
#[derive(Debug)]
pub struct TpmBuffer {
    interface: HSM_CLIENT_TPM_INTERFACE,
    key: *const c_uchar,
    len: usize,
}

impl Drop for TpmBuffer {
    fn drop(&mut self) {
        let free_fn = self
            .interface
            .hsm_client_free_buffer
            .expect("Unknown Free function for TpmBuffer");
        unsafe { free_fn(self.key as *mut c_void) };
    }
}

impl TpmBuffer {
    pub fn new(interface: HSM_CLIENT_TPM_INTERFACE, key: *const c_uchar, len: usize) -> TpmBuffer {
        TpmBuffer {
            interface,
            key,
            len,
        }
    }
}

impl Deref for TpmBuffer {
    type Target = [u8];
    fn deref(&self) -> &Self::Target {
        self.as_ref()
    }
}

pub type TpmKey = TpmBuffer;
pub type TpmDigest = TpmBuffer;

impl AsRef<[u8]> for TpmBuffer {
    fn as_ref(&self) -> &[u8] {
        unsafe { slice::from_raw_parts(self.key, self.len) }
    }
}

#[cfg(test)]
mod tests {
    use std::os::raw::{c_int, c_uchar, c_void};
    use std::ptr;

    use super::super::{ManageTpmKeys, SignWithTpm};
    use super::{Tpm, TpmKey};
    use hsm_sys::*;

    extern "C" {
        pub fn malloc(size: usize) -> *mut c_void;
        pub fn memset(s: *mut c_void, c: c_int, size: usize) -> *mut c_void;
        pub fn free(s: *mut c_void);
    }

    unsafe extern "C" fn real_buffer_destroy(b: *mut c_void) {
        free(b);
    }

    fn fake_good_tpm_buffer_free() -> HSM_CLIENT_TPM_INTERFACE_TAG {
        HSM_CLIENT_TPM_INTERFACE_TAG {
            hsm_client_free_buffer: Some(real_buffer_destroy),
            ..HSM_CLIENT_TPM_INTERFACE_TAG::default()
        }
    }

    #[test]
    fn tpm_data_test() {
        let len = 10;
        let key = unsafe {
            let v = malloc(len);
            memset(v, 6 as c_int, len)
        };

        let key2 = TpmKey::new(fake_good_tpm_buffer_free(), key as *const c_uchar, len);

        let slice1 = &key2;

        assert_eq!(slice1.len(), len);
        assert_eq!(slice1[0], 6);
        assert_eq!(slice1[len - 1], 6);
    }

    #[test]
    #[should_panic(expected = "Unknown Free function for TpmBuffer")]
    fn tpm_free_fn_none() {
        let key = b"0123456789";
        let len = key.len();

        let key2 = TpmKey::new(
            HSM_CLIENT_TPM_INTERFACE_TAG::default(),
            key.as_ptr() as *const c_uchar,
            len,
        );

        let slice1 = &key2;

        assert_eq!(slice1.len(), len);
        // function will panic on drop.
    }

    unsafe extern "C" fn fake_handle_create_good() -> HSM_CLIENT_HANDLE {
        ptr::null_mut()
    }
    unsafe extern "C" fn fake_handle_create_bad() -> HSM_CLIENT_HANDLE {
        1_isize as *mut c_void
    }

    unsafe extern "C" fn fake_handle_destroy(_h: HSM_CLIENT_HANDLE) {}

    unsafe extern "C" fn fake_buffer_destroy(_b: *mut c_void) {}

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

    unsafe extern "C" fn fake_derive_and_sign(
        handle: HSM_CLIENT_HANDLE,
        _data_to_be_signed: *const c_uchar,
        _data_to_be_signed_size: usize,
        _identity: *const c_uchar,
        _identity_size: usize,
        digest: *mut *mut c_uchar,
        digest_size: *mut usize,
    ) -> c_int {
        let n = handle as isize;
        if n == 0 {
            *digest = malloc(DEFAULT_KEY_LEN) as *mut c_uchar;
            *digest_size = DEFAULT_KEY_LEN;
            0
        } else {
            1
        }
    }

    fn fake_no_if_tpm_hsm() -> Tpm {
        Tpm {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                ..HSM_CLIENT_TPM_INTERFACE_TAG::default()
            },
        }
    }

    #[test]
    fn tpm_no_activate_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let key = b"key data";
        let err = hsm_tpm.activate_identity_key(key).unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn tpm_no_getek_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let err = hsm_tpm.get_ek().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn tpm_no_getsrk_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let err = hsm_tpm.get_srk().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn tpm_no_sign_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let key = b"key data";
        let err = hsm_tpm.sign_with_identity(key).unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    #[test]
    fn tpm_no_derive_and_sign_function_fail() {
        let hsm_tpm = fake_no_if_tpm_hsm();
        let key = b"key data";
        let identity = b"identity";
        let err = hsm_tpm
            .derive_and_sign_with_identity(key, identity)
            .unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API Not Implemented")));
    }

    fn fake_good_tpm_hsm() -> Tpm {
        Tpm {
            handle: unsafe { fake_handle_create_good() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                hsm_client_activate_identity_key: Some(fake_activate_id_key),
                hsm_client_get_ek: Some(fake_ek),
                hsm_client_get_srk: Some(fake_srk),
                hsm_client_sign_with_identity: Some(fake_sign),
                hsm_client_derive_and_sign_with_identity: Some(fake_derive_and_sign),
                hsm_client_free_buffer: Some(fake_buffer_destroy),
            },
        }
    }

    #[test]
    #[allow(clippy::let_unit_value)]
    fn tpm_success() {
        let hsm_tpm = fake_good_tpm_hsm();
        let k1 = b"A fake key";
        let _result1: () = hsm_tpm.activate_identity_key(k1).unwrap();

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

        let k3 = b"a buffer";
        let identity = b"some identity";
        let result5 = hsm_tpm.derive_and_sign_with_identity(k3, identity).unwrap();
        let buf5 = &result5;
        assert_eq!(buf5.len(), DEFAULT_KEY_LEN);
    }

    fn fake_bad_tpm_hsm() -> Tpm {
        Tpm {
            handle: unsafe { fake_handle_create_bad() },
            interface: HSM_CLIENT_TPM_INTERFACE_TAG {
                hsm_client_tpm_create: Some(fake_handle_create_good),
                hsm_client_tpm_destroy: Some(fake_handle_destroy),
                hsm_client_activate_identity_key: Some(fake_activate_id_key),
                hsm_client_get_ek: Some(fake_ek),
                hsm_client_get_srk: Some(fake_srk),
                hsm_client_sign_with_identity: Some(fake_sign),
                hsm_client_derive_and_sign_with_identity: Some(fake_derive_and_sign),
                hsm_client_free_buffer: Some(fake_buffer_destroy),
            },
        }
    }

    #[test]
    fn tpm_activate_identity_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let k1 = b"A fake key";
        let err = hsm_tpm.activate_identity_key(k1).unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API failure occurred")));
    }

    #[test]
    fn tpm_getek_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let err = hsm_tpm.get_ek().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API failure occurred")));
    }

    #[test]
    fn tpm_getsrk_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let err = hsm_tpm.get_srk().unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API failure occurred")));
    }

    #[test]
    fn tpm_sign_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let k1 = b"A fake buffer";
        let err = hsm_tpm.sign_with_identity(k1).unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API failure occurred")));
    }

    #[test]
    fn tpm_derive_and_sign_errors() {
        let hsm_tpm = fake_bad_tpm_hsm();
        let k1 = b"A fake buffer";
        let identity = b"an identity";
        let err = hsm_tpm
            .derive_and_sign_with_identity(k1, identity)
            .unwrap_err();
        assert!(failure::Fail::iter_chain(&err)
            .any(|err| err.to_string().contains("HSM API failure occurred")));
    }
}
