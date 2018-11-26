#!/bin/bash

set -eo pipefail

usage()
{
    echo "Missing arguments. Usage: $0 -v <keyVault name> -c <Cert name>"
    exit 1;
}

while getopts ":c:v:s:" o; do
    case "${o}" in
        c)
            CERT_NAME=${OPTARG}
            ;;
        v)
            KEYVAULT_NAME=${OPTARG}
            ;;
        s)
            STORE_NAME=${OPTARG}
            ;;
        *)
            usage
            ;;
    esac
done
shift $((OPTIND-1))

if [ -z "${CERT_NAME}" ] || [ -z "${KEYVAULT_NAME}" ]; then
    usage
fi

if [[ -z ${STORE_NAME} ]]; then
        STORE_NAME="My"
fi

BASEDIR=$(dirname "$0")

# Download the Cert
echo Downloading cert from KeyVault
keyVaultCertSecret="$(az keyvault secret show --name $CERT_NAME --vault-name $KEYVAULT_NAME)"
keyVaultCert="$(echo $keyVaultCertSecret | jq -r '.value')"
echo Done downloading cert from KeyVault

# Install the Cert
echo Installing Cert
pwsh -Command "$BASEDIR/InstallCert.ps1 -CertificateValue $keyVaultCert" -StoreName $STORE_NAME
echo Done installing Cert.

exit 0
