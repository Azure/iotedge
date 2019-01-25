// Copyright (c) Microsoft. All rights reserved.

use super::sanitize_dns_value;
use crate::constants::*;
use crate::KubeModule;
use docker::models::ContainerCreateBody;
use edgelet_docker::DockerConfig;
use k8s_openapi::v1_10::api::core::v1 as api_core;
use log::debug;

fn get_container_by_name<'a>(
    name: &str,
    pod: &'a api_core::Pod,
) -> Option<&'a api_core::Container> {
    pod.spec
        .as_ref()
        .and_then(|pod_spec| pod_spec.containers.iter().find(|spec| spec.name == name))
}

pub fn pod_to_module(pod: &api_core::Pod) -> Option<KubeModule> {
    debug!("to docker");
    // find the original module ID in metadata
    pod.metadata
        .as_ref()
        .and_then(|meta| {
            meta.labels.as_ref().and_then(|labels| {
                labels
                    .get(EDGE_MODULE_LABEL)
                    .and_then(|label| sanitize_dns_value(label).ok())
            })
        })
        .and_then(|pod_name| {
            // now find the pod by name in the containers list and give back some information.
            debug!("Found a Pod named: {}", pod_name);
            get_container_by_name(&pod_name, pod)
                .and_then(|container| {
                    container.image.as_ref().and_then(|image_name| {
                        DockerConfig::new(image_name.to_string(), ContainerCreateBody::new(), None)
                            .ok()
                    })
                })
                .and_then(move |config| KubeModule::new(pod_name.to_string(), config).ok())
        })
}

#[cfg(test)]
mod tests {

    use super::*;
    use edgelet_core::Module;
    use k8s_openapi::v1_10::api::core::v1 as api_core;
    use serde_json;

    const POD_1: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"$edgeHub"
            }
        },
        "spec" : 
        {
            "containers" : [
                {
                    "image": "incorrect_image",
                    "name": "not_edgehub"
                },
                {
                    "image": "correct_image",
                    "name": "edgehub"
                }
            ]
        }
    }
    "###;

    #[test]
    fn pod_1_success() {
        let pod_1: api_core::Pod = serde_json::from_str(POD_1).unwrap();

        let module = pod_to_module(&pod_1).unwrap();
        assert_eq!(module.name(), "edgehub");
        assert_eq!(module.config().image(), "correct_image");
    }

    const POD_2: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub"
        }
    }
    "###;
    #[test]
    fn pod_2_no_label() {
        let pod_2: api_core::Pod = serde_json::from_str(POD_2).unwrap();
        let result = pod_to_module(&pod_2);
        assert!(result.is_none());
    }
    const POD_3: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"$edgeHub"
            }
        },
        "spec" : 
        {
            "containers" : [
                {
                    "image": "incorrect_image",
                    "name": "not_edgehub"
                }
            ]
        }
    }
    "###;
    #[test]
    fn pod_3_no_container() {
        let pod_3: api_core::Pod = serde_json::from_str(POD_3).unwrap();
        let result = pod_to_module(&pod_3);
        assert!(result.is_none());
    }
    const POD_4: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"$edgeHub"
            }
        },
        "spec" : 
        {
            "containers" : [
                {
                    "image": "",
                    "name": "edgehub"
                }
            ]
        }
    }
    "###;
    #[test]
    fn pod_4_invalid_image() {
        let pod_4: api_core::Pod = serde_json::from_str(POD_4).unwrap();
        let result = pod_to_module(&pod_4);
        assert!(result.is_none());
    }
    const POD_5: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"$$$"
            }
        },
        "spec" : 
        {
            "containers" : [
                {
                    "image": "correct_image",
                    "name": ""
                }
            ]
        }
    }
    "###;
    #[test]
    fn pod_5_invalid_pod_name() {
        let pod_5: api_core::Pod = serde_json::from_str(POD_5).unwrap();
        let result = pod_to_module(&pod_5);
        assert!(result.is_none());
    }

}
