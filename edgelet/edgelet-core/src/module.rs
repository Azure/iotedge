// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::default::Default;
use std::fmt;
use std::result::Result as StdResult;
use std::str::FromStr;
use std::string::ToString;
use std::time::Duration;

use chrono::prelude::*;
use failure::{Fail, ResultExt};
use futures::{Future, Stream};
use serde_derive::{Deserialize, Serialize};
use serde_json;

use edgelet_utils::{ensure_not_empty_with_context, serialize_ordered};

use crate::error::{Error, ErrorKind, Result};

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Serialize)]
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
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
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
    exit_code: Option<i64>,
    status_description: Option<String>,
    started_at: Option<DateTime<Utc>>,
    finished_at: Option<DateTime<Utc>>,
    image_id: Option<String>,
    pid: Option<i32>,
}

impl Default for ModuleRuntimeState {
    fn default() -> Self {
        ModuleRuntimeState {
            status: ModuleStatus::Unknown,
            exit_code: None,
            status_description: None,
            started_at: None,
            finished_at: None,
            image_id: None,
            pid: None,
        }
    }
}

impl ModuleRuntimeState {
    pub fn status(&self) -> &ModuleStatus {
        &self.status
    }

    pub fn with_status(mut self, status: ModuleStatus) -> Self {
        self.status = status;
        self
    }

    pub fn exit_code(&self) -> Option<i64> {
        self.exit_code
    }

    pub fn with_exit_code(mut self, exit_code: Option<i64>) -> Self {
        self.exit_code = exit_code;
        self
    }

    pub fn status_description(&self) -> Option<&str> {
        self.status_description.as_ref().map(AsRef::as_ref)
    }

    pub fn with_status_description(mut self, status_description: Option<String>) -> Self {
        self.status_description = status_description;
        self
    }

    pub fn started_at(&self) -> Option<&DateTime<Utc>> {
        self.started_at.as_ref()
    }

    pub fn with_started_at(mut self, started_at: Option<DateTime<Utc>>) -> Self {
        self.started_at = started_at;
        self
    }

    pub fn finished_at(&self) -> Option<&DateTime<Utc>> {
        self.finished_at.as_ref()
    }

    pub fn with_finished_at(mut self, finished_at: Option<DateTime<Utc>>) -> Self {
        self.finished_at = finished_at;
        self
    }

    pub fn image_id(&self) -> Option<&str> {
        self.image_id.as_ref().map(AsRef::as_ref)
    }

    pub fn with_image_id(mut self, image_id: Option<String>) -> Self {
        self.image_id = image_id;
        self
    }

    pub fn pid(&self) -> Option<i32> {
        self.pid
    }

    pub fn with_pid(mut self, pid: Option<i32>) -> Self {
        self.pid = pid;
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
    #[serde(serialize_with = "serialize_ordered")]
    env: HashMap<String, String>,
    #[serde(default)]
    #[serde(rename = "imagePullPolicy")]
    image_pull_policy: ImagePullPolicy,
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
            image_pull_policy: self.image_pull_policy,
        }
    }
}

