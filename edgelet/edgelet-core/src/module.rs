// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::default::Default;
use std::fmt;
use std::result::Result;
use std::str::FromStr;
use std::string::ToString;
use std::time::Duration;

use chrono::prelude::*;
use failure::{Fail, ResultExt};
use serde::{Deserialize, Serialize};
use serde_with::skip_serializing_none;

use aziotctl_common::host_info::{DmiInfo, OsInfo};
use edgelet_settings::module::Settings as ModuleSpec;
use edgelet_settings::RuntimeSettings;
use tokio::sync::mpsc::UnboundedSender;

use crate::error::{Error, ErrorKind, Result as EdgeletResult};

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Unknown,
    Running,
    Stopped,
    Failed,
    Dead,
}

pub enum ModuleAction {
    Start(String, tokio::sync::oneshot::Sender<()>),
    Stop(String),
    Remove(String),
}

impl FromStr for ModuleStatus {
    type Err = serde_json::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
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

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
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

    fn from_str(s: &str) -> EdgeletResult<Self> {
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

#[async_trait::async_trait]
pub trait Module {
    type Config;
    type Error: Fail;

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn config(&self) -> &Self::Config;
    async fn runtime_state(&self) -> Result<ModuleRuntimeState, Self::Error>;
}

#[async_trait::async_trait]
pub trait ModuleRegistry {
    type Config;
    type Error: Fail;

    async fn pull(&self, config: &Self::Config) -> Result<(), Self::Error>;
    async fn remove(&self, name: &str) -> Result<(), Self::Error>;
}

#[skip_serializing_none]
#[derive(Debug, Default, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemInfo {
    pub kernel: String,
    pub kernel_release: String,
    pub kernel_version: String,

    pub operating_system: Option<String>,
    pub operating_system_version: Option<String>,
    pub operating_system_variant: Option<String>,
    pub operating_system_build: Option<String>,

    pub architecture: String,
    pub cpus: usize,
    pub virtualized: String,

    pub product_name: Option<String>,
    pub system_vendor: Option<String>,

    pub version: String,
    pub provisioning: ProvisioningInfo,

    #[serde(default, flatten)]
    pub additional_properties: BTreeMap<String, String>,
}

impl SystemInfo {
    pub fn from_system() -> Result<Self, Error> {
        let kernel = nix::sys::utsname::uname();
        let dmi = DmiInfo::default();
        let os = OsInfo::default();

        let res = Self {
            kernel: kernel.sysname().to_owned(),
            kernel_release: kernel.release().to_owned(),
            kernel_version: kernel.version().to_owned(),

            operating_system: os.id,
            operating_system_version: os.version_id,
            operating_system_variant: os.variant_id,
            operating_system_build: os.build_id,

            architecture: os.arch.to_owned(),
            cpus: num_cpus::get(),
            virtualized: match crate::virtualization::is_virtualized_env() {
                Ok(Some(true)) => "yes",
                Ok(Some(false)) => "no",
                _ => "unknown",
            }
            .to_owned(),

            product_name: dmi.product,
            system_vendor: dmi.vendor,

            version: crate::version_with_source_version(),
            provisioning: ProvisioningInfo {
                r#type: "ProvisioningType".into(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: false,
            },

            additional_properties: BTreeMap::new(),
        };

        Ok(res)
    }

    pub fn merge_additional(&mut self, mut additional_info: BTreeMap<String, String>) -> &Self {
        macro_rules! remove_assign {
            ($src:literal, $dest:ident) => {
                if let Some((_, x)) = additional_info.remove_entry($src) {
                    self.$dest = x.into();
                }
            };
        }

        remove_assign!("kernel_name", kernel);
        remove_assign!("kernel_release", kernel_release);
        remove_assign!("kernel_version", kernel_version);

        remove_assign!("os_name", operating_system);
        remove_assign!("os_version", operating_system_version);
        remove_assign!("os_variant", operating_system_variant);
        remove_assign!("os_build", operating_system_build);

        remove_assign!("cpu_architecture", architecture);

        remove_assign!("product_name", product_name);
        remove_assign!("product_vendor", system_vendor);

        self.additional_properties
            .extend(additional_info.into_iter());

        self
    }
}

#[derive(Clone, Debug, Default, Deserialize, PartialEq, Serialize)]
pub struct ProvisioningInfo {
    /// IoT Edge provisioning type, examples: manual.device_connection_string, dps.x509
    pub r#type: String,
    #[serde(rename = "dynamicReprovisioning")]
    pub dynamic_reprovisioning: bool,
    #[serde(rename = "alwaysReprovisionOnStartup")]
    pub always_reprovision_on_startup: bool,
}

#[derive(Debug, Serialize)]
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

#[derive(Debug, Serialize)]
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

pub trait ProvisioningResult {
    fn device_id(&self) -> &str;
    fn hub_name(&self) -> &str;
}

#[async_trait::async_trait]
pub trait MakeModuleRuntime {
    type Config: Clone + Send;
    type Settings: RuntimeSettings<ModuleConfig = Self::Config>;
    type ModuleRuntime: ModuleRuntime<Config = Self::Config>;
    type Error: Fail;

