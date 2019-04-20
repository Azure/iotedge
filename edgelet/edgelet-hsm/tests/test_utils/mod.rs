
use std::{env};
use std::sync::{Mutex, MutexGuard};
use tempfile::TempDir;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

pub struct TestHSMEnvSetup<'a> {
    _guard: MutexGuard<'a, ()>,
    home_dir: Option<TempDir>,
}

impl<'a> TestHSMEnvSetup<'a> {
    pub fn new(m: &'a Mutex<()>) -> Self {
        let guard = m.lock().unwrap();
        let home_dir = TempDir::new().unwrap();
        env::set_var(HOMEDIR_KEY, &home_dir.path());
        println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());
        TestHSMEnvSetup {
            _guard: guard,
            home_dir: Some(home_dir)
        }
    }
}

impl<'a> Drop for TestHSMEnvSetup<'a> {
    fn drop(&mut self) {
        env::remove_var(HOMEDIR_KEY);
        if self.home_dir.is_some() {
            let hd = self.home_dir.take();
            hd.unwrap().close().unwrap();
        }
    }
}