impl<T> ModuleSpec<T> {
    pub fn new(
        name: String,
        type_: String,
        config: T,
        env: HashMap<String, String>,
        image_pull_policy: ImagePullPolicy,
    ) -> Result<Self> {
        ensure_not_empty_with_context(&name, || ErrorKind::InvalidModuleName(name.clone()))?;
        ensure_not_empty_with_context(&type_, || ErrorKind::InvalidModuleType(type_.clone()))?;

        Ok(ModuleSpec {
            name,
            type_,
            config,
            env,
            image_pull_policy,
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

    pub fn image_pull_policy(&self) -> ImagePullPolicy {
        self.image_pull_policy
    }

    pub fn with_image_pull_policy(mut self, image_pull_policy: ImagePullPolicy) -> Self {
        self.image_pull_policy = image_pull_policy;
        self
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
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
            let num = s
                .parse::<u64>()
                .with_context(|_| ErrorKind::InvalidLogTail(s.to_string()))?;
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
    since: i32,
}

impl LogOptions {
    pub fn new() -> Self {
        LogOptions {
            follow: false,
            tail: LogTail::All,
            since: 0,
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

    pub fn with_since(mut self, since: i32) -> Self {
        self.since = since;
        self
    }

    pub fn follow(&self) -> bool {
        self.follow
    }

    pub fn tail(&self) -> &LogTail {
        &self.tail
    }

    pub fn since(&self) -> i32 {
        self.since
    }
}

pub trait Module {
    type Config;
    type Error: Fail;
    type RuntimeStateFuture: Future<Item = ModuleRuntimeState, Error = Self::Error> + Send;

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn config(&self) -> &Self::Config;
    fn runtime_state(&self) -> Self::RuntimeStateFuture;
}

pub trait ModuleRegistry {
    type Error: Fail;
    type PullFuture: Future<Item = (), Error = Self::Error> + Send;
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
    /// iotedge version string
    version: &'static str,
}

impl SystemInfo {
    pub fn new(os_type: String, architecture: String) -> Self {
        SystemInfo {
            os_type,
            architecture,
            version: super::version_with_source_version(),
        }
    }

    pub fn os_type(&self) -> &str {
        &self.os_type
    }

    pub fn architecture(&self) -> &str {
        &self.architecture
    }

    pub fn version(&self) -> &str {
        self.version
    }
}

#[derive(Debug)]
pub struct ModuleTop {
    /// Name of the module. Example: tempSensor
    name: String,
    /// A vector of process IDs (PIDs) representing a snapshot of all processes running inside the module.
    process_ids: Vec<i32>,
}

impl ModuleTop {
    pub fn new(name: String, process_ids: Vec<i32>) -> Self {
        ModuleTop { name, process_ids }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn process_ids(&self) -> &[i32] {
        &self.process_ids
    }
}

pub trait ModuleRuntime {
    type Error: Fail;

    type Config: Send;
    type Module: Module<Config = Self::Config> + Send;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config, Error = Self::Error>;
    type Chunk: AsRef<[u8]>;
    type Logs: Stream<Item = Self::Chunk, Error = Self::Error> + Send;

    type CreateFuture: Future<Item = (), Error = Self::Error> + Send;
    type GetFuture: Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send;
    type InitFuture: Future<Item = (), Error = Self::Error> + Send;
    type ListFuture: Future<Item = Vec<Self::Module>, Error = Self::Error> + Send;
    type ListWithDetailsStream: Stream<
            Item = (Self::Module, ModuleRuntimeState),
            Error = Self::Error,
        > + Send;
    type LogsFuture: Future<Item = Self::Logs, Error = Self::Error> + Send;
    type RemoveFuture: Future<Item = (), Error = Self::Error> + Send;
    type RestartFuture: Future<Item = (), Error = Self::Error> + Send;
    type StartFuture: Future<Item = (), Error = Self::Error> + Send;
    type StopFuture: Future<Item = (), Error = Self::Error> + Send;
    type SystemInfoFuture: Future<Item = SystemInfo, Error = Self::Error> + Send;
    type RemoveAllFuture: Future<Item = (), Error = Self::Error> + Send;

    fn init(&self) -> Self::InitFuture;
    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture;
    fn get(&self, id: &str) -> Self::GetFuture;
    fn start(&self, id: &str) -> Self::StartFuture;
    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture;
    fn restart(&self, id: &str) -> Self::RestartFuture;
    fn remove(&self, id: &str) -> Self::RemoveFuture;
    fn system_info(&self) -> Self::SystemInfoFuture;
    fn list(&self) -> Self::ListFuture;
    fn list_with_details(&self) -> Self::ListWithDetailsStream;
    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture;
    fn registry(&self) -> &Self::ModuleRegistry;
    fn remove_all(&self) -> Self::RemoveAllFuture;
}

#[derive(Clone, Copy, Debug)]
pub enum ModuleRuntimeErrorReason {
    NotFound,
    Other,
}

// Useful for error contexts
#[derive(Clone, Copy, Debug)]
pub enum ModuleOperation {
    RuntimeState,
}

impl fmt::Display for ModuleOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ModuleOperation::RuntimeState => write!(f, "Could not query module runtime state"),
        }
    }
}

// Useful for error contexts
#[derive(Clone, Debug)]
pub enum RegistryOperation {
    PullImage(String),
    RemoveImage(String),
}

impl fmt::Display for RegistryOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            RegistryOperation::PullImage(name) => write!(f, "Could not pull image {}", name),
            RegistryOperation::RemoveImage(name) => write!(f, "Could not remove image {}", name),
        }
    }
}

// Useful for error contexts
#[derive(Clone, Debug, PartialEq)]
pub enum RuntimeOperation {
    CreateModule(String),
    GetModule(String),
    GetModuleLogs(String),
    Init,
    ListModules,
    RemoveModule(String),
    RestartModule(String),
    StartModule(String),
    StopModule(String),
    SystemInfo,
    TopModule(String),
}

impl fmt::Display for RuntimeOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            RuntimeOperation::CreateModule(name) => write!(f, "Could not create module {}", name),
            RuntimeOperation::GetModule(name) => write!(f, "Could not get module {}", name),
            RuntimeOperation::GetModuleLogs(name) => {
                write!(f, "Could not get logs for module {}", name)
            }
            RuntimeOperation::Init => write!(f, "Could not initialize module runtime"),
            RuntimeOperation::ListModules => write!(f, "Could not list modules"),
            RuntimeOperation::RemoveModule(name) => write!(f, "Could not remove module {}", name),
            RuntimeOperation::RestartModule(name) => write!(f, "Could not restart module {}", name),
            RuntimeOperation::StartModule(name) => write!(f, "Could not start module {}", name),
            RuntimeOperation::StopModule(name) => write!(f, "Could not stop module {}", name),
            RuntimeOperation::SystemInfo => write!(f, "Could not query system info"),
            RuntimeOperation::TopModule(name) => write!(f, "Could not top module {}", name),
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum ImagePullPolicy {
    #[serde(rename = "on-create")]
    OnCreate,
    Never,
}

impl Default for ImagePullPolicy {
    fn default() -> Self {
        ImagePullPolicy::OnCreate
    }
}

impl FromStr for ImagePullPolicy {
    type Err = Error;

