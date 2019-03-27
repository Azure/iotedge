// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

use std::collections::BTreeMap;

use clap::{App, Arg, ArgMatches};
use edgelet_core::crypto::{DerivedKeyStore, KeyIdentity, KeyStore, MemoryKey, MemoryKeyStore};
use edgelet_core::{Identity, IdentityManager, IdentitySpec};
use edgelet_http::client::Client as HttpClient;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
use futures::future::{self, Either, Future};
use hyper::client::HttpConnector;
use hyper::Client as HyperClient;
use hyper_tls::HttpsConnector;
use iothubservice::DeviceClient;
use kube_client::k8s_openapi::v1_10::api::core::v1 as api_core;
use kube_client::k8s_openapi::v1_10::apiextensions_apiserver::pkg::apis::apiextensions::v1beta1;
use kube_client::k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
use kube_client::{get_config, Client as KubeClient, ValueToken};
use provisioning::provisioning::{ManualProvisioning, Provision, ProvisioningResult};
use url::Url;

const IOTHUB_API_VERSION: &str = "2017-11-08-preview";
const EDGE_AGENT_MODULE_ID: &str = "$edgeAgent";

fn main() {
    env_logger::init();

    let matches = parse_args();
    let device_cs = matches.value_of("device-connection-string").unwrap();
    let kube_ns = matches.value_of("kubernetes-namespace").unwrap();
    let release_name = matches.value_of("release-name").unwrap();
    let run_mode = matches.value_of("run-mode").unwrap();

    match run_mode {
        "create" => create(device_cs, kube_ns, release_name),
        "delete" => delete(kube_ns, release_name),
        _ => panic!("Unrecognized run mode supplied: {}", run_mode),
    }
}

fn create(device_cs: &str, kube_ns: &str, release_name: &str) {
    let mut runtime = tokio::runtime::Runtime::new().unwrap();
    let (id_man, prov_result) = get_identity_manager(device_cs.to_string());

    let kube_config = get_config().unwrap();
    let kube_client = KubeClient::new(kube_config);

    // create the crd
    runtime.block_on(create_crd(kube_client.clone())).unwrap();
    println!("Created CRD.");

    // create a config map with the edge agent's generation ID
    let ea_config_map = runtime
        .block_on(create_ea_config_map(
            kube_ns.to_string(),
            release_name.to_string(),
            id_man,
            prov_result,
            kube_client,
        ))
        .unwrap();
    println!("{}", serde_yaml::to_string(&ea_config_map).unwrap());
}

fn create_crd(kube_client: KubeClient<ValueToken>) -> impl Future<Item = (), Error = String> {
    // check if this CRD already exists and create if it doesn't
    let kube_client_copy = kube_client.clone();
    kube_client
        .read_crd("edgedeployments.microsoft.azure.devices.edge")
        .map_err(|err| format!("{:?}", err))
        .and_then(move |crd| {
            crd.map(|_| Either::A(future::ok(()))).unwrap_or_else(|| {
                let crd = v1beta1::CustomResourceDefinition {
                    api_version: Some("apiextensions.k8s.io/v1beta1".to_string()),
                    kind: Some("CustomResourceDefinition".to_string()),
                    metadata: Some(api_meta::ObjectMeta {
                        name: Some("edgedeployments.microsoft.azure.devices.edge".to_string()),
                        ..Default::default()
                    }),
                    spec: Some(v1beta1::CustomResourceDefinitionSpec {
                        group: "microsoft.azure.devices.edge".to_string(),
                        names: v1beta1::CustomResourceDefinitionNames {
                            kind: "EdgeDeployment".to_string(),
                            list_kind: Some("EdgeDeploymentList".to_string()),
                            plural: "edgedeployments".to_string(),
                            singular: Some("edgedeployment".to_string()),
                            ..Default::default()
                        },
                        scope: "Namespaced".to_string(),
                        version: "v1beta1".to_string(),
                        ..Default::default()
                    }),
                    ..Default::default()
                };

                Either::B(
                    kube_client_copy
                        .create_crd(crd)
                        .map(|_| ())
                        .map_err(|err| format!("{:?}", err)),
                )
            })
        })
}

