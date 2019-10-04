// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use k8s_openapi::api::core::v1 as api_core;
use log::debug;

use docker::models::ContainerCreateBody;
use edgelet_docker::DockerConfig;

use crate::constants::*;
use crate::error::{Error, ErrorKind, Result};
use crate::KubeModule;

fn get_container_by_name<'a>(
    name: &str,
    pod: &'a api_core::Pod,
) -> Option<&'a api_core::Container> {
    pod.spec
        .as_ref()
        .and_then(|pod_spec| pod_spec.containers.iter().find(|spec| spec.name == name))
}

pub fn pod_to_module(pod: &api_core::Pod) -> Option<Result<KubeModule>> {
    // find the module label and original module ID in metadata
    // If we don't find these labels, this is not a pod created/managed by IoT Edge, return None
    pod.metadata
        .as_ref()
        .and_then(|meta| {
            meta.labels
                .as_ref()
                .and_then(|labels| labels.get(EDGE_MODULE_LABEL))
                .and_then(|module| {
                    meta.annotations.as_ref().and_then(|annotations| {
                        annotations
                            .get(EDGE_ORIGINAL_MODULEID)
                            .and_then(|module_id| Some((module, module_id)))
                    })
                })
        })
        .map(|(module, module_id)| {
            // now find the pod by name in the containers list and give back some information.
            // If the information is not found or incorrect, return, Some(Err())
            debug!("Found the module named: {}, id {}", module, module_id);
            get_container_by_name(&module, pod)
                .map_or_else(
                    || Err(ErrorKind::ModuleNotFound(module.to_string()).into()),
                    |container| {
                        container.image.as_ref().map_or_else(
                            || Err(ErrorKind::ImageNotFound.into()),
                            |image_name| {
                                DockerConfig::new(
                                    image_name.to_string(),
                                    ContainerCreateBody::new(),
                                    None,
                                )
                                .map_err(|err| Error::from(err.context(ErrorKind::PodToModule)))
                            },
                        )
                    },
                )
                .map_err(|err| Error::from(err.context(ErrorKind::PodToModule)))
                .and_then(|config| KubeModule::new(module_id.to_string(), config))
        })
}

#[cfg(test)]
mod tests {

    use super::*;
    use edgelet_core::Module;
    use k8s_openapi::api::core::v1 as api_core;
    use serde_json;

    const POD_SUCCESS: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"edgehub"
            },
            "annotations": {
                "net.azure-devices.edge.original-moduleid" : "$edgeHub"
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
    fn pod_success() {
        let pod_1: api_core::Pod = serde_json::from_str(POD_SUCCESS).unwrap();

        let module = pod_to_module(&pod_1).unwrap().unwrap();
        assert_eq!(module.name(), "$edgeHub");
        assert_eq!(module.config().image(), "correct_image");
    }

    const POD_NO_ANNOTATION: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"edgehub"
            }
        }
    }
    "###;

    #[test]
    fn pod_no_annotation() {
        let pod_2: api_core::Pod = serde_json::from_str(POD_NO_ANNOTATION).unwrap();
        let result = pod_to_module(&pod_2);
        assert!(result.is_none());
    }

    const POD_NO_LABEL: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub"
        }
    }
    "###;
    #[test]
    fn pod_no_label() {
        let pod_2: api_core::Pod = serde_json::from_str(POD_NO_LABEL).unwrap();
        let result = pod_to_module(&pod_2);
        assert!(result.is_none());
    }

    const POD_NO_CONTAINER: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"edgehub"
            },
            "annotations": {
                "net.azure-devices.edge.original-moduleid" : "$edgeHub"
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
    fn pod_no_container() {
        let pod_3: api_core::Pod = serde_json::from_str(POD_NO_CONTAINER).unwrap();
        let result = pod_to_module(&pod_3);
        assert!(result.is_some());
        assert!(result.unwrap().is_err());
    }

    const POD_INVALID_IMAGE: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"edgehub"
            },
            "annotations": {
                "net.azure-devices.edge.original-moduleid" : "$edgeHub"
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
    fn pod_invalid_image() {
        let pod_4: api_core::Pod = serde_json::from_str(POD_INVALID_IMAGE).unwrap();
        let result = pod_to_module(&pod_4);
        assert!(result.is_some());
        assert!(result.unwrap().is_err());
    }

    const POD_INVALID_NAME: &str = r###"
    {
        "kind": "Pod",
        "metadata" : 
        {
            "name" : "edgehub",
            "labels" : {
                "net.azure-devices.edge.module":"$$$"
            },
            "annotations": {
                "net.azure-devices.edge.original-moduleid" : "$edgeHub"
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
    fn pod_invalid_pod_name() {
        let pod_5: api_core::Pod = serde_json::from_str(POD_INVALID_NAME).unwrap();
        let result = pod_to_module(&pod_5);
        assert!(result.is_some());
        assert!(result.unwrap().is_err());
    }
}
