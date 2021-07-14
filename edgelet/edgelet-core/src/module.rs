// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::default::Default;
use std::fmt;
use std::result::Result as StdResult;
use std::str::FromStr;
use std::string::ToString;
use std::time::Duration;

use chrono::prelude::*;
use failure::{Fail, ResultExt};
use futures::sync::mpsc::UnboundedSender;
use futures::{Future, Stream};
use serde_derive::Serialize;

use edgelet_utils::ensure_not_empty_with_context;
use futures::sync::oneshot::Sender;

use crate::error::{Error, ErrorKind, Result};
use crate::settings::RuntimeSettings;

#[derive(Clone, Copy, Debug, serde_derive::Deserialize, PartialEq, serde_derive::Serialize)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Unknown,
    Running,
    Stopped,
    Failed,
}

pub enum ModuleAction {
    Start(String, Sender<()>),
    Stop(String),
    Remove(String),
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

#[derive(serde_derive::Serialize, serde_derive::Deserialize, Debug, PartialEq, Clone)]
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

#[derive(serde_derive::Deserialize, Debug, serde_derive::Serialize)]
pub struct ModuleSpec<T> {
    pub name: String,
    #[serde(rename = "type")]
    pub type_: String,
    #[serde(default)]
    #[serde(rename = "imagePullPolicy")]
    pub image_pull_policy: ImagePullPolicy,
    pub config: T,
    #[serde(default)]
    pub env: BTreeMap<String, String>,
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

/// In nested scenario, Agent image can be pulled from its parent.
/// It is possible to specify the parent address using the keyword $upstream
///
/// Unfortunately, due to the particularly runtime-independent and generic
/// nature of configurations, it's very difficult to do "late binding" of
/// runtime specific configuration values, such as the the $upstream
/// `parent_hostname` resolution, which can only be done _after_ fetching the
/// `parent_hostname` value from the underlying aziot identity service.
///
/// As the name implies, this trait is a bodge that enables this functionality,
/// and was added in the lead-up to 1.2 GA.
///
/// A proper rework of settings loading should be undertaken when there is more
/// time, but for now, this will have to do...
pub trait NestedEdgeBodge {
    fn parent_hostname_resolve(&mut self, parent_hostname: &str);
}

impl<T> ModuleSpec<T>
where
    T: NestedEdgeBodge,
{
    pub fn parent_hostname_resolve(&mut self, parent_hostname: &str) {
        self.config.parent_hostname_resolve(parent_hostname);
    }
}

impl<T> ModuleSpec<T> {
    pub fn new(
        name: String,
        type_: String,
        config: T,
        env: BTreeMap<String, String>,
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

    pub fn env(&self) -> &BTreeMap<String, String> {
        &self.env
    }

    pub fn env_mut(&mut self) -> &mut BTreeMap<String, String> {
        &mut self.env
    }

    pub fn with_env(mut self, env: BTreeMap<String, String>) -> Self {
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
    timestamps: bool,
    since: i32,
    until: Option<i32>,
}

impl LogOptions {
    pub fn new() -> Self {
        LogOptions {
            follow: false,
            tail: LogTail::All,
            timestamps: false,
            since: 0,
            until: None,
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

    pub fn with_until(mut self, until: i32) -> Self {
        self.until = Some(until);
        self
    }

    pub fn with_timestamps(mut self, timestamps: bool) -> Self {
        self.timestamps = timestamps;
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

    pub fn until(&self) -> Option<i32> {
        self.until
    }

    pub fn timestamps(&self) -> bool {
        self.timestamps
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

#[derive(Debug, Default, Serialize)]
pub struct SystemInfo {
    /// OS Type of the Host. Example of value expected: \"linux\" and \"windows\".
    #[serde(rename = "osType")]
    pub os_type: String,
    /// Hardware architecture of the host. Example of value expected: arm32, x86, amd64
    pub architecture: String,
    /// iotedge version string
    pub version: &'static str,
    pub provisioning: ProvisioningInfo,
    pub server_version: String,
    pub kernel_version: String,
    pub operating_system: String,
    pub cpus: i32,
    pub virtualized: &'static str,
}

#[derive(Clone, Debug, Default, Serialize)]
pub struct ProvisioningInfo {
    /// IoT Edge provisioning type, examples: manual.device_connection_string, dps.x509
    pub r#type: String,
    #[serde(rename = "dynamicReprovisioning")]
    pub dynamic_reprovisioning: bool,
    #[serde(rename = "alwaysReprovisionOnStartup")]
    pub always_reprovision_on_startup: bool,
}

#[derive(Debug, serde_derive::Serialize)]
pub struct SystemResources {
    host_uptime: u64,
    process_uptime: u64,
    used_cpu: f64,
    used_ram: u64,
    total_ram: u64,
    disks: Vec<DiskInfo>,
    docker_stats: String,
}

impl SystemResources {
    pub fn new(
        host_uptime: u64,
        process_uptime: u64,
        used_cpu: f64,
        used_ram: u64,
        total_ram: u64,
        disks: Vec<DiskInfo>,
        docker_stats: String,
    ) -> Self {
        SystemResources {
            host_uptime,
            process_uptime,
            used_cpu,
            used_ram,
            total_ram,
            disks,
            docker_stats,
        }
    }
}

#[derive(Debug, serde_derive::Serialize)]
pub struct DiskInfo {
    name: String,
    available_space: u64,
    total_space: u64,
    file_system: String,
    file_type: String,
}

impl DiskInfo {
    pub fn new(
        name: String,
        available_space: u64,
        total_space: u64,
        file_system: String,
        file_type: String,
    ) -> Self {
        DiskInfo {
            name,
            available_space,
            total_space,
            file_system,
            file_type,
        }
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

pub trait ProvisioningResult {
    fn device_id(&self) -> &str;
    fn hub_name(&self) -> &str;
}

pub trait MakeModuleRuntime {
    type Config: Clone + Send;
    type Settings: RuntimeSettings<Config = Self::Config>;
    type ModuleRuntime: ModuleRuntime<Config = Self::Config>;
    type Error: Fail;
    type Future: Future<Item = Self::ModuleRuntime, Error = Self::Error> + Send;

    fn make_runtime(
        settings: Self::Settings,
        create_socket_channel: UnboundedSender<ModuleAction>,
    ) -> Self::Future;
}

pub trait ModuleRuntime: Sized {
    type Error: Fail;

    type Config: Clone + Send;
    type Module: Module<Config = Self::Config> + Send;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config, Error = Self::Error>;
    type Chunk: AsRef<[u8]>;
    type Logs: Stream<Item = Self::Chunk, Error = Self::Error> + Send;

    type CreateFuture: Future<Item = (), Error = Self::Error> + Send;
    type GetFuture: Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send;
    type ListFuture: Future<Item = Vec<Self::Module>, Error = Self::Error> + Send;
    type ListWithDetailsStream: Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error>
        + Send;
    type LogsFuture: Future<Item = Self::Logs, Error = Self::Error> + Send;
    type RemoveFuture: Future<Item = (), Error = Self::Error> + Send;
    type RestartFuture: Future<Item = (), Error = Self::Error> + Send;
    type StartFuture: Future<Item = (), Error = Self::Error> + Send;
    type StopFuture: Future<Item = (), Error = Self::Error> + Send;
    type SystemInfoFuture: Future<Item = SystemInfo, Error = Self::Error> + Send;
    type SystemResourcesFuture: Future<Item = SystemResources, Error = Self::Error> + Send;
    type RemoveAllFuture: Future<Item = (), Error = Self::Error> + Send;
    type StopAllFuture: Future<Item = (), Error = Self::Error> + Send;

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture;
    fn get(&self, id: &str) -> Self::GetFuture;
    fn start(&self, id: &str) -> Self::StartFuture;
    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture;
    fn restart(&self, id: &str) -> Self::RestartFuture;
    fn remove(&self, id: &str) -> Self::RemoveFuture;
    fn system_info(&self) -> Self::SystemInfoFuture;
    fn system_resources(&self) -> Self::SystemResourcesFuture;
    fn list(&self) -> Self::ListFuture;
    fn list_with_details(&self) -> Self::ListWithDetailsStream;
    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture;
    fn registry(&self) -> &Self::ModuleRegistry;
    fn remove_all(&self) -> Self::RemoveAllFuture;
    fn stop_all(&self, wait_before_kill: Option<Duration>) -> Self::StopAllFuture;
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
    GetSupportBundle,
    Init,
    ListModules,
    RemoveModule(String),
    RestartModule(String),
    StartModule(String),
    StopModule(String),
    SystemInfo,
    SystemResources,
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
            RuntimeOperation::GetSupportBundle => write!(f, "Could not get support bundle"),
            RuntimeOperation::Init => write!(f, "Could not initialize module runtime"),
            RuntimeOperation::ListModules => write!(f, "Could not list modules"),
            RuntimeOperation::RemoveModule(name) => write!(f, "Could not remove module {}", name),
            RuntimeOperation::RestartModule(name) => write!(f, "Could not restart module {}", name),
            RuntimeOperation::StartModule(name) => write!(f, "Could not start module {}", name),
            RuntimeOperation::StopModule(name) => write!(f, "Could not stop module {}", name),
            RuntimeOperation::SystemInfo => write!(f, "Could not query system info"),
            RuntimeOperation::SystemResources => write!(f, "Could not query system resources"),
            RuntimeOperation::TopModule(name) => write!(f, "Could not top module {}", name),
        }
    }
}

#[derive(Clone, Copy, Debug, serde_derive::Deserialize, PartialEq, serde_derive::Serialize)]
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
    use super::{BTreeMap, Default, ImagePullPolicy, ModuleSpec};

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
            BTreeMap::new(),
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
            BTreeMap::new(),
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
            BTreeMap::new(),
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
            BTreeMap::new(),
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
}
