// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub enum ProvisioningMethod {
    None,
    Manual,
    Dps,
}

impl fmt::Display for ProvisioningMethod {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        let s = match *self {
            ProvisioningMethod::None => "None",
            ProvisioningMethod::Manual => "Manual",
            ProvisioningMethod::Dps => "DPS",
        };
        write!(f, "{}", s)
    }
}

pub trait ProvisioningInfo {
    fn iot_hub_name(&self) -> &str;
    fn device_id(&self) -> &str;
    fn method(&self) -> &ProvisioningMethod;
}
