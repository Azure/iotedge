// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use edgelet_core::{
    Certificates, Connect, Listen, ModuleSpec, Provisioning, RuntimeSettings,
    Settings as BaseSettings, WatchdogSettings,
};

use crate::config::ShellConfig;
use crate::error::Error;

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: BaseSettings<ShellConfig>,
}

impl Settings {
    pub fn new(_filename: &Path) -> Result<Self, Error> {
        unimplemented!()
    }
}

impl RuntimeSettings for Settings {
    type Config = ShellConfig;

    fn provisioning(&self) -> &Provisioning {
        self.base.provisioning()
    }

    fn agent(&self) -> &ModuleSpec<ShellConfig> {
        self.base.agent()
    }

    fn agent_mut(&mut self) -> &mut ModuleSpec<ShellConfig> {
        self.base.agent_mut()
    }

    fn hostname(&self) -> &str {
        self.base.hostname()
    }

    fn connect(&self) -> &Connect {
        self.base.connect()
    }

    fn listen(&self) -> &Listen {
        self.base.listen()
    }

    fn homedir(&self) -> &Path {
        self.base.homedir()
    }

    fn certificates(&self) -> &Certificates {
        self.base.certificates()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        self.base.watchdog()
    }
}
