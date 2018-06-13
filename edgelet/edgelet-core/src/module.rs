// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::default::Default;
use std::fmt;
use std::result::Result as StdResult;
use std::str::FromStr;
use std::string::ToString;
use std::time::Duration;

use chrono::prelude::*;
use failure::Fail;
use futures::{Future, Stream};
use pid::Pid;
use serde_json;

use error::{Error, Result};

#[derive(Serialize, Deserialize, Debug, PartialEq, Clone)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Unknown,
    Running,
    Stopped,
    Failed,
}

impl FromStr for ModuleStatus {
    type Err = serde_json::Error;

    fn from_str(s: &str) -> StdResult<Self, Self::Err> {
        serde_json::from_str(&format!("\"{}\"", s))
    }
}

impl fmt::Display for ModuleStatus {
    fn fmt(&self, formatter: &mut fmt::Formatter) -> fmt::Result {
        write!(
            formatter,
            "{}",
            serde_json::to_string(self)
                .map(|s| s.trim_matches('"').to_string())
                .map_err(|_| fmt::Error)?
        )
    }
}

#[derive(Serialize, Deserialize, Debug, PartialEq, Clone)]
pub struct ModuleRuntimeState {
    status: ModuleStatus,
    exit_code: Option<i32>,
    status_description: Option<String>,
    started_at: Option<DateTime<Utc>>,
    finished_at: Option<DateTime<Utc>>,
    image_id: Option<String>,
    pid: Pid,
}

impl Default for ModuleRuntimeState {
    fn default() -> ModuleRuntimeState {
        ModuleRuntimeState {
            status: ModuleStatus::Unknown,
            exit_code: None,
            status_description: None,
            started_at: None,
            finished_at: None,
            image_id: None,
            pid: Pid::None,
        }
    }
}

impl ModuleRuntimeState {
    pub fn status(&self) -> &ModuleStatus {
        &self.status
    }

    pub fn with_status(mut self, status: ModuleStatus) -> ModuleRuntimeState {
        self.status = status;
        self
    }

    pub fn exit_code(&self) -> Option<&i32> {
        self.exit_code.as_ref()
    }

    pub fn with_exit_code(mut self, exit_code: Option<i32>) -> ModuleRuntimeState {
        self.exit_code = exit_code;
        self
    }

    pub fn status_description(&self) -> Option<&String> {
        self.status_description.as_ref()
    }

    pub fn with_status_description(
        mut self,
        status_description: Option<String>,
    ) -> ModuleRuntimeState {
        self.status_description = status_description;
        self
    }

    pub fn started_at(&self) -> Option<&DateTime<Utc>> {
        self.started_at.as_ref()
    }

    pub fn with_started_at(mut self, started_at: Option<DateTime<Utc>>) -> ModuleRuntimeState {
        self.started_at = started_at;
        self
    }

    pub fn finished_at(&self) -> Option<&DateTime<Utc>> {
        self.finished_at.as_ref()
    }

    pub fn with_finished_at(mut self, finished_at: Option<DateTime<Utc>>) -> ModuleRuntimeState {
        self.finished_at = finished_at;
        self
    }

    pub fn image_id(&self) -> Option<&String> {
        self.image_id.as_ref()
    }

    pub fn with_image_id(mut self, image_id: Option<String>) -> ModuleRuntimeState {
        self.image_id = image_id;
        self
    }

    pub fn pid(&self) -> &Pid {
        &self.pid
    }

    pub fn with_pid(mut self, pid: &Pid) -> ModuleRuntimeState {
        self.pid = pid.clone();
        self
    }
}

#[derive(Deserialize, Debug, Serialize)]
pub struct ModuleSpec<T> {
    name: String,
    #[serde(rename = "type")]
    type_: String,
    config: T,
    #[serde(default = "HashMap::new")]
    env: HashMap<String, String>,
}

impl<T> Clone for ModuleSpec<T>
where
    T: Clone,
{
    fn clone(&self) -> Self {
        Self {
            name: self.name.clone(),
            type_: self.type_.clone(),
            config: self.config.clone(),
            env: self.env.clone(),
        }
    }
}

impl<T> ModuleSpec<T> {
    pub fn new(
        name: &str,
        type_: &str,
        config: T,
        env: HashMap<String, String>,
    ) -> Result<ModuleSpec<T>> {
        Ok(ModuleSpec {
            name: ensure_not_empty!(name).to_string(),
            type_: ensure_not_empty!(type_).to_string(),
            config,
            env,
        })
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn type_(&self) -> &str {
        &self.type_
    }

    pub fn with_type_(mut self, type_: String) -> Self {
        self.type_ = type_;
        self
    }

    pub fn config(&self) -> &T {
        &self.config
    }

    pub fn config_mut(&mut self) -> &mut T {
        &mut self.config
    }

    pub fn with_config(mut self, config: T) -> Self {
        self.config = config;
        self
    }

    pub fn set_config(&mut self, config: T) {
        self.config = config;
    }

    pub fn env(&self) -> &HashMap<String, String> {
        &self.env
    }

    pub fn with_env(mut self, env: HashMap<String, String>) -> Self {
        self.env = env;
        self
    }
}

#[derive(Debug, PartialEq)]
pub enum LogTail {
    All,
    Num(u64),
}

impl Default for LogTail {
    fn default() -> Self {
        LogTail::All
    }
}

impl FromStr for LogTail {
    type Err = Error;

