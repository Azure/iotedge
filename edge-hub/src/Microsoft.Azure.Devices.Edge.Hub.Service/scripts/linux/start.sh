#!/bin/bash

# This scrips starts the IoT hub service

function logtime
{
    echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]") $*"
    return $?
}

EdgeHubUser=$(printenv EdgeHubUser)
EdgeModuleHubServerCertificateFile=$(printenv EdgeModuleHubServerCertificateFile)
EdgeModuleHubServerCAChainCertificateFile=$(printenv EdgeModuleHubServerCAChainCertificateFile)
EDGEHUB_CA_INSTALLED_FILE="/app/ca_certs_installed.json"

# check if the EdgeAgent supplied the hub with the server certificate and its
# corresponding signing CA cert
if [[ -z "${EdgeModuleHubServerCertificateFile}" ]] || [[ -z "${EdgeModuleHubServerCAChainCertificateFile}" ]]; then
    # certs not provided so generate SSL self signed certificate
	chmod +x ./scripts/linux/generate-cert.sh
    ./scripts/linux/generate-cert.sh
else
    logtime "Edge Hub Server Certificate File: ${EdgeModuleHubServerCertificateFile}"
    logtime "Edge Hub CA Server Certificate File: ${EdgeModuleHubServerCAChainCertificateFile}"

    SSL_CERTIFICATE_PATH=$(dirname "${EdgeModuleHubServerCertificateFile}")
    export SSL_CERTIFICATE_PATH

    SSL_CERTIFICATE_NAME=$(basename "${EdgeModuleHubServerCertificateFile}")
    export SSL_CERTIFICATE_NAME

    logtime "SSL_CERTIFICATE_PATH=${SSL_CERTIFICATE_PATH}"
    logtime "SSL_CERTIFICATE_NAME=${SSL_CERTIFICATE_NAME}"

    # install chain cert only for the first boot
    if [ ! -f ${EDGEHUB_CA_INSTALLED_FILE} ]; then
        # copy the CA cert into the ca cert dir
        command="cp ${EdgeModuleHubServerCAChainCertificateFile} /usr/local/share/ca-certificates/edge-chain-ca.crt"
        logtime "Executing: ${command}"
        if ! $command; then
            logtime "Failed to Copy Edge Chain CA Certificate."
            exit 1
        fi
        # register the newly added CA cert
        command="update-ca-certificates"
        logtime "Executing: ${command}"
        if ! $command; then
            logtime "Failed to Update CA Certificates."
            exit 1
        fi
        echo " { \"installed_certs\": \"true\" } " > ${EDGEHUB_CA_INSTALLED_FILE}
        logtime "Certificates installed successfully!"
    else
        logtime "Certificates already installed, skipping install!"
    fi
fi

# start service
command="exec runuser -u ${EdgeHubUser} dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll"
logtime "Starting Edge Hub: ${command}"
if ! $command; then
    logtime "Failed to start Edge Hub"
    exit 1
fi
