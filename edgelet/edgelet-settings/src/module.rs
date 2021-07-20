// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings<ModuleConfig> {
    name: String,

    #[serde(rename = "type")]
    type_: String,

    #[serde(default, rename = "imagePullPolicy")]
    image_pull_policy: ImagePullPolicy,

    config: ModuleConfig,

    #[serde(default)]
    env: std::collections::BTreeMap<String, String>,
}

impl<T> Clone for Settings<T>
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

#[derive(Clone, Copy, Debug, PartialEq, serde::Deserialize, serde::Serialize)]
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

impl std::str::FromStr for ImagePullPolicy {
    type Err = String;

    fn from_str(s: &str) -> Result<ImagePullPolicy, Self::Err> {
        match s.to_lowercase().as_str() {
            "on-create" => Ok(ImagePullPolicy::OnCreate),
            "never" => Ok(ImagePullPolicy::Never),
            _ => Err(format!("Unsupported image pull policy {}", s.to_string())),
        }
    }
}