    async fn make_runtime(
        settings: &Self::Settings,
        create_socket_channel: UnboundedSender<ModuleAction>,
    ) -> Result<Self::ModuleRuntime, Self::Error>;
}

#[async_trait::async_trait]
pub trait ModuleRuntime: Sized {
    type Error: Fail;

    type Config: Clone + Send + serde::Serialize;
    type Module: Module<Config = Self::Config> + Send;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config, Error = Self::Error> + Send + Sync;

    async fn create(&self, module: ModuleSpec<Self::Config>) -> Result<(), Self::Error>;
    async fn get(&self, id: &str) -> Result<(Self::Module, ModuleRuntimeState), Self::Error>;
    async fn start(&self, id: &str) -> Result<(), Self::Error>;
    async fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Result<(), Self::Error>;
    async fn restart(&self, id: &str) -> Result<(), Self::Error>;
    async fn remove(&self, id: &str) -> Result<(), Self::Error>;
    async fn system_info(&self) -> Result<SystemInfo, Self::Error>;
    async fn system_resources(&self) -> Result<SystemResources, Self::Error>;
    async fn list(&self) -> Result<Vec<Self::Module>, Self::Error>;
    async fn list_with_details(
        &self,
    ) -> Result<Vec<(Self::Module, ModuleRuntimeState)>, Self::Error>;
    async fn logs(&self, id: &str, options: &LogOptions) -> Result<hyper::Body, Self::Error>;
    async fn remove_all(&self) -> Result<(), Self::Error>;
    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> Result<(), Self::Error>;
    async fn module_top(&self, id: &str) -> Result<Vec<i32>, Self::Error>;

    fn registry(&self) -> &Self::ModuleRegistry;
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
            RuntimeOperation::TopModule(name) => {
                write!(f, "Could not top module {}.", name)
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;
    use std::str::FromStr;
    use std::string::ToString;

    use edgelet_settings::module::ImagePullPolicy;

    use super::ModuleSpec;
    use crate::module::ModuleStatus;

    fn get_inputs() -> Vec<(&'static str, ModuleStatus)> {
        vec![
            ("unknown", ModuleStatus::Unknown),
            ("running", ModuleStatus::Running),
            ("stopped", ModuleStatus::Stopped),
            ("failed", ModuleStatus::Failed),
            ("dead", ModuleStatus::Dead),
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
        ModuleSpec::new(
            name,
            "docker".to_string(),
            10_i32,
            BTreeMap::new(),
            ImagePullPolicy::default(),
        )
        .unwrap_err();
    }

    #[test]
    fn module_config_white_space_name_fails() {
        let name = "    ".to_string();
        ModuleSpec::new(
            name,
            "docker".to_string(),
            10_i32,
            BTreeMap::new(),
            ImagePullPolicy::default(),
        )
        .unwrap_err();
    }

    #[test]
    fn module_config_empty_type_fails() {
        let type_ = "    ".to_string();
        ModuleSpec::new(
            "m1".to_string(),
            type_,
            10_i32,
            BTreeMap::new(),
            ImagePullPolicy::default(),
        )
        .unwrap_err();
    }

    #[test]
    fn module_config_white_space_type_fails() {
        let type_ = "    ".to_string();
        ModuleSpec::new(
            "m1".to_string(),
            type_,
            10_i32,
            BTreeMap::new(),
            ImagePullPolicy::default(),
        )
        .unwrap_err();
    }
}