    fn from_str(s: &str) -> Result<Self> {
        let tail = if s == "all" {
            LogTail::All
        } else {
            let num = s.parse()?;
            LogTail::Num(num)
        };
        Ok(tail)
    }
}

impl ToString for LogTail {
    fn to_string(&self) -> String {
        match self {
            LogTail::All => "all".to_string(),
            LogTail::Num(n) => n.to_string(),
        }
    }
}

#[derive(Debug, Default)]
pub struct LogOptions {
    follow: bool,
    tail: LogTail,
}

impl LogOptions {
    pub fn new() -> Self {
        LogOptions {
            follow: false,
            tail: LogTail::All,
        }
    }

    pub fn with_follow(mut self, follow: bool) -> Self {
        self.follow = follow;
        self
    }

    pub fn with_tail(mut self, tail: LogTail) -> Self {
        self.tail = tail;
        self
    }

    pub fn follow(&self) -> bool {
        self.follow
    }

    pub fn tail(&self) -> &LogTail {
        &self.tail
    }
}

pub trait Module {
    type Config;
    type Error: Fail;
    type RuntimeStateFuture: Future<Item = ModuleRuntimeState, Error = Self::Error>;

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn config(&self) -> &Self::Config;
    fn runtime_state(&self) -> Self::RuntimeStateFuture;
}

pub trait ModuleRegistry {
    type Error: Fail;
    type PullFuture: Future<Item = (), Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;
    type Config;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture;
    fn remove(&self, name: &str) -> Self::RemoveFuture;
}

#[derive(Debug)]
pub struct SystemInfo {
    /// OS Type of the Host. Example of value expected: \"linux\" and \"windows\".
    os_type: String,
    /// Hardware architecture of the host. Example of value expected: arm32, x86, amd64
    architecture: String,
}

impl SystemInfo {
    pub fn new(os_type: String, architecture: String) -> Self {
        SystemInfo {
            os_type,
            architecture,
        }
    }

    pub fn os_type(&self) -> &str {
        &self.os_type
    }

    pub fn architecture(&self) -> &str {
        &self.architecture
    }
}

pub trait ModuleRuntime {
    type Error: Fail;

    type Config;
    type Module: Module<Config = Self::Config>;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config, Error = Self::Error>;
    type Chunk: AsRef<[u8]>;
    type Logs: Stream<Item = Self::Chunk, Error = Self::Error>;

    type CreateFuture: Future<Item = (), Error = Self::Error>;
    type InitFuture: Future<Item = (), Error = Self::Error>;
    type ListFuture: Future<Item = Vec<Self::Module>, Error = Self::Error>;
    type LogsFuture: Future<Item = Self::Logs, Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;
    type RestartFuture: Future<Item = (), Error = Self::Error>;
    type StartFuture: Future<Item = (), Error = Self::Error>;
    type StopFuture: Future<Item = (), Error = Self::Error>;
    type SystemInfoFuture: Future<Item = SystemInfo, Error = Self::Error>;

    fn init(&self) -> Self::InitFuture;
    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture;
    fn start(&self, id: &str) -> Self::StartFuture;
    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture;
    fn restart(&self, id: &str) -> Self::RestartFuture;
    fn remove(&self, id: &str) -> Self::RemoveFuture;
    fn system_info(&self) -> Self::SystemInfoFuture;
    fn list(&self) -> Self::ListFuture;
    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture;
    fn registry(&self) -> &Self::ModuleRegistry;
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::str::FromStr;
    use std::string::ToString;

    use error::ErrorKind;
    use module::ModuleStatus;

    fn get_inputs() -> Vec<(&'static str, ModuleStatus)> {
        vec![
            ("unknown", ModuleStatus::Unknown),
            ("running", ModuleStatus::Running),
            ("stopped", ModuleStatus::Stopped),
            ("failed", ModuleStatus::Failed),
        ]
    }

    #[test]
    fn module_status_ser() {
        let inputs = get_inputs();
        for &(expected, ref status) in &inputs {
            assert_eq!(expected, &status.to_string());
        }
    }

    #[test]
    fn module_status_deser() {
        let inputs = get_inputs();
        for &(status, ref expected) in &inputs {
            assert_eq!(*expected, ModuleStatus::from_str(status).unwrap());
        }
    }

    #[test]
    fn module_config_empty_name_fails() {
        match ModuleSpec::new("", "docker", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_white_space_name_fails() {
        match ModuleSpec::new("    ", "docker", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_empty_type_fails() {
        match ModuleSpec::new("m1", "", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_white_space_type_fails() {
        match ModuleSpec::new("m1", "     ", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn system_info_new_and_access_succeed() {
        //arrange
        let system_info = SystemInfo::new(
            "testValueOsType".to_string(),
            "testArchitectureType".to_string(),
        );
        let expected_value_os_type = "testValueOsType";
        let expected_test_architecture_type = "testArchitectureType";

        //act
        let current_value_os_type = system_info.os_type();
        let current_value_architecture_type = system_info.architecture();

        //assert
        assert_eq!(expected_value_os_type, current_value_os_type);
        assert_eq!(
            expected_test_architecture_type,
            current_value_architecture_type
        );
    }
}
