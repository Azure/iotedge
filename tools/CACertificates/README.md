# Managing test CA certificates for samples and tutorials

## :warning: WARNING :warning:
Certificates created by these scripts **MUST NOT** be used for production.  They contain hard-coded passwords ("1234"), expire by default after 30 days, and most importantly are provided for demonstration purposes to help you quickly understand CA Certificates.  When productizing against CA Certificates, you'll need to use your own security best practices for certification creation and lifetime management.

## Introduction
This document helps create certificates for use in **pre-testing** IoT SDKs against the IoT Hub and Edge runtime.  In particular, the tools in this directory can be used to either setup CA Certificates (along with proof of possession) or Edge device certificates.  This document assumes you have basic familiarity with the scenario you are setting up for as well as some knowledge of PowerShell or Bash.

This directory contains a PowerShell (PS1) and Bash script to help create **test** certificates for Azure IoT Hub's CA Certificate / proof-of-possession and/or Edge certificates.

The PS1 and Bash scripts are functionally equivalent; they are both provided depending on your preference for Windows or Linux respectively.

A more detailed document showing UI screen shots for CA Certificates and proof of possession flow is available from [the official documentation].

A more detailed document explaining Edge and showing its use of certificates generated here is available from the [Edge gateway creation documentation].

## USE

## Step 1 - Initial Setup
You'll need to do some initial setup prior to running these scripts.

###  **PowerShell**
* Get OpenSSL for Windows.
  * See https://docs.microsoft.com/en-us/azure/iot-edge/how-to-create-transparent-gateway#install-openssl.
* Start PowerShell as an Administrator.
* `cd` to a working directory you want to run in.  All files will be created in this directory.
* `cp *.cnf` and `cp ca-certs.ps1` from the directory this .MD file is located into your working directory.
* Run `Set-ExecutionPolicy -ExecutionPolicy Unrestricted`.  You'll need this for PowerShell to allow you to run the scripts.
* Run `. .\ca-certs.ps1` .  This is called dot-sourcing and brings the functions of the script into PowerShell's global namespace.
* Run `Test-CACertsPrerequisites` to make sure all the required pre requisites are met and follow on the on screen prompts to resolve.

###  **Bash**
* Start Bash.
* `cd` to the directory you want to run in.  All files will be created as children of this directory.
* `cp *.cnf` and `cp certGen.sh` from the directory this .MD file is located into your working directory.

## Step 2 - Create the certificate chain
First you need to create a CA and an intermediate certificate signer that chains back to the CA.

### **PowerShell**
* Run `New-CACertsCertChain [ecc|rsa]`.
  * You **must** use `rsa` if you're creating certificates for Edge.
  * `ecc` is recommended for CA certificates, but not required.

### **Bash**
* Run `./certGen.sh create_root_and_intermediate`

After the scripts have been executed certificates and keys will be generated in the following directories within the work dir.

```
<work_dir>
  +-- certs   (Contains all the public certificates in various formats and their full certificate chains)
  +-- private (Contains all the private keys in PEM format)
  +-- csr     (Contains all the public CSRs used to generate the certificates)
```

## Step 3 - Proof of Possession
*Optional - Only perform this step if you're setting up CA Certificates and proof of possession.  For simple device certificates, such as Edge certificates, skip to the next step.*

Now that you've registered your root CA with Azure IoT Hub, you'll need to prove that you actually own it.

Select the new certificate that you've created and navigate to and select  "Generate Verification Code".  This will give you a verification string you will need to place as the subject name of a certificate that you need to sign.  For our example, assume IoT Hub verification code was "106A5SD242AF512B3498BD6098C4941E66R34H268DDB3288", the certificate subject name should be that code. See below example PowerShell and Bash scripts

### **PowerShell**
* Run  `New-CACertsVerificationCert "106A5SD242AF512B3498BD6098C4941E66R34H268DDB3288"`

### **Bash**
* Run `./certGen.sh create_verification_certificate 106A5SD242AF512B3498BD6098C4941E66R34H268DDB3288`

In both cases, the scripts will output the name of the file containing `"CN=106A5SD242AF512B3498BD6098C4941E66R34H268DDB3288"` to the console.  Upload this file to IoT Hub (in the same UX that had the "Generate Verification Code") and select "Verify".

## Step 4 - Create a new device
Finally, let's create an application and corresponding device on IoT Hub that shows how CA Certificates are used.

On Azure IoT Hub, navigate to the "Device Explorer".  Add a new device (e.g. `mydevice`), and for its authentication type chose "X.509 CA Signed".  Devices can authenticate to IoT Hub using a certificate that is signed by the Root CA from Step 2.

Note that if you're using this certificate as a DPS registration ID, the ID **must be lower case** or the server will reject it.

### **PowerShell**
#### IoT Leaf Device
* Run `New-CACertsDevice mydevice` to create the new device certificate.
  * This will create files ```iot-device-mydevice*``` that each contain the public key, private key, and PFX respectively.

#### IoT Edge Device
* Run `New-CACertsEdgeDevice mydevice` to create the new IoT Edge device certificate.
  * This will create files ```iot-edge-device-mydevice*``` that each contain the public key, private key, and PFX respectively.

### **Bash**
#### IoT Leaf Device
* Run `./certGen.sh create_device_certificate mydevice` to create the new device certificate.
  * This will create files ```iot-device-mydevice*``` that each contain the public key, private key, and PFX respectively.

#### IoT Edge Device
* Run `./certGen.sh create_edge_device_certificate myEdgeDevice` to create the new IoT Edge device certificate.
  * This will create files ```iot-edge-device-mydevice*``` that each contain the public key, private key, and PFX respectively.

## Step 5 - Cleanup
These scripts output certificates to the current working directory, so there is no analogous system cleanup needed. Simply delete the contents of your work directory.

'''Note:''' On Windows machines in case you are prompted to cleanup any old certificates from the powershell script, please follow these general guidelines.
* From start menu, open `manage computer certificates` and navigate Certificates --> Local Computer -->personal.
* Remove certificates issued by "Azure IoT CA TestOnly*".
* Similarly remove them from "Trusted Root Certification Authority --> Certificates" and "Intermediate Certificate Authorities --> Certificates".


[the official documentation]: https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-security-x509-get-started
[Edge gateway creation documentation]: https://docs.microsoft.com/en-us/azure/iot-edge/how-to-create-transparent-gateway
