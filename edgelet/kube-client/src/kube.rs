// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;

use serde_derive::{Deserialize, Serialize};

pub trait NameValue {
    type Item;

    fn name(&self) -> &str;
    fn value(&self) -> &Self::Item;
}

pub trait Lookup<T> {
    fn get(&self, name: &str) -> Option<&T>;
}

impl<K> Lookup<K::Item> for Vec<K>
where
    K: NameValue,
{
    fn get(&self, name: &str) -> Option<&K::Item> {
        self.iter().find(|v| v.name() == name).map(K::value)
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct Config {
    kind: Option<String>,
    #[serde(rename = "apiVersion")]
    api_version: Option<String>,
    clusters: Vec<ClusterEntry>,
    users: Vec<AuthInfoEntry>,
    contexts: Vec<ContextEntry>,
    current_context: String,
}

impl Config {
    pub fn kind(&self) -> Option<&str> {
        self.kind.as_ref().map(String::as_str)
    }

    pub fn with_kind(mut self, kind: Option<String>) -> Self {
        self.kind = kind;
        self
    }

    pub fn api_version(&self) -> Option<&str> {
        self.api_version.as_ref().map(String::as_str)
    }

    pub fn with_api_version(mut self, api_version: String) -> Self {
        self.api_version = Some(api_version);
        self
    }

    pub fn clusters(&self) -> &Vec<ClusterEntry> {
        &self.clusters
    }

    pub fn with_clusters(mut self, clusters: Vec<ClusterEntry>) -> Self {
        self.clusters = clusters;
        self
    }

    pub fn users(&self) -> &Vec<AuthInfoEntry> {
        &self.users
    }

    pub fn with_users(mut self, users: Vec<AuthInfoEntry>) -> Self {
        self.users = users;
        self
    }

    pub fn contexts(&self) -> &Vec<ContextEntry> {
        &self.contexts
    }

    pub fn with_contexts(mut self, contexts: Vec<ContextEntry>) -> Self {
        self.contexts = contexts;
        self
    }

    pub fn current_context(&self) -> &str {
        &self.current_context
    }

    pub fn with_current_context(mut self, current_context: String) -> Self {
        self.current_context = current_context;
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct ClusterEntry {
    name: String,
    cluster: Cluster,
}

impl NameValue for ClusterEntry {
    type Item = Cluster;

    fn name(&self) -> &str {
        &self.name
    }

    fn value(&self) -> &Self::Item {
        self.cluster()
    }
}

impl ClusterEntry {
    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn cluster(&self) -> &Cluster {
        &self.cluster
    }

    pub fn with_cluster(mut self, cluster: Cluster) -> Self {
        self.cluster = cluster;
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct Cluster {
    server: String,
    insecure_skip_tls_verify: Option<bool>,
    certificate_authority: Option<String>,
    certificate_authority_data: Option<String>,
}

impl Cluster {
    pub fn server(&self) -> &str {
        &self.server
    }

    pub fn with_server(mut self, server: String) -> Self {
        self.server = server;
        self
    }

    pub fn insecure_skip_tls_verify(&self) -> Option<bool> {
        self.insecure_skip_tls_verify
    }

    pub fn with_insecure_skip_tls_verify(mut self, insecure_skip_tls_verify: bool) -> Self {
        self.insecure_skip_tls_verify = Some(insecure_skip_tls_verify);
        self
    }

    pub fn certificate_authority(&self) -> Option<&str> {
        self.certificate_authority.as_ref().map(String::as_str)
    }

    pub fn with_certificate_authority(mut self, certificate_authority: String) -> Self {
        self.certificate_authority = Some(certificate_authority);
        self
    }

    pub fn certificate_authority_data(&self) -> Option<&str> {
        self.certificate_authority_data.as_ref().map(String::as_str)
    }

    pub fn with_certificate_authority_data(mut self, certificate_authority_data: String) -> Self {
        self.certificate_authority_data = Some(certificate_authority_data);
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct AuthInfoEntry {
    name: String,
    user: AuthInfo,
}

impl NameValue for AuthInfoEntry {
    type Item = AuthInfo;

    fn name(&self) -> &str {
        &self.name
    }

    fn value(&self) -> &Self::Item {
        self.user()
    }
}

impl AuthInfoEntry {
    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn user(&self) -> &AuthInfo {
        &self.user
    }

    pub fn with_user(mut self, user: AuthInfo) -> Self {
        self.user = user;
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct AuthInfo {
    client_certificate: Option<String>,
    client_certificate_data: Option<String>,
    client_key: Option<String>,
    client_key_data: Option<String>,
    token: Option<String>,
    token_file: Option<String>,
    impersonate: Option<String>,
    impersonate_groups: Option<Vec<String>>,
    username: Option<String>,
    password: Option<String>,
    auth_provider: Option<AuthProviderConfig>,
    exec: Option<ExecConfig>,
}

impl AuthInfo {
    pub fn client_certificate(&self) -> Option<&str> {
        self.client_certificate.as_ref().map(String::as_str)
    }

    pub fn with_client_certificate(mut self, client_certificate: String) -> Self {
        self.client_certificate = Some(client_certificate);
        self
    }

    pub fn client_key(&self) -> Option<&str> {
        self.client_key.as_ref().map(String::as_str)
    }

    pub fn with_client_key(mut self, client_key: String) -> Self {
        self.client_key = Some(client_key);
        self
    }

    pub fn token(&self) -> Option<&str> {
        self.token.as_ref().map(String::as_str)
    }

    pub fn with_token(mut self, token: String) -> Self {
        self.token = Some(token);
        self
    }

    pub fn token_file(&self) -> Option<&str> {
        self.token_file.as_ref().map(String::as_str)
    }

    pub fn with_token_file(mut self, token_file: String) -> Self {
        self.token_file = Some(token_file);
        self
    }

    pub fn impersonate(&self) -> Option<&str> {
        self.impersonate.as_ref().map(String::as_str)
    }

    pub fn with_impersonate(mut self, impersonate: String) -> Self {
        self.impersonate = Some(impersonate);
        self
    }

    pub fn username(&self) -> Option<&str> {
        self.username.as_ref().map(String::as_str)
    }

    pub fn with_username(mut self, username: String) -> Self {
        self.username = Some(username);
        self
    }

    pub fn password(&self) -> Option<&str> {
        self.password.as_ref().map(String::as_str)
    }

    pub fn with_password(mut self, password: String) -> Self {
        self.password = Some(password);
        self
    }

    pub fn client_certificate_data(&self) -> Option<&str> {
        self.client_certificate_data.as_ref().map(String::as_str)
    }

    pub fn with_client_certificate_data(mut self, client_certificate_data: String) -> Self {
        self.client_certificate_data = Some(client_certificate_data);
        self
    }

    pub fn client_key_data(&self) -> Option<&str> {
        self.client_key_data.as_ref().map(String::as_str)
    }

    pub fn with_client_key_data(mut self, client_key_data: String) -> Self {
        self.client_key_data = Some(client_key_data);
        self
    }

    pub fn impersonate_groups(&self) -> Option<&Vec<String>> {
        self.impersonate_groups.as_ref()
    }

    pub fn with_impersonate_groups(mut self, impersonate_groups: Vec<String>) -> Self {
        self.impersonate_groups = Some(impersonate_groups);
        self
    }

    pub fn exec(&self) -> Option<&ExecConfig> {
        self.exec.as_ref()
    }

    pub fn with_exec(mut self, exec: ExecConfig) -> Self {
        self.exec = Some(exec);
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct AuthProviderConfig {
    name: String,
    config: Option<BTreeMap<String, String>>,
}

impl AuthProviderConfig {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn config(&self) -> Option<&BTreeMap<String, String>> {
        self.config.as_ref()
    }

    pub fn with_config(mut self, config: BTreeMap<String, String>) -> Self {
        self.config = Some(config);
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct ExecConfig {
    command: String,
    args: Vec<String>,
    env: Vec<ExecEnvVar>,
    api_version: Option<String>,
}

impl ExecConfig {
    pub fn command(&self) -> &str {
        &self.command
    }

    pub fn with_command(mut self, command: String) -> Self {
        self.command = command;
        self
    }

    pub fn args(&self) -> &Vec<String> {
        &self.args
    }

    pub fn with_args(mut self, args: Vec<String>) -> Self {
        self.args = args;
        self
    }

    pub fn env(&self) -> &Vec<ExecEnvVar> {
        &self.env
    }

    pub fn with_env(mut self, env: Vec<ExecEnvVar>) -> Self {
        self.env = env;
        self
    }

    pub fn api_version(&self) -> Option<&str> {
        self.api_version.as_ref().map(String::as_str)
    }

    pub fn with_api_version(mut self, api_version: String) -> Self {
        self.api_version = Some(api_version);
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct ExecEnvVar {
    name: String,
    value: String,
}

impl ExecEnvVar {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn value(&self) -> &str {
        &self.value
    }

    pub fn with_value(mut self, value: String) -> Self {
        self.value = value;
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct ContextEntry {
    name: String,
    context: Context,
}

impl NameValue for ContextEntry {
    type Item = Context;

    fn name(&self) -> &str {
        &self.name
    }

    fn value(&self) -> &Self::Item {
        self.context()
    }
}

impl ContextEntry {
    pub fn with_name(mut self, name: String) -> Self {
        self.name = name;
        self
    }

    pub fn context(&self) -> &Context {
        &self.context
    }

    pub fn with_context(mut self, context: Context) -> Self {
        self.context = context;
        self
    }
}

#[derive(Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub struct Context {
    cluster: String,
    user: String,
    namespace: Option<String>,
}

impl Context {
    pub fn cluster(&self) -> &str {
        &self.cluster
    }

    pub fn with_cluster(mut self, cluster: String) -> Self {
        self.cluster = cluster;
        self
    }

    pub fn user(&self) -> &str {
        &self.user
    }

    pub fn with_user(mut self, user: String) -> Self {
        self.user = user;
        self
    }

    pub fn namespace(&self) -> Option<&str> {
        self.namespace.as_ref().map(String::as_str)
    }

    pub fn with_namespace(mut self, namespace: String) -> Self {
        self.namespace = Some(namespace);
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_yaml;

    struct TestNameValue {
        name: String,
        value: i32,
    }

    impl TestNameValue {
        pub fn new(name: String, value: i32) -> TestNameValue {
            TestNameValue { name, value }
        }
    }

    impl NameValue for TestNameValue {
        type Item = i32;

        fn name(&self) -> &str {
            &self.name
        }
        fn value(&self) -> &Self::Item {
            &self.value
        }
    }

    const EMPTY_CONFIG_YAML: &str = r###"---
kind: ~
apiVersion: ~
clusters: []
users: []
contexts: []
current-context: ~
"###;

    const GOOD_CONFIG_YAML: &str = r###"---
kind: Config
apiVersion: v1
clusters:
- cluster:
    certificate-authority: /path/ca.crt
    server: https://server1.contoso.com:443
  name: server1
- cluster:
    certificate-authority-data: Y2VydGlmaWNhdGUhCg==
    server: https://server2.contoso.com:8443
  name: server2
users:
- name: server1user
  user:
    client-certificate: /path/server1user.crt
    client-key: /path/server1user.key
- name: server2user
  user:
    client-certificate-data: Y2xpZW50LWNlcnRpZmljYXRlLWRhdGE=
    client-key-data: Y2xpZW50LWtleS1kYXRh
contexts:
- context:
    cluster: server1
    user: server1user
  name: server1context
- context:
    cluster: server2
    user: server2user
  name: server2context
current-context: server1context
"###;

    const GOOD_CLUSTER1: &str = r###"---
cluster:
  certificate-authority: /path/ca.crt
  server: https://server1.contoso.com:443
name: server1
"###;
    const GOOD_CLUSTER2: &str = r###"---
cluster:
  certificate-authority-data: Y2VydGlmaWNhdGUhCg==
  server: https://server2.contoso.com:8443
name: server2
"###;
    const GOOD_USER1: &str = r###"---
name: server1user
user:
  client-certificate: /path/server1user.crt
  client-key: /path/server1user.key
"###;
    const GOOD_USER2: &str = r###"---
name: server2user
user:
  client-certificate-data: Y2xpZW50LWNlcnRpZmljYXRlLWRhdGE=
  client-key-data: Y2xpZW50LWtleS1kYXRh
"###;
    const GOOD_CONTEXT1: &str = r###"---
context:
  cluster: server1
  user: server1user
name: server1context
"###;
    const GOOD_CONTEXT2: &str = r###"---
context:
  cluster: server2
  user: server2user
name: server2context
"###;

    #[test]
    fn test_lookup() {
        let test_vec = vec![
            TestNameValue::new("124".to_string(), 124),
            TestNameValue::new("45".to_string(), 45),
            TestNameValue::new("45266".to_string(), 45266),
            TestNameValue::new("79".to_string(), 79),
        ];

        assert_eq!(test_vec.get("124"), Some(&124));
        assert_eq!(test_vec.get("45"), Some(&45));
        assert_eq!(test_vec.get("45266"), Some(&45266));
        assert_eq!(test_vec.get("79"), Some(&79));
        assert_eq!(test_vec.get("not in vector"), None);
    }

    #[test]
    fn test_config_construction() {
        let cluster1: ClusterEntry = serde_yaml::from_str(GOOD_CLUSTER1).unwrap();
        let cluster2: ClusterEntry = serde_yaml::from_str(GOOD_CLUSTER2).unwrap();
        let clusters = vec![cluster1, cluster2];
        let user1: AuthInfoEntry = serde_yaml::from_str(GOOD_USER1).unwrap();
        let user2: AuthInfoEntry = serde_yaml::from_str(GOOD_USER2).unwrap();
        let users = vec![user1, user2];
        let context1: ContextEntry = serde_yaml::from_str(GOOD_CONTEXT1).unwrap();
        let context2: ContextEntry = serde_yaml::from_str(GOOD_CONTEXT2).unwrap();
        let contexts = vec![context1, context2];
        let c1: Config = serde_yaml::from_str(EMPTY_CONFIG_YAML).unwrap();
        let c1 = c1
            .with_kind(Some("Config".to_string()))
            .with_api_version("v1".to_string())
            .with_clusters(clusters)
            .with_users(users)
            .with_contexts(contexts)
            .with_current_context("server1context".to_string());
        let c2: Config = serde_yaml::from_str(GOOD_CONFIG_YAML).unwrap();
        assert_eq!(c1, c2);
    }
}
