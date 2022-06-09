# Manifest Signer Client

Manifest Signer Client is a tool that will be used for the Manifest Trust feature. The deployment manifest JSON will be signed using the tool Manifest Signer client and the signatures will be verified by the IoT Edge Runtime once the signed deployment manifest is deployed.

Manifest Signer client gives a signed deployment JSON file as an end output. 

### Step 1: Certificate Generator
OpenSSL is needed to generate the certificate. Please follow the instructions [here](https://github.com/Azure/iotedge/blob/main/edgelet/doc/devguide.md#windows-1).
Manifest signer client needs a root Certificate Authority and a signer cert. To achieve that Certificate Generator scripts will be used. There are two folders in the CertificateGenerator folder. One for Windows and other for Linux.

Under Windows, there are two scripts. One for RSA and other EDCDsa.
1. Gen_RootCA_with_Signer_ECDsa.bat
2. Gen_RootCA_with_Signer_RSA.bat

Under Linux, same format is followed.
1. Gen_RootCA_with_Signer_ECDsa.sh
2. Gen_RootCA_with_Signer_RSA.sh

##### Set of choices in DSA algorithms
1. The first decision to make is which DSA algorithm schemes to choose. We highly recommend using ECDSA as it generates smaller signatures. Choose the scripts accordingly with the corresponding labelled script. 
2. Next choice is the different algorithmic parameters. For ECDsa, there are three EC parameters to choose from secp521r1 , secp384r1, prime256v1. Edit the parameter `ROOT_KEY_ALGO` and `SIGNER_KEY_ALGO`. Default value for root CA is `scep521r1` and signer cert is `prime256v1`
3. Next choice is the different SHA algorithm. For RSA and ECDsa, there are three choices of SHA algorithm to choose from  SHA256, SHA384, SHA512. Recommendation is to use larger keys for root CA and smaller keys for signer certs. Edit the parameter `ROOT_SHA_ALGORITHM` and `SIGNER_SHA_ALGORITHM`. Default value is `SHA256`
3. The file names of the root CA, root private key, signer cert, signer private key are also editable in the script. 

Once the variables are set in the script. Run the script in a `Powershell` and the following files will be generated as a result.

1. `root_ca_private_<algo>_key.pem` - private root key
2. `root_ca_public_<algo>_cert.pem` - public root key / root CA
3. `root_ca_public_<algo>_key.srl` - root .srl file
4. `signer_private_<algo>_key.pem` - private signer key
5. `signer_public_<algo>_key.pem` - prublic signer key
5. `signer_public_<algo>_cert.pem` - public signer cert
6. `signer_<algo>.csr` - signer .csr file

The files that are needed to launch and sign deployment manifest JSON is as follows. 
1. `root_ca_public_<algo>_cert.pem` - public root key / root CA
2. `signer_private_<algo>_key.pem` - private signer key
3. `signer_public_<algo>_cert.pem` - public signer cert

### Step 2: Launch Settings
Manifest Signer client has  `launchSettings.json` under `Properties` folder and the following values have to be set to sign the deployment manifest file. 
1. `MANIFEST_VERSION` is the version of the deployment manifest JSON. Each time a new deployment file is generated, the manifest version has to be incremented. To begin, give `1` as the value. 
2. `DSA_ALGORITHM` is the DSA algorithm scheme. The values supported for ECDsa are `ES256`, `ES384` and `ES512`. For RSA, it is `RS256`, `RS384` and `RS512`
3. `DEPLOYMENT_MANIFEST_FILE_PATH` is the absolute path of the deployment manifest to be signed including the file name. 
4. `SIGNED_DEPLOYMENT_MANIFEST_FILE_PATH` is the absolute path of the signed deployment manifest including the file name of your choice. 
5. `MANIFEST_TRUST_DEVICE_ROOT_CA_PATH` is the absolute path of the file `root_ca_public_<algo>_cert.pem`
6. `MANIFEST_TRUST_SIGNER_PRIVATE_KEY_PATH` is the absolute path of the file  `signer_private_<algo>_key.pem`
7. `MANIFEST_TRUST_SIGNER_CERT_PATH` is the absolute path of the file `signer_public_<algo>_cert.pem` 

### Step 3: Build and Run Manifest Signer Client
Once the `launchSettings.json` file is configured, the solution can be built and run using `dotnet build` and `dotnet run`. If all the inputs are configured properly, then signed deployment JSON will be generated. 