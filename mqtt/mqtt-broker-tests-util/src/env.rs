use std::{
    env::VarError,
    ffi::{OsStr, OsString},
};

pub fn set_var<K: AsRef<OsStr>, V: AsRef<OsStr>>(key: K, value: V) -> Result<EnvVar<K>, VarError> {
    let old_value = std::env::var_os(key.as_ref());
    std::env::set_var(key.as_ref(), value);

    Ok(EnvVar::new(key, old_value))
}

pub struct EnvVar<K: AsRef<OsStr>> {
    key: K,
    old_value: Option<OsString>,
}

impl<K: AsRef<OsStr>> EnvVar<K> {
    pub fn new(key: K, old_value: Option<OsString>) -> Self {
        Self { key, old_value }
    }
}

impl<K: AsRef<OsStr>> Drop for EnvVar<K> {
    fn drop(&mut self) {
        if let Some(value) = self.old_value.take() {
            std::env::set_var(self.key.as_ref(), value);
        } else {
            std::env::remove_var(self.key.as_ref());
        }
    }
}