    fn from_str(s: &str) -> StdResult<ImagePullPolicy, Self::Err> {
        match s.to_lowercase().as_str() {
            "on-create" => Ok(ImagePullPolicy::OnCreate),
            "never" => Ok(ImagePullPolicy::Never),
            _ => Err(Error::from(ErrorKind::InvalidImagePullPolicy(
                s.to_string(),
            ))),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::str::FromStr;
    use std::string::ToString;

    use crate::error::ErrorKind;
    use crate::module::ModuleStatus;

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
        let name = "".to_string();
        match ModuleSpec::new(
            name.clone(),
            "docker".to_string(),
            10_i32,
            HashMap::new(),
            ImagePullPolicy::default(),
        ) {
            Ok(_) => panic!("Expected error"),
            Err(err) => {
                if let ErrorKind::InvalidModuleName(s) = err.kind() {
                    assert_eq!(s, &name);
                } else {
                    panic!("Expected `InvalidModuleName` but got {:?}", err);
                }
            }
        }
    }

    #[test]
    fn module_config_white_space_name_fails() {
        let name = "    ".to_string();
        match ModuleSpec::new(
            name.clone(),
            "docker".to_string(),
            10_i32,
            HashMap::new(),
            ImagePullPolicy::default(),
        ) {
            Ok(_) => panic!("Expected error"),
            Err(err) => {
                if let ErrorKind::InvalidModuleName(s) = err.kind() {
                    assert_eq!(s, &name);
                } else {
                    panic!("Expected `InvalidModuleName` but got {:?}", err);
                }
            }
        }
    }

    #[test]
    fn module_config_empty_type_fails() {
        let type_ = "    ".to_string();
        match ModuleSpec::new(
            "m1".to_string(),
            type_.clone(),
            10_i32,
            HashMap::new(),
            ImagePullPolicy::default(),
        ) {
            Ok(_) => panic!("Expected error"),
            Err(err) => {
                if let ErrorKind::InvalidModuleType(s) = err.kind() {
                    assert_eq!(s, &type_);
                } else {
                    panic!("Expected `InvalidModuleType` but got {:?}", err);
                }
            }
        }
    }

    #[test]
    fn module_config_white_space_type_fails() {
        let type_ = "    ".to_string();
        match ModuleSpec::new(
            "m1".to_string(),
            type_.clone(),
            10_i32,
            HashMap::new(),
            ImagePullPolicy::default(),
        ) {
            Ok(_) => panic!("Expected error"),
            Err(err) => {
                if let ErrorKind::InvalidModuleType(s) = err.kind() {
                    assert_eq!(s, &type_);
                } else {
                    panic!("Expected `InvalidModuleType` but got {:?}", err);
                }
            }
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
