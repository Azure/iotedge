// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::ffi::OsStr;
use std::fmt;
use std::time::Duration;

use anyhow::Context;
use chrono::prelude::*;
use nix::sys::utsname::UtsName;
use serde::{Deserialize, Serialize};

use aziotctl_common::host_info::{DmiInfo, OsInfo};
use edgelet_settings::module::Settings as ModuleSpec;

use crate::error::Error;

#[derive(Clone, Copy, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Unknown,
    Running,
    Stopped,
    Failed,
    Dead,
}

impl Default for ModuleStatus {
    fn default() -> Self {
        Self::Unknown
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

impl std::str::FromStr for ModuleStatus {
    type Err = serde_json::Error;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        serde_json::from_str(&format!("\"{}\"", value))
    }
}

pub enum ModuleAction {
    Start(String, tokio::sync::oneshot::Sender<()>),
    Stop(String),
    Remove(String),
}

#[derive(Clone, Debug, Default, Deserialize, Eq, PartialEq, Serialize)]
pub struct ModuleRuntimeState {
    status: ModuleStatus,
    exit_code: Option<i64>,
    started_at: Option<DateTime<Utc>>,
    finished_at: Option<DateTime<Utc>>,
    image_id: Option<String>,
    pid: Option<i32>,
}

impl ModuleRuntimeState {
    pub fn status(&self) -> &ModuleStatus {
        &self.status
    }

    #[must_use]
    pub fn with_status(mut self, status: ModuleStatus) -> Self {
        self.status = status;
        self
    }

    pub fn exit_code(&self) -> Option<i64> {
        self.exit_code
    }

    #[must_use]
    pub fn with_exit_code(mut self, exit_code: Option<i64>) -> Self {
        self.exit_code = exit_code;
        self
    }

    pub fn started_at(&self) -> Option<&DateTime<Utc>> {
        self.started_at.as_ref()
    }

    #[must_use]
    pub fn with_started_at(mut self, started_at: Option<DateTime<Utc>>) -> Self {
        self.started_at = started_at;
        self
    }

    pub fn finished_at(&self) -> Option<&DateTime<Utc>> {
        self.finished_at.as_ref()
    }

    #[must_use]
    pub fn with_finished_at(mut self, finished_at: Option<DateTime<Utc>>) -> Self {
        self.finished_at = finished_at;
        self
    }

    pub fn image_id(&self) -> Option<&str> {
        self.image_id.as_ref().map(AsRef::as_ref)
    }

    #[must_use]
    pub fn with_image_id(mut self, image_id: Option<String>) -> Self {
        self.image_id = image_id;
        self
    }

    pub fn pid(&self) -> Option<i32> {
        self.pid
    }

    #[must_use]
    pub fn with_pid(mut self, pid: Option<i32>) -> Self {
        self.pid = pid;
        self
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum LogTail {
    All,
    Num(u64),
}

impl Default for LogTail {
    fn default() -> Self {
        Self::All
    }
}

impl fmt::Display for LogTail {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::All => write!(formatter, "all"),
            Self::Num(n) => write!(formatter, "{}", n),
        }
    }
}

impl std::str::FromStr for LogTail {
    type Err = anyhow::Error;

    fn from_str(s: &str) -> anyhow::Result<Self> {
        let tail = if s == "all" {
            LogTail::All
        } else {
            let num = s
                .parse::<u64>()
                .with_context(|| Error::InvalidLogTail(s.to_string()))?;
            LogTail::Num(num)
        };
        Ok(tail)
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

    #[must_use]
    pub fn with_follow(mut self, follow: bool) -> Self {
        self.follow = follow;
        self
    }

    #[must_use]
    pub fn with_tail(mut self, tail: LogTail) -> Self {
        self.tail = tail;
        self
    }

    #[must_use]
    pub fn with_since(mut self, since: i32) -> Self {
        self.since = since;
        self
    }

    #[must_use]
    pub fn with_until(mut self, until: i32) -> Self {
        self.until = Some(until);
        self
    }

    #[must_use]
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

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn config(&self) -> &Self::Config;
    async fn runtime_state(&self) -> anyhow::Result<ModuleRuntimeState>;
}

#[async_trait::async_trait]
pub trait ModuleRegistry {
    type Config;

    async fn pull(&self, config: &Self::Config) -> anyhow::Result<()>;
    async fn remove(&self, name: &str) -> anyhow::Result<()>;
}

#[derive(Debug, Eq, PartialEq, Serialize)]
pub struct SystemInfo {
    #[serde(rename = "osType")]
    pub kernel: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub kernel_release: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub kernel_version: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub operating_system: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub operating_system_version: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub operating_system_variant: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub operating_system_build: Option<String>,

