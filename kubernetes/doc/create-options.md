# Kubernetes createOptions Extensions

IoT Edge on Kubernetes supports several experimental features to allow a user to assign additional parameters to pods that represents IoT Edge Modules.

## Experimental features

All features described in this document are experimental. To enable them, the following environment variable need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                  | value  |
|--------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`            | `true` |

## Create Options

We added CreateOptions for experimental features on Kubernetes. These options will be ignores until both `ExperimentalFeatures__Enabled` and `ExperimentalFeatures__Enabled{FeatureName}` are not set to `true`.

```json
{
  "k8s-experimental": {
    "volumes": [{...}],
    "resources": [{...}],
    "nodeSelector": {...}
  }
}
```

## Volumes

EdgeAgent allows to mount existing volumes in the namespace from different sources e.g. `ConfigMap` as a pod `Volume`.

### Enabling this feature
To enable this feature, the following environment variables need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                | value  |
|------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`          | `true` |
| `ExperimentalFeatures__K8SEnableVolumes` | `true` |

### Create Options

A `volumes` section of config used to describe how a `ConfigMap` existing in a namespace will be mounted into module container. The value of this config section is an array of pairs `volume` and `volumeMount`. A `volume` part describes how a specified volume source is mounted to `pod` and `volumeMount` describes a mounting of a `volume` within a container. 

`EdgeAgent` doesn't do any translations or interpretations of values but simply assign values to the `pod` and `container` specs. The description of exact structure can be found here for [Volume](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#volume-v1-core) and for [VolumeMount](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#volumemount-v1-core).

```json
{
  ...
  "k8s-experimental": {
    "volumes": [
      {
        "volume": {
          "name": "ModuleA",
          "configMap": {
            "optional": "true",
            "defaultMode": 420,
            "items": [{
                "key": "config-file",
                "path": "config.yaml",
                "mode": 420
            }],
            "name": "module-config"
          }
        },
        "volumeMount": {
          "name": "module-config",
          "mountPath": "/etc/module/config.yaml",
          "mountPropagation": "None",
          "readOnly": "true",
          "subPath": "" 
        }
      }
    ]
  }
}
```

## CPU and Memory Resources

EdgeAgent allows to specify Compute resources for a module container.

### Enabling this feature
To enable this feature, the following environment variables need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                   | value  |
|---------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`             | `true` |
| `ExperimentalFeatures__K8SEnableResources`  | `true` |

### Create Options

A `resources` section of config used to specify compute resources for an IoT Edge Module container. The value of this section is the same description Kubernetes uses for [container spec](https://kubernetes.io/docs/concepts/configuration/manage-compute-resources-container/). 

`EdgeAgent` doesn't do any translations or interpretations of values but simply assign value from module deployment to `resources` parameter of container spec. The description of exact structure can be found [here](hhttps://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#resourcerequirements-v1-core).

```json
{
  ...
  "k8s-experimental": {
    "resources": {
      "limits": {
        "memory": "128Mi",
        "cpu": "500m"
      },
      "requests": {
        "memory": "64Mi",
        "cpu": "250m"
      }
    }
  }
}
```

## Assigning Modules to Nodes

EdgeAgent allows to constrain an IoT Edge Module to only be able to run on particular node(s), or to prefer to run on particular nodes.

### Enabling this feature
To enable this feature, the following environment variables need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                | value  |
|------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`          | `true` |
| `ExperimentalFeatures__K8SNodeSelector`  | `true` |

### CreateOptions

A `nodeSelector` section of config used for node selection constrain. It specifies a map of key-value pairs that corresponds to a key-value pair of node labels. To know more please refer to a [corresponding docs](https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#nodeselector).

`EdgeAgent` doesn't do any translations or interpretations of values but simply assign value from module deployment to `nodeSelector` parameter of a pod spec.

```json
{
  ...
  "k8s-experimental": {
    "nodeSelector": {
      "disktype": "ssd",
      "gpu": "true"
    }
  }
}
```


 