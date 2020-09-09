# Notary Content Trust in IoT Edge
Content Trust is achieved in IoT edge with [Notary](https://github.com/theupdateframework/notary) which is based on The Update Framework (TUF). Notary aims to make the internet more secure by making it easy for people to publish and verify content. The goal of Content Trust in IoT edge is to secure the docker container images of the Edge modules with its trust pinned to the Edge device via a root Certificate Authority(CA). When Content Trust is enabled, IoT Edge device only deploys signed images and rejects any other images that are tampered or modified in the transfer over the internet. Feature is supported in Linux OS only for now. 

## Sign and Publish Container Images in Azure Container Registry
The container images can be signed and published in Azure Container Registry which has an in built Notary support for content trust. 

To publish signed images, following tools must be installed in a publisher's environment which is different from the Edge device. 
1. OpenSSL - to generate root CA for each registry and root certs for each container
2. Notary client - to initialize and create TUF trust collection
3. Docker client - to sign the image and publish to the Container Registry


### 1. Generate Certificates using OpenSSL
Follow instructions in [iotedge repository]([https://github.com/ggjjj/iotedge/blob/master/edgelet/doc/devguide.md](https://github.com/ggjjj/iotedge/blob/master/edgelet/doc/devguide.md)) to install OpenSSL. There are two types of certificates to be generated.
1. root CA for each container registry
2. root certificate for each container image

There is only one root CA needed for each container registry which forms the root of all trust for all its container images. When using multiple registries, multiple root CAs have to be generated. Root certificate for each image is needed to initialize the TUF trust collection using Notary.

####  Steps to create root CA for a registry
OpenSSL is a prerequisite tool that must be installed before proceeding.
`openssl genrsa -out root_ca_exampleregistry.key 2048`

`openssl req -new -sha256 -key root_ca_exampleregistry.key -out root_ca_exampleregistry.csr -subj "/C=XX/ST=XX/L=XX/O=XX/OU=XX/CN=XX"`

`openssl x509 -req -days 1000 -in root_ca_exampleregistry.csr -signkey root_ca_exampleregistry.key -out root_ca_exampleregistry.crt`

Note at the end of the above steps the following files are generated:
- root_ca_exampleregistry.key is the private key of root CA and should be kept safe in a safe vault always
- root_ca_exampleregistry.crt is the public key of root CA
- root_ca_exampleregistry.srl is the CA serial file
#### Steps to create root certificates for each container image
`openssl genrsa -aes128 2048 > root_image.key`

`openssl req -sha256 -new -key root_image.key -out root_image.csr -subj "/C=XX/ST=XX/L=XX/O=XX/OU=XX/CN=exampleregistry.azurecr.io\/image"`

`openssl x509 -req -days 1000 -sha256 -in root_image.csr -out root_image.crt -CAkey root_ca_exampleregistry.key -CA root_ca_exampleregistry.crt -CAserial root_ca_exampleregistry.srl -CAcreateserial`

Note at the end of the above steps the following files are generated:

- root_image.key is the private key used for initializing TUF trust collection 
- root_image.csr is the Certificate Signing Request file 
- root_image.crt is the public cert used for initializing TUF trust collection

Important Note:

-   The Comman Name (CN) in root certificate for each container must contain the Globally Unique Names(GUNs) i.e exampleregistry.azurecr.io/image 
-  Safely store the pass code for the private key for `root_ca_exampleregistry.key` and `root_image.key` and also it will be used in the next steps. 	

### 2. Initialize TUF trust collection using Notary client
Notary initializes the TUF trust collection for each image in the Container Registry. 

#### Notary Installation
Notary client can be installed with the command for `amd64` target platform and avoid installing using `sudo apt-get install notary`

`wget '[https://github.com/theupdateframework/notary/releases/download/v0.6.0/notary-Linux-amd64](https://github.com/theupdateframework/notary/releases/download/v0.6.0/notary-Linux-amd64)'` 

Rename `notary-Linux-amd64` to `notary` and change the permissions by `chmod +x notary` and place the binary in `/usr/bin/notary`

Once Notary client is installed, install the [Docker client](https://docs.docker.com/get-docker/). Docker client has to be aware of the TUF trust collection so do not skip installing Docker client. 

#### Notary Configuration
Notary client directory exists in `~/.notary`. A configuration file has be created in `~/.notary/config.json` with the following contents
```json
{
    "trust_dir" : "~/.docker/trust",
    "remote_server": {
     "url": "https://exampleregistry.azurecr.io"
    }
}
```
`trust_dir` is the location where the trust directory gets initialized and downloaded. `remote_server` has a sub field `url` which configures the URL of the registry server host name. 

Notary needs authorization credentials to communicate to the Notary Server. For this, a Service Principal for the Azure Container Registry with Owner and Push access must be created. Follow instructions in [this](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-service-principal) link to create a Service Principal.  The base64 encoding of username and password for the Service Principal will be used to create an environment variable for Notary called `NOTARY_AUTH`

`export NOTARY_AUTH="$(printf '%s:%s' 'username' 'password' | base64 -w 0)"`

Note: Replace username and password with actual Service Principal credentials. 

#### Notary TUF Trust collection initialization
Once the Notary is set up, the TUF trust collection for each individual image can be done.

`notary init --rootkey root_image.key --rootcert root_image.crt exampleregistry.azurecr.io/image -c ~/.notary/config.json`

Note that Notary Trust collection for an container image can be initialized only once. If a new version of the image there is no need to initialize the trust collection again. 

#### Notary Snapshot key rotation 

TUF Snapshot key must be rotated to the Notary server. With this command, there is no need to maintain the private key of the Snapshot key locally at Publisher instead it will be maintained by the Notary Server. 

`notary key rotate exampleregistry.azurecr.io/image snapshot -r -c ~/.notary/config.json`

### 3. Sign and publish images using Docker Client

Once the trust collection is created, the container image can be signed with Docker client. First login into the Container Registry by `docker login exampleregistry.azurecr.io`. Provide the Service Principal username and password to sign in. 

To enable Docker Content Trust, set the environment variable as `export DOCKER_CONTENT_TRUST=1`

To sign the target container image, `docker push exampleregistry.azurecr.io/image:v1`. Tag must be specified while signing the image. Step 3 has to be repeated whenever there is new update in the image. Now the image is successfully signed and pushed into the registry.
Note: Target role private key pass code is same as the repository key pass code. 
To check if the image is signed or not, `az` tools can be used. Before that login into the registry using `az acr login --name exampleregistry --username xxxx --password yyyy`

`az acr repository show -n exampleregistry -t image:v1`


## Enable Content Trust in IoT edge device

The root CA of each Container Registry i.e `root_ca_exampleregistry.crt` must be copied out of band into the device in a specific location.

In the `config.yaml`, in the Moby runtime section, content trust can be enabled by specifiying the registry server name and location of its corresponding root CA as shown in [sample](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/config/linux/config.yaml)

Recommendation is to create another Service Principal with Pull access for the edge device and ensure the login credentials are applied in the deployment manifest. 

Important Note: When content trust is enabled for a registry, all the contaner images of the edge modules in that registry must be signed. If content trust is enabled for a registry and if one of the images is not signed, the iotedge daemon will fail pulling the container image into the device.