    pub architecture: String,
    pub cpus: u64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub total_memory: Option<u64>,
    pub virtualized: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub product_name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub system_vendor: Option<String>,

    pub version: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub server_version: Option<String>,

    pub provisioning: ProvisioningInfo,

    #[serde(default, flatten, skip_serializing_if = "BTreeMap::is_empty")]
    pub additional_properties: BTreeMap<String, String>,
}

impl SystemInfo {
    pub fn merge_additional(&mut self, mut additional_info: BTreeMap<String, String>) -> &Self {
        macro_rules! remove_assign {
            ($key:ident) => {
                if let Some((_, x)) = additional_info.remove_entry(stringify!($key)) {
                    self.$key = x.into();
                }
            };
        }

        remove_assign!(kernel);
        remove_assign!(kernel_release);
        remove_assign!(kernel_version);

        remove_assign!(operating_system);
        remove_assign!(operating_system_version);
        remove_assign!(operating_system_variant);
        remove_assign!(operating_system_build);

        remove_assign!(architecture);
        remove_assign!(virtualized);

        remove_assign!(product_name);
        remove_assign!(system_vendor);

        remove_assign!(server_version);

        self.additional_properties
            .extend(additional_info.into_iter());

        self
    }
}

impl Default for SystemInfo {
    fn default() -> Self {
        let kernel = nix::sys::utsname::uname()
            .map_err(|e| log::error!("Failed calling uname(): {}", e))
            .ok();

        let kernel = kernel.as_ref();

        let dmi = DmiInfo::default();
        let os = OsInfo::default();

        Self {
            // NOTE: `kernel` maps to `osType`, which is required by the
            // management API.  So, we have to provide some value even
            // in the case of failure.
            kernel: kernel
                .map(UtsName::sysname)
                .and_then(OsStr::to_str)
                .unwrap_or("UNKNOWN")
                .to_owned(),
            kernel_release: kernel
                .map(UtsName::release)
                .and_then(OsStr::to_str)
                .map(ToOwned::to_owned),
            kernel_version: kernel
                .map(UtsName::version)
                .and_then(OsStr::to_str)
                .map(ToOwned::to_owned),

            operating_system: os.id,
            operating_system_version: os.version_id,
            operating_system_variant: os.variant_id,
            operating_system_build: os.build_id,

            architecture: os.arch.to_owned(),
            cpus: u64::try_from(num_cpus::get()).expect("128-bit architectures unsupported"),
            total_memory: None,
            virtualized: match crate::virtualization::is_virtualized_env() {
                Ok(Some(true)) => "yes",
                Ok(Some(false)) => "no",
                _ => "unknown",
            }
            .to_owned(),

            product_name: dmi.product,
            system_vendor: dmi.vendor,

            version: crate::version_with_source_version(),
            server_version: None,

            provisioning: ProvisioningInfo {
                r#type: "ProvisioningType".into(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: false,
            },

            additional_properties: BTreeMap::new(),
        }
    }
}

#[derive(Clone, Debug, Default, Deserialize, Eq, PartialEq, Serialize)]
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
pub trait ModuleRuntime {
    type Config: Clone + Send + serde::Serialize;
    type Module: Module<Config = Self::Config> + Send;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config> + Send + Sync;

    async fn create(&self, module: ModuleSpec<Self::Config>) -> anyhow::Result<()>;
    async fn get(&self, id: &str) -> anyhow::Result<(Self::Module, ModuleRuntimeState)>;
    async fn start(&self, id: &str) -> anyhow::Result<()>;
    async fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> anyhow::Result<()>;
    async fn restart(&self, id: &str) -> anyhow::Result<()>;
    async fn remove(&self, id: &str) -> anyhow::Result<()>;
    async fn system_info(&self) -> anyhow::Result<SystemInfo>;
    async fn system_resources(&self) -> anyhow::Result<SystemResources>;
    async fn list(&self) -> anyhow::Result<Vec<Self::Module>>;
    async fn list_with_details(&self) -> anyhow::Result<Vec<(Self::Module, ModuleRuntimeState)>>;
    async fn list_images(&self) -> anyhow::Result<std::collections::HashMap<String, String>>;
    async fn logs(&self, id: &str, options: &LogOptions) -> anyhow::Result<hyper::Body>;
    async fn remove_all(&self) -> anyhow::Result<()>;
    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> anyhow::Result<()>;
    async fn module_top(&self, id: &str) -> anyhow::Result<Vec<i32>>;

