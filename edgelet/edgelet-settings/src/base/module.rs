// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings<ModuleConfig> {
    name: String,

    r#type: String,

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
            r#type: self.r#type.clone(),
            config: self.config.clone(),
            env: self.env.clone(),
            image_pull_policy: self.image_pull_policy,
        }
    }
}

impl<ModuleConfig> Settings<ModuleConfig> {
    pub fn new(
        name: String,
        r#type: String,
        config: ModuleConfig,
        env: std::collections::BTreeMap<String, String>,
        image_pull_policy: ImagePullPolicy,
    ) -> Result<Self, String> {
        if name.trim().is_empty() {
            return Err("module name cannot be empty".to_string());
        }

        if r#type.trim().is_empty() {
            return Err("module type cannot be empty".to_string());
        }

        Ok(Settings {
            name,
            r#type,
            image_pull_policy,
            config,
            env,
        })
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    #[must_use]
    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn r#type(&self) -> &str {
        &self.r#type
    }

    #[must_use]
    pub fn with_type(mut self, r#type: String) -> Self {
        self.r#type = r#type;
        self
    }

    pub fn image_pull_policy(&self) -> ImagePullPolicy {
        self.image_pull_policy
    }

    #[must_use]
    pub fn with_image_pull_policy(mut self, image_pull_policy: ImagePullPolicy) -> Self {
        self.image_pull_policy = image_pull_policy;
        self
    }

    pub fn config(&self) -> &ModuleConfig {
        &self.config
    }

    pub fn config_mut(&mut self) -> &mut ModuleConfig {
        &mut self.config
    }

    #[must_use]
    pub fn with_config(mut self, config: ModuleConfig) -> Self {
        self.config = config;
        self
    }

    pub fn set_config(&mut self, config: ModuleConfig) {
        self.config = config;
    }

    pub fn env(&self) -> &std::collections::BTreeMap<String, String> {
        &self.env
    }

    pub fn env_mut(&mut self) -> &mut std::collections::BTreeMap<String, String> {
        &mut self.env
    }

    #[must_use]
    pub fn with_env(mut self, env: std::collections::BTreeMap<String, String>) -> Self {
        self.env = env;
        self
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
            _ => Err(format!("Unsupported image pull policy {}", s)),
        }
    }
}
