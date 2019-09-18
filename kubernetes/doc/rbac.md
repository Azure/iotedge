# Edge RBAC

IoT Edge runtime on Kubernetes leverages standard [RBAC](https://kubernetes.io/docs/reference/access-authn-authz/rbac/) method to regulate access to resources inside Kubernetes cluster. Edge runtime installs itself in a namespace provided by user. All resources it creates during installation and work are scoped to the namespace.

```
+----------------+       +-------------------------------+      +--------------------------------------+
| ServiceAccount |---+---| ClusterRoleBinding            |------| ClusterRole                          |
| iotedged       |   |   | iotedge:{name}:auth-delegator |      | system:auth-delegator                |
+----------------+   |   +-------------------------------+      +--------------------------------------+
                     |   +-------------------------------+      +--------------------------------------+
                     +---| ClusterRoleBinding            |------| ClusterRole                          |
                     |   | iotedge:{name}:node-observer  |      | iotedge:{name}:iotedge:node-observer |
                     |   +-------------------------------+      +--------------------------------------+
                     |   +-------------------------------+      +--------------------------------------+
                     +---| RoleBinding                   |------| Role                                 |
                         | iotedged                      |      | iotedged                             |
                         +-------------------------------+      +--------------------------------------+


+----------------+       +-------------------------------+      +--------------------------------------+
| ServiceAccount |-------| RoleBinding                   |------| Role                                 |
| edgeagent      |       | edgeagent                     |      | edgeagent                            |
+----------------+       +-------------------------------+      +--------------------------------------+
```

## iotedged

An iotedged component requires the most privileged entity in the Edge deployment so it has ClusterRole but with very limited scope of roles. The amount of permissions is related to the amount of operations it needs to successfully install EdgeAgent and monitor its running status. The full list of permissions required for iotedged can be found in the [main repository](../charts/edge-kubernetes/templates/edge-rbac.yaml). 

In addition to standard permissions to list, create, delete, update and watch Kubernetes resources like Deployments, Pods, Services, ConfigMaps etc it has security related permissions.

An iotedged has a ClusterRole because it is required to perform cluster wide operations. When reporting system information, it lists nodes and collects all unique types of architecture with number of nodes of this type. Another cluster-wide operation is TokenReview that is a foundation of module authentication.

Each installation will create its own ClusterRole and ClusterRoleBinding for iotedged.

## EdgeAgent

An EdgeAgent component doesn’t require cluster-wide operations so it is downgraded to a Role with permissions scope to a namespace Edge installed in. All security operations are delegated to iotedged.

## EdgeHub and modules

EdgeHub and other modules shouldn’t rely on any Kubernetes specific APIs and be runtime agnostic. As a result, no specific roles and permissions to access Kubernetes resources is required.

# Module Authentication

In order to authenticate modules in Kubernetes iotedged leverages approach Kubernetes API itself uses to authenticate its clients.

Each Edge module has a dedicated ServiceAccount assigned to deployment. This ServiceAccount is working as module identity in Kubernetes cluster. It doesn’t require to have any roles associated with it so EdgeAgent doesn’t create any Roles and RoleBindings for modules. Each pod module was deployed on contains a token that can be passed to iotedged as an Authorization bearer token. An iotedged attempts to review token against Kubernetes API. The response contains a status field of the request to indicate the success of the review. If review finished successfully it will contain the name of the ServiceAccount the token belongs to. A given ServiceAccount is used as a module identity to allow an access to certain iotedged operation calls.

Along with a container with user module EdgeAgent puts sidecar proxy container that establish secure TLS connection between iotedged and module container. The trust bundle that iotedged generates during an initialization process is mounted as a ConfigMap volume to a proxy container. It contains certs essential to establish secure communication with iotedged.

In addition, proxy reads auth token file from mounted pod volume and provide it as an Authorization bearer token with every outgoing request to iotedged.

From a module point of view, it communicates with iotedged via HTTP and all necessary work to secure connection is taking care of by sidecar proxy.

```
+----------------+                  +----------------------------------------+                 
| +------------+ |                  | +-----------+            +-----------+ |                 
| |            | |      HTTPS       | |           |    HTTP    |           | |                 
| |  iotedged  |<-------------------->|   proxy   |<---------->|  ModuleA  | |                 
| |            | |  Authrorization  | |           |            |           | |                 
| +------------+ |                  | +-----------+            +-----------+ |                 
| pod   ^        |                  | pod                                    |                 
+-------|--------+                  +----------------------------------------+                 
        |                                                                                        
  HTTPS | POST                                                                                   
        | TokenReview                                                                            
        v                                                                                        
  +------------+                                                                                 
  |  Kube API  |                                                                                 
  +------------+                                                                                 
```