    fn registry(&self) -> &Self::ModuleRegistry;

    fn error_code(error: &anyhow::Error) -> hyper::StatusCode;
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
            ModuleOperation::RuntimeState => write!(f, "query module runtime state"),
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
            RegistryOperation::PullImage(name) => write!(f, "pull image {:?}", name),
            RegistryOperation::RemoveImage(name) => write!(f, "remove image {:?}", name),
        }
    }
}

// Useful for error contexts
#[derive(Clone, Debug, Eq, PartialEq)]
pub enum RuntimeOperation {
    CreateModule(String),
    GetModule(String),
    GetModuleLogs(String),
    GetSupportBundle,
    Init,
    ListImages,
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
            RuntimeOperation::CreateModule(name) => write!(f, "create module {:?}", name),
            RuntimeOperation::GetModule(name) => write!(f, "get module {:?}", name),
            RuntimeOperation::GetModuleLogs(name) => {
                write!(f, "get logs for module {:?}", name)
            }
            RuntimeOperation::GetSupportBundle => write!(f, "get support bundle"),
            RuntimeOperation::Init => write!(f, "initialize module runtime"),
            RuntimeOperation::ListModules => write!(f, "list modules"),
            RuntimeOperation::ListImages => write!(f, "list images"),
            RuntimeOperation::RemoveModule(name) => write!(f, "remove module {:?}", name),
            RuntimeOperation::RestartModule(name) => write!(f, "restart module {:?}", name),
            RuntimeOperation::StartModule(name) => write!(f, "start module {:?}", name),
            RuntimeOperation::StopModule(name) => write!(f, "stop module {:?}", name),
            RuntimeOperation::SystemInfo => write!(f, "query system info"),
            RuntimeOperation::SystemResources => write!(f, "query system resources"),
            RuntimeOperation::TopModule(name) => {
                write!(f, "top module {:?}", name)
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::collections::BTreeMap;

    use edgelet_settings::module::ImagePullPolicy;

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
        for (status, expected) in inputs {
            assert_eq!(expected, status.parse().unwrap());
        }
    }

    #[test]
    fn module_config_empty_name_fails() {
        let name = String::new();
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

    #[test]
    fn system_info_merge() {
        let mut base = SystemInfo {
            kernel: "FOO".into(),
            kernel_release: Some("BAR".into()),
            kernel_version: Some("BAZ".into()),

            operating_system: "A".to_owned().into(),
            operating_system_version: "B".to_owned().into(),
            operating_system_variant: "C".to_owned().into(),
            operating_system_build: "D".to_owned().into(),

            architecture: "ARCH".into(),
            cpus: 0,
            total_memory: None,
            virtualized: "UNKNOWN".into(),

            product_name: None,
            system_vendor: None,

            version: crate::version_with_source_version(),
            server_version: None,

            provisioning: ProvisioningInfo {
                r#type: "ProvisioningType".into(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: false,
            },

            additional_properties: BTreeMap::new(),
        };

        let result = SystemInfo {
            kernel: "linux".into(),
            kernel_release: Some("5.0".into()),
            kernel_version: Some("1".into()),

            operating_system: "OS".to_owned().into(),
            operating_system_version: "B".to_owned().into(),
            operating_system_variant: "C".to_owned().into(),
            operating_system_build: "D".to_owned().into(),

            architecture: "ARCH".into(),
            cpus: 0,
            total_memory: None,
            virtualized: "UNKNOWN".into(),

            product_name: None,
            system_vendor: None,

            version: crate::version_with_source_version(),
            server_version: None,

            provisioning: ProvisioningInfo {
                r#type: "ProvisioningType".into(),
                dynamic_reprovisioning: false,
                always_reprovision_on_startup: false,
            },

            additional_properties: BTreeMap::from([
                ("foo".to_owned(), "foofoo".to_owned()),
                ("bar".to_owned(), "barbar".to_owned()),
            ]),
        };

        let additional = BTreeMap::from([
            ("kernel".to_owned(), "linux".to_owned()),
            ("kernel_release".to_owned(), "5.0".to_owned()),
            ("kernel_version".to_owned(), "1".to_owned()),
            ("operating_system".to_owned(), "OS".to_owned()),
            ("foo".to_owned(), "foofoo".to_owned()),
            ("bar".to_owned(), "barbar".to_owned()),
        ]);

        base.merge_additional(additional);

        assert_eq!(base, result);
    }
}
