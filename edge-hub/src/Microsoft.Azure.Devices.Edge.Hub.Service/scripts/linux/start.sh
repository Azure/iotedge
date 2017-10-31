#!/bin/bash

# This scrips starts the IoT hub service

EdgeModuleHubServerCertificateFile=$(printenv EdgeModuleHubServerCertificateFile)
EdgeModuleHubServerCAChainCertificateFile=$(printenv EdgeModuleHubServerCAChainCertificateFile)
# check if the EdgeAgent supplied the hub with the server certificate and its
# corresponding signing CA cert
if [[ -z "${EdgeModuleHubServerCertificateFile}" ]] || [[ -z "${EdgeModuleHubServerCAChainCertificateFile}" ]]; then
    # certs not provided so generate SSL self signed certificate
    ./scripts/linux/generate-cert.sh
else
    echo "Edge Hub Server Certificate File: ${EdgeModuleHubServerCertificateFile}"
    echo "Edge Hub CA Server Certificate File: ${EdgeModuleHubServerCAChainCertificateFile}"
    export SSL_CERTIFICATE_PATH=$(dirname "${EdgeModuleHubServerCertificateFile}")
    export SSL_CERTIFICATE_NAME=$(basename "${EdgeModuleHubServerCertificateFile}")
    echo "SSL_CERTIFICATE_PATH=${SSL_CERTIFICATE_PATH}"
    echo "SSL_CERTIFICATE_NAME=${SSL_CERTIFICATE_NAME}"
    # copy the CA cert into the ca cert dir
    command="cp ${EdgeModuleHubServerCAChainCertificateFile} /usr/local/share/ca-certificates/edge-chain-ca.crt"
    echo "Executing: ${command}"
    ${command}
    if [ $? -ne 0 ]; then
        echo "Failed to Copy Edge Chain CA Certificate."
        exit 1
    fi
    # register the newly added CA cert
    command="update-ca-certificates"
    echo "Executing: ${command}"
    ${command}
    if [ $? -ne 0 ]; then
        echo "Failed to Update CA Certificates."
        exit 1
    fi
    echo "Certificates generated and installed successfully!"
fi

# start service
exec dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
