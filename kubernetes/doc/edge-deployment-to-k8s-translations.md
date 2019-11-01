# Conversion of Edge Deployment ModuleSpec to Kubernetes Deployments.

Azure IoT Edge on Kuberenetes project will take an existing Azure IoT Edge application that is 
running on a single device and install that application into a Kubernetes cluster.  The expectation 
is that these applications will "just work," regarless of where it is installed.  In order to do 
that, the Edge on K8s project has to transform the edge deployment into Kubernetes objects which 
will provide the framework for modules to communicate with other modules.

Existing Edge Deployments running on a single device specifies module setting via the [Docker 
ContainerCreate structure](https://docs.docker.com/engine/api/v1.40/#operation/ContainerCreate). 
This means Edge on K8s will have to make decisions about how to provide the module framework based 
on these settings. This document will describe which values are used from the module specification 
and how they are transformed into Kubernetes objects.

The application will create Deployments, Services, ImagePullSecrets, PersistentVolumeClaims, and 
ServiceAccounts to establish this framework.
Each will be described in detail in subsequent sections.

## Deployment

Each Edge Module will create one Deployment. 

There will be a default label set used throughout the deployment. Three labels assigned to every 
object, all values will be sanitized to be a K8S label value:
- **net.azure-devices.edge.module** = module name
- **net.azure-devices.edge.deviceid** = device id
- **net.azure-devices.edge.hub** = associated IoT Hub name.

### metadata

- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **labels**      = Default label set
- **annotations** = Deployments will have the original JSON creation string added in the field "net.azure-devices.edge.creationstring".

### spec (DeploymentSpec)

- **replicas** = Currently set to 1 - only 1 Pod will be created for each module.
- **selector** = This currently matches the Default label set.
- **template** = See [PodTemplate](#podtemplate)

### PodTemplate

##### metadata

- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **labels**      = Default label set
- **annotations** = The pod's metadata will have one fixed annotation:
    - **net.azure-devices.edge.original-moduleid** = unsanitized module id from Edge Deployment Spec.
    - then `settings.createOptions.Labels` will be added to the pod's annotations.
    
##### spec (PodSpec)
- **containers**
    - **Proxy container**
      This will contain the specification for the module to iotedged proxy.
    - **Module container**
        - **name** = Name will be the module name, sanitized to be a K8s identifier.
        - **image** = `settings.image`
        - **env** = env will contain some predefined settings for all modules, then:
            - Add all environment variables from `env` section in module spec
            - Add all environment variables from `settings.createOptions.Env`
        - **securityContext**
            - privileged = Derived from `settings.createOptions.HostConfig.Privileged`
        - **volumeMounts** = There are 4 sources to volume mounts.
            - bind mounts from `settings.createOptions.HostConfig.Binds` in format "host path:target path[:readwrite mode]".
              *This is not recommended for Edge on K8s.*
                - Name = Sanitized host path
                - MountPath = target path
                - readOnlyProperty = readwrite mode if set, default is false
            - bind mounts from `settings.createOptions.HostConfig.Mounts`
              *This is not recommended for Edge on K8s.*
                - Name = mount.Source
                - MountPath = mount.Target
                - readOnlyProperty = readwrite mode if set, default is false
            - volume mounts from `settings.createOptions.HostConfig.Mounts`
                - Name = mount.Source
                - MountPath = mount.Target
                - readOnlyProperty = readwrite mode if set, default is false
            - volume mounts from `settings.k8s-extensions.volumes[*].volumeMounts`. Placed in spec 
              as provided.
- **imagePullSecrets**
    If the module server address matches a server address in the Edge Deployment's 
    *Container Registry Settings*, this will be an array with one name, the created ImagePullSecret.
- **volumes** There are 4 sources for volumes.
    - bind mounts from `settings.createOptions.HostConfig.Binds` in format "host path:target path[:readwrite mode]".
      *This is not recommended for Edge on K8s.*
        - name = Sanitized host path
        - hostPath
            - path = host path
            - type = "DirectoryOrCreate"
    - bind mounts from `settings.createOptions.HostConfig.Mounts`
      *This is not recommended for Edge on K8s.*
        - name = mount.Source
        - hostPath
            - path = mount.Source
            - type = "DirectoryOrCreate"
    - volume mounts from `settings.createOptions.HostConfig.Mounts`
        - name = mount.Source
        - persistentVolumeClaim is assigned if edge runtime is started with `persistentVolumeName` 
          or `storageClassName` set
            - claimName = mount.Source
            - readOnlyProperty = mount.ReadOnly
        - emptyDir is assigned otherwise.
    - volume mounts from `settings.k8s-extensions.volumes[*].volume`. Placed in spec as provided.
- **serviceAccountName** = The module name, sanitized to be a K8s identifier.
- **nodeSelector** = `settings.k8s-extensions.nodeSelector` Placed in spec as provided.

## Service
### metadata
- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **labels**      = Default label set
- **annotations** = The service's metadata will have one fixed annotation:
    - **net.azure-devices.edge.creationstring** = the original JSON creation string for this object.
    - then `settings.createOptions.Lables` will be added to the service's annotations.

##### spec (ServiceSpec)
- **type** = ClusterIP if only exposed ports (`settings.createOptions.HostConfig.ExposedPorts`) are 
  set. If port bindings (`settings.createOptions.HostConfig.PortBindings`) are set, runtime will use
  the default set by `portMappingServiceType` on runtime startup, default is ClusterIP.
- **ports** = a list of port bindings
    - **port** = Exposed port if source is `settings.createOptions.HostConfig.ExposedPorts`, 
      host port if source is `settings.createOptions.HostConfig.PortBindings`
    - **name** = "ExposedPort-<port>-<protocol>" or "HostPort-<port>-<protocol>"
    - **protocol** = protocol from port setting, default "TCP"
    - **targetPort** = Assigned if source is `settings.createOptions.HostConfig.PortBindings`, 
      set to Portbinding's target port.
- **selector** = Default label set.

## ImagePullSecret

Image pull secrets are derived from the Edge Deployment's *Container Registry Settings*, which 
contain the following fields:
- **UserName**
- **ServerAddress**
- **Password**

### metadata
- **name**        = "<UserName>-<ServerAddress>"
### data
- **.dockerconfigjson** = Base64 encoding of the pull secret.

## PersistentVolumeClaim

Persistent volume claims are created when a docker volume is requested (all volume mounts from 
`settings.createOptions.HostConfig.Mounts`), the peristent volume claim has not been created by the 
user, and the runtime has been set to expect to use persistent volumes.

The runtime is set to expect PVs by assigning `persistentVolumeName` or `storageClassName` at 
startup. User will also need to set a value for `persistentVolumeClaimDefaultSizeInMb`. This value 
gives the Edge Runtime a default claim size as this is not provided by `createOptions`.


### metadata

- **name**        = mount.Source from `settings.createOptions.HostConfig.Mounts`
- **labels**      = Default label set

### spec (PersistentVolumeClaimSpec)
- **accessModes** = list with one element, "ReadOnlyMany" if mount.ReadOnly is true, otherwise, 
  "ReadWriteMany"
- **resources**
    - **requests**
        - **storage** = `persistentVolumeClaimDefaultSizeInMb`
    - **volumeName** = `persistentVolumeName` - assigned if set.
    - **storageClass** = `storageClassName` - assigned if set and `persistentVolumeName` is not set.

## ServiceAccount

Service accounts are described in the document describing our [Edge RBAC](rbac.md)

## Map of configuration source to Kubernetes objects

### Helm Chart

| Setting | Affected k8s objects |
| ------- | -------------------- |
| edgeAgent.env.portMappingServiceType | [Service](#service) |
| edgeAgent.env.persistentVolumeClaimDefaultSizeInMb | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.persistentVolumeName | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.storageClassName | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.enableExperimentalFeatures | [Deployment](#podtemplate) |
| edgeAgent.env.enableK8sExtensions | [Deployment](#podtemplate) |

### EdgeDeployment

| Setting | Affected k8s objects |
| ------- | -------------------- |
| runtime.settings.registryCredentials | [Deployment](#podtemplate), [ImagePullSecret](#imagepullsecret) |

#### Module Spec

| Setting | Affected k8s objects |
| ------- | -------------------- |
| modules.`<module name>` | [Deployment](#deployment), [Service](#service), [PersistentVolumeClaim](#persistentvolumeclaim) , [ServiceAccount](#serviceaccount) |
| modules.`<module name>`.settings.image | [Deployment](#podtemplate) |
| modules.`<module name>`.settings.createOptions | See [Module Spec createOptions](#module-spec-createoptions) |
| modules.`<module name>`.env | [Deployment](#podtemplate) |

#### Module Spec createOptions

| Setting | Affected k8s objects |
| ------- | -------------------- |
| createOptions.Labels | [Deployment](#podtemplate) |
| createOptions.Env | [Deployment](#podtemplate) |
| createOptions.HostConfig.Privileged | [Deployment](#podtemplate) |
| createOptions.HostConfig.Binds | [Deployment](#podtemplate) |
| createOptions.HostConfig.Mounts | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| createOptions.HostConfig.ExposedPorts | [Service](#service) |
| createOptions.HostConfig.PortBindings | [Service](#service) |

### K8s Extensions

Some Kubernetes concepts are not represented in the Docker ContainerCreate structure. Edge on 
K8s has provided extensions to the createOptions which will give some useful extensions to an 
IoT Edge application running on Kubernetes. This is described in [Kubernetes createOptions 
Extensions](create-options.md).