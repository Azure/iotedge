use std::fs;
use std::fs::File;
use std::io::prelude::*;
use std::path::Path;

use anyhow::Context;

use edgelet_settings::RuntimeSettings;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct CheckFsCalls {}

#[async_trait::async_trait]
impl Checker for CheckFsCalls {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "check-fs-calls",
            description: "IoT Edge can perform File System Operations",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl CheckFsCalls {
    #[allow(clippy::unused_self)]
    #[allow(unused_variables)]
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let home_dir_str = match edgelet_settings::Settings::new() {
            Ok(settings) => settings.homedir().to_str().unwrap().to_owned(),
            _ => "/var/lib/aziot/edged".to_string(),
        };

        let home_dir = Path::new(&home_dir_str);

        // Test Read file and Remove file
        File::create(home_dir.join("createfile_test.txt"))
            .with_context(|| format!("Unable to create test file for file read test"))?;
        fs::read(home_dir.join("createfile_test.txt"))
            .with_context(|| format!("Unable to read the test file"))?;
        fs::remove_file(home_dir.join("createfile_test.txt"))
            .with_context(|| format!("Unable to cleanup file create test file"))?;

        // Test write operation
        fs::write(
            home_dir.join("writefile_test.txt"),
            "Testing write operation",
        )
        .with_context(|| format!("Unable to write test file"))?;
        fs::remove_file(home_dir.join("writefile_test.txt"))
            .with_context(|| format!("Unable to cleanup file write test file"))?;

        // Test read_to_string function
        let mut test_file = File::create(home_dir.join("read_to_string_test.txt"))
            .with_context(|| format!("Unable to create test file for read_to_string test"))?;

        test_file
            .write_all(b"Testing Read to string fucntion")
            .with_context(|| format!("Unable to write contents to test file"))?;
        let mut file_contents = String::new();
        let mut open_file = File::open(home_dir.join("read_to_string_test.txt"))
            .with_context(|| format!("Unable to open test file for read_to_string test"))?;
        open_file
            .read_to_string(&mut file_contents)
            .with_context(|| format!("Unable to read the file contents as string"))?;
        assert_eq!(file_contents, "Testing Read to string fucntion");
        fs::remove_file(home_dir.join("read_to_string_test.txt"))
            .with_context(|| format!("Unable to cleanup file read_to_string test file"))?;

        // Test create_dir and remove_dir
        fs::create_dir(home_dir.join("createdirtest1"))
            .with_context(|| format!("Unable to create test directory"))?;
        fs::remove_dir(home_dir.join("createdirtest1"))
            .with_context(|| format!("Unable to remove test directory"))?;

        // Test create_dir_all, read_dir and remove_dir_alliot
        fs::create_dir_all(home_dir.join("createtestdir2/level1/level2"))
            .with_context(|| format!("Unable to perform create_dir_all"))?;
        fs::read_dir(home_dir.join("createtestdir2/level1/level2"))
            .with_context(|| format!("Unable to perform read_dir"))?;
        fs::remove_dir_all(home_dir.join("createtestdir2"))
            .with_context(|| format!("Unable to perform create_dir_all"))?;

        Ok(CheckResult::Ok)
    }
}
