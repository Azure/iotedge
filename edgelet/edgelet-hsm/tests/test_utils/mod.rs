use std::env;
use std::path::{Path, PathBuf};
use std::sync::{Mutex, MutexGuard};
use tempfile::TempDir;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

pub struct TestHSMEnvSetup<'a> {
    _guard: MutexGuard<'a, ()>,
    home_dir: Option<TempDir>,
    path: PathBuf,
}

impl<'a> TestHSMEnvSetup<'a> {
    pub fn new(m: &'a Mutex<()>, home_dir: Option<&str>) -> Self {
        let guard = m.lock().unwrap();

        let (temp_dir, path) = home_dir.map_or_else(
            || {
                let td = TempDir::new().unwrap();
                let p = td.path().to_path_buf();
                (Some(td), p)
            },
            |d| (None, PathBuf::from(d)),
        );
        env::set_var(HOMEDIR_KEY, path.as_os_str());
        println!("IOTEDGE_HOMEDIR set to {:#?}", &path);
        TestHSMEnvSetup {
            _guard: guard,
            home_dir: temp_dir,
            path,
        }
    }

    #[allow(dead_code)]
    pub fn get_path(&self) -> &Path {
        self.path.as_path()
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
