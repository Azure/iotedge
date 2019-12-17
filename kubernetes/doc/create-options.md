# Kubernetes createOptions Extensions

IoT Edge on Kubernetes supports several experimental features to allow a user to assign additional parameters to containers that represents IoT Edge Modules.

## Experimental features

All features described in this document are experimental. To enable them, the following environment variable need to be set for the edgeAgent (make note of the double underscores):

| Environment Variable Name                   | value  |
|---------------------------------------------|--------|
| `ExperimentalFeatures__Enabled`             | `true` |
| `ExperimentalFeatures__EnableK8SExtensions` | `true` |

## Create Options

We added CreateOptions for experimental features on Kubernetes. These options "options will take effect if both variables `ExperimentalFeatures__Enabled` and `ExperimentalFeatures__EnableK8SExtensions` are set to `true`.

```json
{
  "k8s-experimental": {
    "volumes": [{...}],
    "resources": [{...}],
    "nodeSelector": {...}
    "securityContext": {...},
  }
}
```

## Volumes

EdgeAgent allows to mount existing volumes in the namespace from different sources e.g. `ConfigMap` as a pod `Volume`.

### Create Options

A `volumes` section of config used to describe how a `ConfigMap` or any other volume source existing in a namespace will be mounted into module container. The value of this config section is an array of pairs `volume` and `volumeMount`. A `volume` part describes how a specified volume source is mounted to `pod` and `volumeMounts` describes a mounting of a `volume` within a module main container. See [Volumes](https://kubernetes.io/docs/concepts/storage/volumes/) and [PodSpec](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#podspec-v1-core) reference for more information.

`EdgeAgent` doesn't do any translations or interpretations of values but simply assigns values to the `pod` and `container` specs. The description of exact structure can be found here for [Volume](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#volume-v1-core) and for [VolumeMount](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#volumemount-v1-core).

```json
{
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
        "volumeMounts": [
          {
            "name": "module-config",
            "mountPath": "/etc/module/config.yaml",
            "mountPropagation": "None",
            "readOnly": "true",
            "subPath": "" 
          }
        ]
      }
    ]
  }
}
```

## CPU, Memory and Device Resources

EdgeAgent allows to specify Compute resources for a module container. Edge supports HostConfig CPU and Memory limits. They will be overrriten if a user specifis `resources` section described below.

### Create Options

A `resources` section of config used to specify compute resources for an IoT Edge Module container. The value of this section is the same description Kubernetes uses for [container spec](https://kubernetes.io/docs/concepts/configuration/manage-compute-resources-container/). In addition to CPU and Memory EdgeAgent operator can specify [device](https://kubernetes.io/docs/concepts/extend-kubernetes/compute-storage-net/device-plugins/) limits for a module.

`EdgeAgent` doesn't do any translations or interpretations of values but simply assigns value from module deployment to `resources` parameter of container spec. The description of exact structure can be found [here](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#resourcerequirements-v1-core).

```json
{
  "k8s-experimental": {
    "resources": {
      "limits": {
        "memory": "128Mi",
        "cpu": "500m",
        "hardware-vendor.example/foo": 2
      },
      "requests": {
        "memory": "64Mi",
        "cpu": "250m",
        "hardware-vendor.example/foo": 2
      }
    }
  }
}
```

## Assigning Modules to Nodes

EdgeAgent allows to constrain an IoT Edge Module to only be able to run on particular node(s).

### CreateOptions

A `nodeSelector` section of config used for node selection constrain. It specifies a map of key-value pairs that corresponds to a key-value pair of node labels. To know more please refer to a [corresponding docs](https://kubernetes.io/docs/concepts/configuration/assign-pod-node/#nodeselector).

`EdgeAgent` doesn't do any translations or interpretations of values but simply assigns value from module deployment to `nodeSelector` parameter of a pod spec.

```json
{
  "k8s-experimental": {
    "nodeSelector": {
      "disktype": "ssd",
      "gpu": "true"
    }
  }
}
```

## Apply Pod Security Context

By default EdgeAgent doesn't make any assumptions about pod security context and allows to run containers under the user this container was built. With this section user can specify exact pod security policy module will run with.

### CreateOptions

A `securityContext` section of config used to apply a pod security context to a module pod spec. The section has the same structure as a Kubernetes [Pod spec](https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.12/#podsecuritycontext-v1-core).
 
`EdgeAgent` doesn't do any translations or interpretations of values but simply assigns value from module deployment to `securityContext` parameter of a pod spec.

```json
{
  "k8s-experimental": {
    "securityContext": {
      "fsGroup": "1",
      "runAsGroup": "1001",
      "runAsUser": "1000",
      ...
    }
  }
}
```