fn create_ea_config_map(
    kube_ns: String,
    release_name: String,
    mut id_man: HubIdentityManager<
        DerivedKeyStore<MemoryKey>,
        HyperClient<HttpsConnector<HttpConnector>>,
        MemoryKey,
    >,
    prov_result: ProvisioningResult,
    kube_client: KubeClient<ValueToken>,
) -> impl Future<Item = api_core::ConfigMap, Error = String> {
    // get the current edge agent identity to fetch the generation id
    id_man
        .get(IdentitySpec::new(EDGE_AGENT_MODULE_ID.to_string()))
        .map_err(|err| format!("{:?}", err))
        .and_then(move |edge_agent| {
            let edge_agent = edge_agent.unwrap();

            // update the credentials
            id_man
                .update(
                    IdentitySpec::new(EDGE_AGENT_MODULE_ID.to_string())
                        .with_generation_id(edge_agent.generation_id().to_string()),
                )
                .map(|_| edge_agent)
                .map_err(|err| format!("{:?}", err))
        })
        .and_then(move |edge_agent| {
            let mut data = BTreeMap::new();
            data.insert(
                "generationId".to_string(),
                edge_agent.generation_id().to_string(),
            );
            data.insert("deviceId".to_string(), prov_result.device_id().to_string());
            data.insert(
                "iotHubHostName".to_string(),
                prov_result.hub_name().to_string(),
            );

            let mut meta: api_meta::ObjectMeta = Default::default();
            meta.name = Some(format!("{}-ea-config", release_name));

            let config_map = api_core::ConfigMap {
                api_version: Some("v1".to_string()),
                binary_data: None,
                data: Some(data),
                kind: Some("ConfigMap".to_string()),
                metadata: Some(meta),
            };

            kube_client
                .create_config_map(&kube_ns, &config_map)
                .map_err(|err| format!("{:?}", err))
        })
}

fn delete(kube_ns: &str, release_name: &str) {
    let mut runtime = tokio::runtime::Runtime::new().unwrap();
    let kube_config = get_config().unwrap();
    let kube_client = KubeClient::new(kube_config);
    let config_map_name = format!("{}-ea-config", release_name);

    // delete EA config map
    runtime
        .block_on(kube_client.delete_config_map(&kube_ns, &config_map_name))
        .unwrap();
    println!("Config map {} deleted.", config_map_name);
}

fn get_identity_manager(
    device_cs: String,
) -> (
    HubIdentityManager<
        DerivedKeyStore<MemoryKey>,
        HyperClient<HttpsConnector<HttpConnector>>,
        MemoryKey,
    >,
    ProvisioningResult,
) {
    let prov = ManualProvisioning::new(&device_cs).unwrap();
    let hsm = MemoryKeyStore::new();
    let prov_result = prov.provision(hsm.clone()).wait().unwrap();
    let key = hsm.get(&KeyIdentity::Device, "primary").unwrap();
    let key_store = DerivedKeyStore::new(key.clone());

    let token_source = SasTokenSource::new(
        prov_result.hub_name().to_string(),
        prov_result.device_id().to_string(),
        key,
    );
    let hyper_client = HyperClient::builder().build(HttpsConnector::new(4).unwrap());
    let http_client = HttpClient::new(
        hyper_client,
        Some(token_source),
        IOTHUB_API_VERSION.to_string(),
        Url::parse(&format!("https://{}", prov_result.hub_name())).unwrap(),
    )
    .unwrap();
    let device_client =
        DeviceClient::new(http_client, prov_result.device_id().to_string()).unwrap();

    (
        HubIdentityManager::new(key_store, device_client),
        prov_result,
    )
}

fn parse_args<'a>() -> ArgMatches<'a> {
    App::new("update-identity-hook")
        .version(env!("CARGO_PKG_VERSION"))
        .author(env!("CARGO_PKG_AUTHORS"))
        .arg(
            Arg::with_name("run-mode")
                .short("m")
                .long("run-mode")
                .value_name("RUN_MODE")
                .help("Run mode")
                .possible_values(&["create", "delete"])
                .required(true)
                .takes_value(true),
        )
        .arg(
            Arg::with_name("device-connection-string")
                .short("d")
                .long("device-connection-string")
                .value_name("DEVICE_CONNECTION_STRING")
                .help("IoT Hub device connection string")
                .required(true)
                .takes_value(true),
        )
        .arg(
            Arg::with_name("kubernetes-namespace")
                .short("n")
                .long("kubernetes-namespace")
                .value_name("KUBERNETES_NAMESPACE")
                .help("Kubernetes namespace in which to create the config map")
                .required(true)
                .takes_value(true),
        )
        .arg(
            Arg::with_name("release-name")
                .short("r")
                .long("release-name")
                .value_name("HELM_RELEASE_NAME")
                .help("Helm release name to use as a base name for the config map")
                .required(true)
                .takes_value(true),
        )
        .get_matches()
}
