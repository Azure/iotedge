## One time setup

*Note: The steps mentioned here have been run and validated on an ubuntu18.04/amd64 machine. If you are running a different os/arch, your steps may differ.*

## Setup your local machine
To run the end-to-end tests, we will be building the required binaries from the code, build container images and push them to a local container registry. The tests will install the IoT Edge runtime from the binaries on your machine and run the containers using your local registry.

##### Prerequisites

It is important that your machine meets the requirements to run IoT Edge. See our installation docs ([Linux](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux), [Windows](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-windows)) for more information on prerequisites

##### Install java
The build scripts use Java for code generation tasks, so you will need a jdk on your machine. Here is how you can install a jdk (if you don't already have one).
~~~ sh
ubuntu_release=`lsb_release -rs`
wget https://packages.microsoft.com/config/ubuntu/${ubuntu_release}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install msopenjdk-11
~~~

The steps above are for ubuntu 18.04. See [Install the Microsoft Build of the OpenJDK](https://docs.microsoft.com/en-us/java/openjdk/install) for information on how to install on other platforms.

##### Setup access to Microsoft installation packages
Prepare your machine to access the Microsoft installation packages. The steps to do this are listed below for your convenience. *Again, please look at the section in the linked document for your os/arch combination if it is different from ubuntu18.04/amd64*

~~~sh
# Setup the repository information
curl https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list > ./microsoft-prod.list

# Setup the Microsoft GPG pulic key in the apt's trusted list.
sudo cp ./microsoft-prod.list /etc/apt/sources.list.d/
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
sudo cp ./microsoft.gpg /etc/apt/trusted.gpg.d/

# Run update to update the package lists on your machine
sudo apt-get update
~~~

##### Create Root CA Certificate for your machine
Create a Root CA certificate for your machine *(See [Create Certs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-create-test-certificates?view=iotedge-2020-11#create-root-ca-certificate) for more details)*. Navigate to the directory where the certificate generation scripts are found (typically iotedge/tools/CACertificates) and run
~~~ sh
./certGen.sh create_root_and_intermediate
# This will create root and intermediate CA certs. The Root CA cert will be located in the 
# certs sub directory and will be needed to run the tests.

# These will be valid for 30 days, after which they will need to be regenerated.
~~~

##### Configure your local container registry
We will be building the container images from the codebase and pushing them to a local container registry. To prepare, we will install the moby engine and configure the container registry using the steps below. 

###### Install moby engine
See [Install a container engine](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge?view=iotedge-2020-11#install-a-container-engine) for details

~~~ sh
sudo apt-get install moby-engine
~~~
###### Create TLS certificates
We will be running the container registry with TLS and authentication turned on. If you already have certs for TLS, feel free to use those and skip the cert creation steps listed below. 
In a convenient directory, create a folder called `auth` to store your keys and certs (*The snippets below use /home/azureuser/auth*)
~~~sh
mkdir auth
cd auth
# Create a private key
openssl genrsa -out server.key  2048
# Create a csr
openssl req -new -key server.key -out server.csr
# Create a cert from the csr
openssl x509 -req -days 365 -in server.csr -signkey server.key -out server.crt

# At the end of these steps, you have 3 files in your auth directory - server.key (the  
# key), server.csr (the certificate signing request) and, server.crt (the certificate)
~~~

###### Setup native basic auth
Next, we will setup basic username/password authentication for the registry using [htpasswd](http://httpd.apache.org/docs/current/programs/htpasswd.html).
~~~ sh
sudo apt-get install apache2-utils
# Create password file
: | sudo tee htpasswd
# Create username and password. Replace the username and password strings 
# below with a user/password combo that you like
echo "password" | sudo htpasswd -iB htpasswd username
~~~

###### Configure and run the local container registry
~~~ sh
# Set up the container registry to listen on port 5000 and to restart automatically.
# Mount the auth folder into the container and point to the TLS and Basic auth files
# Restart is set to always - This will ensure that the registry is always up.
sudo docker run -d  -p 5000:5000 \
  --name registry  \
  -v /home/azureuser/auth/:/etc/security \
  -e REGISTRY_HTTP_TLS_CERTIFICATE=/etc/security/server.crt  \
  -e REGISTRY_HTTP_TLS_KEY=/etc/security/server.key \
  -e REGISTRY_AUTH=htpasswd   \
  -e REGISTRY_AUTH_HTPASSWD_PATH=/etc/security/htpasswd   \
  -e REGISTRY_AUTH_HTPASSWD_REALM="Registry Realm"  \
  --restart always  \
  registry:2

# Run docker ps to validate that the registry came up successfully
# If all goes well, you will see the something like below when you run docker ps.
# 59867fe47a5b   registry:2   "/entrypoint.sh /etc…"   6 seconds ago   Up 6 seconds   0.0.0.0:5000->5000/tcp, :::5000->5000/tcp   registry
sudo docker ps

# Run docker login to validate your setup and verify that your login/password works. 
# The login step will cache your credentials on your machine and will be required later 
# when you build and push docker images

sudo docker login localhost:5000
~~~

##### Install .NET
The end-to-end tests are written in .NET Core and run with the `dotnet test` command, and you need to install .NET Core SDK. See [Install the .NET SDK](https://docs.microsoft.com/en-us/dotnet/core/install/sdk) for further information. *Note: You will have to install .NET version 3.1.*

Here is a convenient way to install the SDK.
~~~ sh
curl -sL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod u+x dotnet-install.sh
./dotnet-install.sh -c Current
~~~

## Setup your cloud resources
The end-to-end tests require a number of azure cloud side resources i.e., IoT Hub, Device Provisioning Service, and a Storage Container to be setup. This next section will walk you through how to setup the cloud resources. 

The steps below will also include steps for the CLI. You can install the CLI by following the instructions [here](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli). _Note: If you already have the CLI installed, make sure to upgrade to the latest version of the CLI and the aziot-iot extension_ 

##### Create a resource group
If you don't already have a resource group, create one.
Here is how you can create one using the CLI
~~~ sh
az group create --name {resource group name} --location {region}
~~~

##### IoT Hub
If you don't already have an existing IoT hub, create one. There is no special configuration required, except for making sure that your IoT hub is enabled for public access.
Here is how you can create one using the CLI

~~~sh
# Create an Iot hub
az iot hub create --resource-group {resource group name} --name {IoT hub name} 
~~~

Note down the `event hub compatible endpoint` and the primary connection string of the `iothubowner` policy. These will need to be set in the `E2E_EVENT_HUB_ENDPOINT` and `E2E_IOT_HUB_CONNECTION_STRING` environment variables later to run the tests.

You can get these via the CLI by running 
~~~sh
# Primary Connection string
az iot hub connection-string show --hub-name {IoT hub name} --key-type primary

#Default eventhub compatible end point
az iot hub connection-string show --hub-name {IoT hub name} --default-eventhub

~~~
##### Device Provisioning Service (DPS)
Create a DPS instance. A subset of the end-to-end tests will use DPS for testing device provisioning scenarios. Note down the `ID Scope` of this DPS instance. You will need to set it in the `dpsIdScope` configuration variable later to run the tests.

Using the CLI,
~~~sh
az iot dps create --name {dps group name} --resource-group {resource group name} --location {region}

# To get the ID Scope
az iot dps show --name {dps group name} | grep idScope
~~~

###### Create enrollment group for symmetric key based enrollment
In your DPS instance, create a new enrollment group for symmetric key based enrollment. For this enrollment group, set the attestation type to be symmetric key, set the IoTEdge Device setting to true, and link the group to your IoT hub with the access policy of `iotHubOwner`. 

Using the CLI,
~~~sh
#Link the DPS group to your Iot hub
az iot dps linked-hub create --dps-name {dps group name} --resource-group {resource group name} \
  --connection-string "{Iot hub ConnectionString}" --location westus2

#Create enrollment group for symmetric key based enrollment
 az iot dps enrollment-group create -g {resource group name} --dps-name {dps group name} \
  --enrollment-id {symkey enrollment group name } --edge-enabled
~~~
You do not have to create the symmetric key. The system will auto generate it for you when the enrollment group is created. After the enrollment group is created, note down the Primary Key. You will need to set this in the `E2E_DPS_GROUP_KEY` environment variable later to run the tests.

See [Symmetric key attestation](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-auto-provision-symmetric-keys?view=iotedge-2020-11&tabs=windows) for further details.

###### Create enrollment group for X.509 certificate based enrollment
In your DPS instance, create another enrollment group for X.509 certificate based enrollment. For this enrollment group, set the attestation type to be certificate and upload the root CA certificate that you had created earlier, set the IoTEdge Device setting to true, and link the group to your IotHub with the access policy of `iotHubOwner`.

~~~sh
# Upload the root ca cert and set it to be verified
az iot dps certificate create --certificate-name {dps root ca name}  --resource-group {resource group name} \
  --dps-name {dps group name} --path {path to ca cert .pem file}  --verified true

# Note that the iot dps certificate will need to be updated if the root certificate is regenerated using the certGen.sh script.

# Create enrollment group for X.509 based enrollment
az iot dps enrollment-group create -g {resource group name} --dps-name {dps group name} \
  --enrollment-id {cert enrollment group name} --ca-name {dps root ca name} --edge-enabled
~~~
See [X.509 certificate attestation](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-auto-provision-x509-certs?view=iotedge-2020-11&tabs=windows) for further details.

##### Create a storage account/container
Create a storage account and storage container, with access level set to private. A subset of the tests will use the storage container to test log/diagnostics upload scenarios. To securely acesss the storage container, create a SAS token URL ( allow https only, permissions set to "write" only). Note down this URL as you will need it to set the `E2E_BLOB_STORE_SAS` environment variable later to run the tests.
~~~ sh
# Create storage account
az storage account create --name {storage account name} --resource-group {resource group name } \
  --location {region} --sku Standard_RAGRS --kind StorageV2

# Get the connection string from the Storage account. This will be needed to create the storage container
az storage account show-connection-string --name {storage account name} \
  --resource-group {resource group name} -o tsv

# Create the storage container
 az storage container create --name {container name} --resource-group {resource group name} \
   --account-name {storage account name} --connection-string "{connection string}"

~~~

Currently, the SAS URL has to created using the Azure portal.
 - Navigate to your storage account.
 - Under 'Data storage' click 'Containers', and select the right container.
 - Click on 'Shared access tokens' under 'Settings'.
 - Select 'Write' permissions and hit 'Generate SAS token and URL'.
 - Save the URL as E2E_BLOB_STORE_SAS.


###### Potential error messages you may run into

~~~
System.Security.Cryptography.CryptographicException : The owner of '~/.dotnet/corefx/cryptography/x509stores/root' is not the current user.
~~~

As a workaround, you can run

~~~ sh
chown -R root ~/.dotnet/corefx/cryptography/x509stores/root
~~~

If the SAS URL created using the Azure portal isn't correct, double check to see if there's a typo. You'll know the URL isn't correct if you see:
~~~
Task upload support bundle failed because of error Invalid URI: The URI scheme is not valid.
~~~
