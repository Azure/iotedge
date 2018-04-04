// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fmt;
use std::result::Result as StdResult;
use std::str::FromStr;

use chrono::prelude::*;
use futures::Future;
use failure::Fail;
use serde_json;

use error::Result;

#[derive(Serialize, Deserialize, Debug, PartialEq)]
#[serde(rename_all = "lowercase")]
pub enum ModuleStatus {
    Unknown,
    Created,
    Paused,
    Restarting,
    Removing,
    Dead,
    Exited,
    Running,
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

#[derive(Serialize, Deserialize, Debug, PartialEq)]
pub struct ModuleRuntimeState {
    exit_code: Option<i32>,
    status_description: Option<String>,
    started_at: Option<DateTime<Utc>>,
    finished_at: Option<DateTime<Utc>>,
    image_id: Option<String>,
}

impl Default for ModuleRuntimeState {
    fn default() -> ModuleRuntimeState {
        ModuleRuntimeState {
            exit_code: None,
            status_description: None,
            started_at: None,
            finished_at: None,
            image_id: None,
        }
    }
}

impl ModuleRuntimeState {
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
}

pub struct ModuleConfig<T> {
    name: String,
    type_: String,
    config: T,
    env: HashMap<String, String>,
}

impl<T> ModuleConfig<T> {
    pub fn new(
        name: &str,
        type_: &str,
        config: T,
        env: HashMap<String, String>,
    ) -> Result<ModuleConfig<T>> {
        Ok(ModuleConfig {
            name: ensure_not_empty!(name).to_string(),
            type_: ensure_not_empty!(type_).to_string(),
            config,
            env,
        })
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn type_(&self) -> &str {
        &self.type_
    }

    pub fn config(&self) -> &T {
        &self.config
    }

    pub fn env(&self) -> &HashMap<String, String> {
        &self.env
    }
}

pub trait Module {
    type Config;
    type Error: Fail;
    type StatusFuture: Future<Item = ModuleStatus, Error = Self::Error>;
    type RuntimeStateFuture: Future<Item = ModuleRuntimeState, Error = Self::Error>;

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn status(&self) -> Self::StatusFuture;
    fn config(&self) -> &Self::Config;
    fn runtime_state(&self) -> Self::RuntimeStateFuture;
}

pub trait ModuleRegistry {
    type Error: Fail;
    type PullFuture: Future<Item = (), Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;

    fn pull(&mut self, name: &str) -> Self::PullFuture;
    fn remove(&mut self, name: &str) -> Self::RemoveFuture;
}

pub trait ModuleRuntime {
    type Error: Fail;
    type Config;
    type Module: Module<Config = Self::Config>;
    type ModuleRegistry: ModuleRegistry;
    type CreateFuture: Future<Item = (), Error = Self::Error>;
    type UpdateFuture: Future<Item = (), Error = Self::Error>;
    type StartFuture: Future<Item = (), Error = Self::Error>;
    type StopFuture: Future<Item = (), Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;
    type ListFuture: Future<Item = Vec<Self::Module>, Error = Self::Error>;

    fn create(&mut self, module: ModuleConfig<Self::Config>) -> Self::CreateFuture;
    fn update(&mut self, module: ModuleConfig<Self::Config>) -> Self::UpdateFuture;
    fn start(&mut self, id: &str) -> Self::StartFuture;
    fn stop(&mut self, id: &str) -> Self::StopFuture;
    fn remove(&mut self, id: &str) -> Self::RemoveFuture;
    fn list(&self, label_filters: Option<&[&str]>) -> Self::ListFuture;
    fn registry_mut(&mut self) -> &mut Self::ModuleRegistry;
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
            ("created", ModuleStatus::Created),
            ("paused", ModuleStatus::Paused),
            ("restarting", ModuleStatus::Restarting),
            ("removing", ModuleStatus::Removing),
            ("dead", ModuleStatus::Dead),
            ("exited", ModuleStatus::Exited),
            ("running", ModuleStatus::Running),
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
        match ModuleConfig::new("", "docker", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils(_) => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_white_space_name_fails() {
        match ModuleConfig::new("    ", "docker", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils(_) => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_empty_type_fails() {
        match ModuleConfig::new("m1", "", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils(_) => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }

    #[test]
    fn module_config_white_space_type_fails() {
        match ModuleConfig::new("m1", "     ", 10i32, HashMap::new()) {
            Ok(_) => panic!("Expected error"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils(_) => (),
                _ => panic!("Expected utils error. Got some other error."),
            },
        }
    }
}
