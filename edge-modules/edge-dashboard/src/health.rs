// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug)]
pub enum Health {
    Healthy,
    Degraded,
    Poor,
}

#[derive(Debug)]
pub struct Status {
    iotedged: bool,
    edge_agent: bool,
    edge_hub: bool,
    other_modules: bool,
}

impl Status {
    pub fn new() -> Self {
        Status {
            iotedged: false,
            edge_agent: false,
            edge_hub: false,
            other_modules: false,
        }
    }

    pub fn _set_status(
        iotedged: bool,
        edge_agent: bool,
        edge_hub: bool,
        other_modules: bool,
    ) -> Self {
        Status {
            iotedged,
            edge_agent,
            edge_hub,
            other_modules,
        }
    }

    pub fn set_iotedged(&mut self) {
        self.iotedged = true;
    }

    pub fn set_edge_agent(&mut self, val: bool) {
        self.edge_agent = val;
    }

    pub fn set_edge_hub(&mut self, val: bool) {
        self.edge_hub = val;
    }

    pub fn set_other_modules(&mut self, val: bool) {
        self.other_modules = val;
    }

    pub fn return_health(&self) -> Health {
        let good = self.iotedged && self.edge_agent && self.edge_hub;
        let healthy = good && self.other_modules;
        let degraded = good && !self.other_modules;

        if healthy {
            Health::Healthy
        } else if degraded {
            Health::Degraded
        } else {
            Health::Poor
        }
    }
}
