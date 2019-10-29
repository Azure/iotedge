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

`iotedged` is the most privileged component in an Edge deployment so it requires a ClusterRole but with very limited scope of roles. It needs these permissions to start the EdgeAgent and monitor its status. The full list of permissions required for iotedged can be found in the [main repository](../charts/edge-kubernetes/templates/edge-rbac.yaml). 

In addition to standard permissions to list, create, delete, update and watch Kubernetes resources like Deployments, Pods, Services, ConfigMaps etc (within the device namespace), it requires security related permissions.

`iotedged` has a ClusterRole because it performs the following cluster wide operations:
* When reporting system information, it lists nodes and collects all unique types of architecture with number of nodes of this type. 
* TokenReview, which is the foundation of module authentication.

Each installation will create its own ClusterRole and ClusterRoleBinding for iotedged.

## EdgeAgent

EdgeAgent doesn’t need to perform cluster-wide operations so it is downgraded to a Role with permissions scoped to the namespace the edge device is installed in. All security operations are delegated to iotedged.

## EdgeHub and modules

EdgeHub and other modules shouldn’t rely on any Kubernetes specific APIs and are runtime agnostic. As a result, no specific roles and permissions to access Kubernetes resources is required.

# Module Authentication

In order to authenticate modules in Kubernetes iotedged leverages approach Kubernetes API itself uses to authenticate its clients.

Each Edge module has a dedicated ServiceAccount assigned to deployment. This ServiceAccount works as module identity in Kubernetes cluster. It doesn’t require to have any roles associated with it so EdgeAgent doesn’t create any Roles and RoleBindings for modules. Each deployed pod module contains a token that can be passed to iotedged as an Authorization bearer token which iotedged reviews against Kubernetes API. The response contains a status field of the request to indicate the success of the review. If the review finishes successfully it will contain the name of the ServiceAccount the token belongs to. A given ServiceAccount is used as a module identity to allow an access to certain iotedged operation calls.

For each user module, EdgeAgent puts a sidecar proxy container that establish secure TLS connection between iotedged and module container. The trust bundle that iotedged generates during an initialization process is mounted as a ConfigMap volume to a proxy container. It contains certs required to establish secure communication with iotedged.

In addition, proxy reads auth token file from mounted pod volume and provide it as an Authorization bearer token with every outgoing request to iotedged.

From a module point of view, it communicates with iotedged via HTTP and all necessary work to secure connection is taking care of by sidecar proxy.

```
+--------------------------------------------------------------------------------+
| +----------------+                  +----------------------------------------+ |                
| | +------------+ |                  | +-----------+            +-----------+ | |                
| | |            | |      HTTPS       | |           |    HTTP    |           | | |                 
| | |  iotedged  |<-------------------->|   proxy   |<---------->|  ModuleA  | | |                 
| | |            | |   Authorization  | |           |            |           | | |                 
| | +------------+ |                  | +-----------+            +-----------+ | |                 
| | pod   ^        |                  | pod                                    | |                 
| +-------|--------+                  +----------------------------------------+ |                 
|         |                                                           namespace  |                  
+---------|----------------------------------------------------------------------+
          |
    HTTPS | POST                                                                              
          | TokenReview                                                                            
          v                                                                                        
    +------------+                                                                                 
    |  Kube API  |                                                                                 
    +------------+                                                                                 
```

