# Conversion of IoT Edge Deployment ModuleSpec to Kubernetes Deployments.

Azure IoT Edge was originally designed for running on a standalone system. Now with IoT Edge on 
Kubernetes, Azure IoT Edge supports deployments of edge devices on a Kubernetes cluster. 
The expectation is that these edge deployments designed for running on a single node will "just work" 
when installed in a Kubernetes cluster.  In order to do that, IoT Edge on K8s has to transform the 
edge deployment specifcation into Kubernetes objects which emulate the IoT Edge single device 
experience.

Existing edge deployments running on a single system specify some module settings via the [Docker 
ContainerCreate structure](https://docs.docker.com/engine/api/v1.40/#operation/ContainerCreate). 
This means IoT Edge on K8s will need to translate IoT Edge device and module configuration based 
on these settings. This document will describe which values are used from the module specification 
and how they are transformed into Kubernetes objects. This document will also show how settings in
the Helm chart affect these translations.

IoT Edge on K8s will create Namespaces, Deployments, Services, ImagePullSecrets, 
PersistentVolumeClaims, and ServiceAccounts to establish the IoT Edge runtime.
These Docker to K8s object mappings will be described in detail in subsequent sections.

## Map of configuration source to Kubernetes objects

The following tables link the critical parts of an edge deployment to the associated Kubernetes 
objects that will be created.

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
| createOptions.HostConfig.IpcMode | [Deployment](#podtemplate) |
| createOptions.HostConfig.NetworkMode | [Deployment](#podtemplate) |
| createOptions.HostConfig.PidMode | [Deployment](#podtemplate) |
| createOptions.HostConfig.ExposedPorts | [Service](#service) |
| createOptions.HostConfig.PortBindings | [Service](#service) |

### Helm Chart

Some information needed to provision the edge deployment isn't available until the runtime is 
installed on the cluster. These are the settings which can be assigned in the Helm chart, and how 
they affect the Kubernetes objects.

| Setting | Affected k8s objects |
| ------- | -------------------- |
| edgeAgent.env.portMappingServiceType | [Service](#service) |
| edgeAgent.env.persistentVolumeClaimDefaultSizeInMb | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.useMountSourceForVolumeName | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.storageClassName | [Deployment](#podtemplate), [PersistentVolumeClaim](#persistentvolumeclaim) |
| edgeAgent.env.enableExperimentalFeatures | [Deployment](#podtemplate) - See [K8s Extensions](#k8s-extensions) for details |
| edgeAgent.env.enableK8sExtensions | [Deployment](#podtemplate) - See [K8s Extensions](#k8s-extensions) for details |

## Conversion details

The following sections describe in detail how the edge deployment gets translated into Kubernetes
objects.

## Namespace

IoT Edge on K8s is intended to be installed in a namespace - the `default` namespace is not recommended. 
The namespace is provided by the user and must be created before the runtime can be installed. The 
Azure IoT Edge runtime will be installed in the given namespace, and there is a 1 to 1 relationship 
between that namespace and the edge device. Two devices may not be installed in the same namespace, 
but there may be multiple namespaces in the cluster running IoT Edge on K8s. All objects created by 
IoT Edge on K8s will be namespaced.

## Common labels

There will be a default label set used on most created objects in IoT Edge on K8s deployment. Three 
labels are automatically put into object metadata:
- **net.azure-devices.edge.module** = module name
- **net.azure-devices.edge.deviceid** = device id
- **net.azure-devices.edge.hub** = associated IoT Hub name.

All values will be sanitized to be a K8S label value.  For example, "edgeHub" will be transformed 
to "edgehub".

## Deployment

Each IoT Edge Module will create one Deployment. This will run the module's specified container image.

### metadata

- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **namespace**   = The given namespace.
- **labels**      = Default label set.
- **annotations** = Deployments will have the original JSON creation string added in the field "net.azure-devices.edge.creationstring".

### spec (DeploymentSpec)

- **replicas** = Currently set to 1 - only 1 Pod will be created for each module.
- **selector** = This currently matches the Default label set.
- **template** = See [PodTemplate](#podtemplate).

### PodTemplate

##### metadata

- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **namespace**   = The given namespace.
- **labels**      = Default label set.
- **annotations** = The pod's metadata will have one fixed annotation:
    - **net.azure-devices.edge.original-moduleid** = unsanitized module id from edge deployment specification.
    - then `settings.createOptions.Labels` will be added to the pod's annotations.
    
##### spec (PodSpec)
- **containers**
    - **Proxy container**
      This will contain the specification for the module to iotedged proxy.
    - **Module container**
        - **name** = Name will be the module name, sanitized to be a K8s identifier.
        - **image** = `settings.image`
        - **env** = env will contain some predefined settings for all modules, then:
            - Add all environment variables from `env` section in module spec.
            - Add all environment variables from `settings.createOptions.Env`.
        - **securityContext**
            - privileged = Derived from `settings.createOptions.HostConfig.Privileged`.
        - **volumeMounts** = There are 4 sources to volume mounts.
            - bind mounts from `settings.createOptions.HostConfig.Binds` in format "host path:target path[:readwrite mode]".
              *This is not recommended for IoT Edge on K8s.*
                - Name = Sanitized host path.
                - MountPath = target path.
                - readOnlyProperty = readwrite mode if set, default is false.
            - bind mounts from `settings.createOptions.HostConfig.Mounts`
              *This is not recommended for IoT Edge on K8s.*
                - Name = mount.Source
                - MountPath = mount.Target
                - readOnlyProperty = readwrite mode if set, default is false.
            - volume mounts from `settings.createOptions.HostConfig.Mounts`
                - Name = mount.Source
                - MountPath = mount.Target
                - readOnlyProperty = readwrite mode if set, default is false.
            - volume mounts from `settings.k8s-extensions.volumes[*].volumeMounts`. Placed in spec 
              as provided.
- **imagePullSecrets**
    If the module server address matches a server address in the edge deployment specification's 
    *Container Registry Settings*, this will be an array with one name, the created ImagePullSecret.
- **volumes** There are 4 sources for volumes.
    - bind mounts from `settings.createOptions.HostConfig.Binds` in format "host path:target path[:readwrite mode]".
      *This is not recommended for IoT Edge on K8s.*
        - name = Sanitized host path
        - hostPath
            - path = host path.
            - type = "DirectoryOrCreate".
    - bind mounts from `settings.createOptions.HostConfig.Mounts`
      *This is not recommended for IoT Edge on K8s.*
        - name = mount.Source
        - hostPath
            - path = mount.Source
            - type = "DirectoryOrCreate".
    - volume mounts from `settings.createOptions.HostConfig.Mounts`
        - name = mount.Source
        - persistentVolumeClaim is always assigned
            - if edge runtime is started with `useMountSourceForVolumeName` or `storageClassName` set it will create a [Persistent Volume Claim](#PersistentVolumeClaim)
            - if neither is set Pod will be created but stuck in `Pending` phase until a PVC is created.
            - claimName = mount.Source
            - readOnlyProperty = mount.ReadOnly
    - volume mounts from `settings.k8s-extensions.volumes[*].volume`. Placed in spec as provided.
- **serviceAccountName** = The module name, sanitized to be a K8s identifier. See [Module Authentication](rbac.md#module-authentication) for details.
- **nodeSelector** = `settings.k8s-extensions.nodeSelector` Placed in spec as provided.
- **hostIPC** = `settings.createOptions.HostConfig.IpcMode` if IpcMode=host, we will set hostIPC to true
- **hostNetwork** = `settings.createOptions.HostConfig.NetworkMode` if NetworkMode=host, we will set hostNetwork to true
- **hostPID** = `settings.createOptions.HostConfig.PidMode` if PidMode=host, we will set hostPID to true
- **dnsPolicy** = `settings.createOptions.HostConfig.NetworkMode` if NetworkMode=host, we will set hostNetwork to `ClusterFirstWithHostNet`

## Service
### metadata
- **name**        = Name will be the module name, sanitized to be a K8s identifier.
- **namespace**   = The given namespace.
- **labels**      = Default label set.
- **annotations** = The service's metadata will have one fixed annotation:
    - **net.azure-devices.edge.creationstring** = the original JSON creation string for this object.
    - then `settings.createOptions.Lables` will be added to the service's annotations.

##### spec (ServiceSpec)
- **type** = Set according to this priority:
     1. `type` from `settings.k8s-extensions.serviceOptions.type`. 
     2. ClusterIP if only exposed ports (`settings.createOptions.HostConfig.ExposedPorts`) are set. 
     3. If port bindings (`settings.createOptions.HostConfig.PortBindings`) are set, runtime will use the default set by `portMappingServiceType` on runtime startup, default is ClusterIP.
- **loadBalancerIP** = `loadBalancerIP` from `settings.k8s-extensions.serviceOptions.loadBalancerIP`.
- **ports** = a list of port bindings
    - **port** = Exposed port if source is `settings.createOptions.HostConfig.ExposedPorts`, 
      host port if source is `settings.createOptions.HostConfig.PortBindings`
    - **name** = "ExposedPort-<port>-<protocol>" or "HostPort-<port>-<protocol>"
    - **protocol** = protocol from port setting, default "TCP"
    - **targetPort** = Assigned if source is `settings.createOptions.HostConfig.PortBindings`, 
      set to Portbinding's target port.
- **selector** = Default label set.

## ImagePullSecret

Image pull secrets are derived from the edge deployment specification's *Container Registry Settings*, 
which contain the following fields:
- **UserName**
- **ServerAddress**
- **Password**

### metadata
- **name**        = "<UserName>-<ServerAddress>"
- **namespace**   = The given namespace.
### data
- **.dockerconfigjson** = Base64 encoding of the pull secret.

## PersistentVolumeClaim

Persistent volume claims are created when a docker volume is requested (all volume mounts from 
`settings.createOptions.HostConfig.Mounts`), the persistent volume claim has not been created by the 
user, and the runtime has been set to expect to use persistent volumes.

The runtime is set to expect PVs by assigning `storageClassName` and a value for 
`persistentVolumeClaimDefaultSizeInMb`. This value gives the IoT Edge Runtime a default claim size 
as this is not provided by `createOptions`. An optional value `useMountSourceForVolumeName` informs 
the runtime to use `mount.Source` in the Docker Volume spec as the persistent volume name.

### metadata

- **name**        = mount.Source from `settings.createOptions.HostConfig.Mounts`.
- **namespace**   = The given namespace.
- **labels**      = Default label set.

### spec (PersistentVolumeClaimSpec)
- **accessModes** = list with one element, "ReadOnlyMany" if mount.ReadOnly is true, otherwise, 
  "ReadWriteMany"
- **resources**
    - **requests**
        - **storage** = `persistentVolumeClaimDefaultSizeInMb`
    - **volumeName** = mount.Source if `useMountSourceForVolumeName` is true, unset otherwise.
    - **storageClass** = `storageClassName` - assigned if set.

## ServiceAccount

Service accounts are described in the [document describing our IoT Edge on Kubernetes RBAC](rbac.md).


### K8s Extensions

Some Kubernetes concepts are not represented in the Docker ContainerCreate structure. IoT Edge on 
K8s has provided extensions to the createOptions which will give some useful extensions to an 
IoT Edge application running on Kubernetes. This is described in [Kubernetes createOptions 
Extensions](create-options.md).

Extensions available:
- [Kubernetes native volume support](create-options.md#volumes)
- [Setting CPU and Memory limits](create-options.md#cpu-memory-and-device-resources)
- [Assigning Modules to Nodes](create-options.md#assigning-modules-to-nodes)
- [Applying Pod Security Context](create-options.md#apply-pod-security-context)
- [Applying Service Options](create-options.md#apply-service-options)
