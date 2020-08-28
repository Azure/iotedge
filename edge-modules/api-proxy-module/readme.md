# API proxy module for nested IoT Edge in layered network (ISA-95)
When inside a layered network, IoT Edge doesn't have direct internet access. 
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
- 1b: check if current certificate is expired. If yes, get the certificate from the workload API.
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
| DOCKER_REQUEST_ROUTE_ADDRESS | Address to route docker request. By default it points to the parent.  |
| PROXY_CONFIG_ENV_VAR_LIST | List all the variable to be replace. By default it contains: NGINX_DEFAULT_PORT,NGINX_HAS_BLOB_MODULE,NGINX_BLOB_MODULE_NAME_ADDRESS,DOCKER_REQUEST_ROUTE_ADDRESS,NGINX_NOT_ROOT,PARENT_HOSTNAME  |

### How it works
When the application boots up, it does two things:
* It sets a value to all the env var that have default value (see above)
* It dereferences all every environment variable that point to another environment variable. It is possible to set 1 level of indirection. 
```
For example:
The environment variable DOCKER_REQUEST_ROUTE_ADDRESS = "${PARENT_HOSTNAME}"
With PARENT_HOSTNAME="127.0.0.1"
After calling we want DOCKER_REQUEST_ROUTE_ADDRESS="127.0.0.1"
```

The template sent through the twin is received by the application and parsed.
The parsing goes through 2 steps:
* All environment variables contained in PROXY_CONFIG_ENV_VAR_LIST are replaced by their value using subst
* Everything that is between #if_tag 0 and #endif_tag 0 or between  #if_tag !1 and #endif_tag !1 is replaced.
It is possible to configure some kind of "conditional configuration"
```
For example:
The environment variable REMOVE_BETWEEN1 = 0
The environment variable REMOVE_BETWEEN2 = 1

A configuration like this:
{
    #if_tag ${REMOVE_BETWEEN1}
        will be removed
    #endif_tag ${REMOVE_BETWEEN1}

    #if_tag !${REMOVE_BETWEEN1}
        will be NOT be removed: REMOVE_BETWEEN1
    #endif_tag ${REMOVE_BETWEEN1}

    #if_tag ${REMOVE_BETWEEN2}
        will be NOT be removed: REMOVE_BETWEEN2
    #endif_tag ${REMOVE_BETWEEN2}

    #if_tag !${REMOVE_BETWEEN2}
        will be removed: REMOVE_BETWEEN2
    #endif_tag ${REMOVE_BETWEEN2}
}
Result after parsing
{
    #if_tag 0
    #endif_tag 0

    #if_tag !0
        will be NOT be removed: REMOVE_BETWEEN1
    #endif_tag 0

    #if_tag 1
        will be NOT be removed: REMOVE_BETWEEN2
    #endif_tag 1

    #if_tag !1
    #endif_tag 1
}

```


### Setup for pulling container images
There are several different kinds of configurations:
- With local registry at the root: 
    - Config for root device:
![](images/set_env_var_local_registry.png)
    - Config for other devices:
![](images/set_env_var_no_local_registry.png)

Make sure to remove port 443 that is opened by edgehub or use another port that is available on the image.
To open port 443 for the API proxy the options should be set as the following:
![](api-proxy-create-options.png)

The edgedevices inside the layers will
