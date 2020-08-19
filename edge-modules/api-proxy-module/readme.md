# API proxy module for nested IoT Edge in layered network (ISA-95)
When inside a layered network, IoT Edge doesn't have direct internet access to internet access. 
The API proxy module provides a way to maintain all the services an IoT Edge provide - inside a layered network - without tunneling and by terminating the connection at each layer

## Architecture
We leverage a [nginx](http://nginx.org/) reverse proxy IoT Edge module to route messages through the layers. The diagram below illustrates this approach: 

![](images/concept.png)

## Design
![](images/api_proxy_module_design.png)
In blue: The configuration path. It is possible to customize the API proxy configuration via Twin.
- 1a: edgeHub notify a new twin is available
- 2a: configuration template is replaced
- 3a: configuration template is parsed and if successfull replaced on disk
- 4a: request to reload nginx
- 5a: nginx reloads with new config

In red: The certificate path
- 1b: check if current certificate are expired. If yes, get certificate from workload API
- 2b: save certs on disk
- 3b: request a reload of nginx
- 4b: nginx reloads with new certs


## Setup instructions 
### Build the api-proxy-module image
1. Clone this repository.

2. Build the image by running the following command:

    ```
    $ ./edge-modules/api-proxy-module/build.sh -t x86_64
    ```

    > To build an image for ARM, run the above command on a Linux ARM32 machine and change the -t switch to `armv7l`

3. Tag the image as desired and push to the container registry used for your IoT Edge deployment.

### Setup the proxy module
To avoid creating a full proxy configuration from scratch, the API proxy module provide a modular default configuration.
That configuration is controlled through the environment variables of the container

| Environment variable  | comments |
| ------------- |  ------------- |
| NGINX_DEFAULT_PORT  | Changes the port Nginx listens too. If you change this option, make sure that the port you select is exposed in the dockerfile. Default is 443  |
| NGINX_HAS_BLOB_MODULE | If set to 1, nginx will add logic to route request to the blob module located on the same device  |
| NGINX_BLOB_MODULE_NAME_ADDRESS | name of the blob module  |
| NGINX_ROUTE_DOCKER_REQUEST_TO_PARENT | set to 1 to pull image  |
| NGINX_ROUTE_DOCKER_REQUEST_TO_LOCAL_REGISTRY | If set to 1, nginx will add logic to route request to the registry module located on thate  |
| NGING_REGISTRY_MODULE_ADDRESS | name of the registry module  |
| NGINX_NOT_ROOT | Set to 1 the edge device is located at the root  |

### Setup for pulling container images
There are several different kind of configurations:
- Without local registry: 



![](images/set_env_var.png)

### Creating a custom configuration


Remove edge hub 443
Add 443 to proxy


Copy /mqtt/mqtt in the folder