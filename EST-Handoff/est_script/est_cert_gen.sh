#!/bin/bash

## Copyright (c) Microsoft. All rights reserved.
## Licensed under the MIT license. See LICENSE file in the project root for full license information.

###############################################################################
#  Checks for all pre reqs before executing this script
###############################################################################
function check_prerequisites()
{
    local exists=$(command -v -- openssl)
    if [ -z "$exists" ]; then
        echo "openssl is required to run this script, please install this before proceeding"
        exit 1
    fi

    local exists=$(command -v -- curl)
    if [ -z "$exists" ]; then
        echo "curl is required to run this script, please install this before proceeding"
        exit 1
    fi

    local exists=$(command -v -- wget)
    if [ -z "$exists" ]; then
        echo "wget is required to run this script, please install this before proceeding"
        exit 1
    fi

    echo "Pre-Requisites installed. Continuing."
}

function help_docs()
{
    echo "Greetings! This script generates two certificates (one Device CA and one Device ID) and submits either (1) or (2) CSR requests to the EST server"
    echo ""
    echo "Usage for two CSR (default):"
    echo "    ./est_cert_gen.sh -ServerBaseURL https://contoso.com:8443 -ServerUser username -ServerPass password -CertOutDir /var/foo/bar"
    echo ""
    echo "Usage for one CSR (Device ID will be chained to Device CA):"
    echo "    ./est_cert_gen.sh -ServerBaseURL https://contoso.com:8443 -ServerUser username -ServerPass password -CertOutDir /var/foo/bar -ChainIDToDeviceCA"
    exit 0
}

# Generate a CSR with certificate based auth
# generate_csr <cert base name> <auth mode string>
function generate_csr_cert_auth()
{
    openssl genrsa -out rsakey.pem 2048

    openssl req -new -key rsakey.pem -config $(pwd)/cert.conf -out req.p10

    curl ${EST_Server_Base}/.well-known/est/simpleenroll --cert ${2} -s -o cert.p7 --cacert ${CA_CERT} --data-binary @req.p10 -H "Content-Type: application/pkcs10" --dump-header resp.hdr

    if grep -q 404 resp.hdr; then
        echo "The EST endpoint call resulted in a 404. Check the EST endpoint."
        rm rsakey.pem
        rm req.p10
        rm cert.p7
        rm resp.hdr
        exit 1
    fi

    if grep -q 403 resp.hdr; then
        echo "The EST endpoint call resulted in a 403. Check the EST credentials."
        rm rsakey.pem
        rm req.p10
        rm cert.p7
        rm resp.hdr
        exit 1
    fi

    openssl base64 -d -in cert.p7 | openssl pkcs7 -inform DER -outform PEM -print_certs -out cert.pem

    # Move the private key with the provided name
    mv rsakey.pem ${Cer_Out_Dir}/${1}_key.pem

    # Move the signed Cert with the provided name
    mv cert.pem ${Cer_Out_Dir}/${1}.pem

    rm req.p10
    rm cert.p7
    rm resp.hdr
    rm cert.conf
}

# Generate a CSR with basic auth (this is the only mode supported by cisco's server)
# generate_csr <cert base name> <auth mode string>
function generate_csr()
{
    openssl genrsa -out rsakey.pem 2048

    openssl req -new -key rsakey.pem -config $(pwd)/cert.conf -out req.p10

    curl ${EST_Server_Base}/.well-known/est/simpleenroll --anyauth -u ${EST_Server_User}:${EST_Server_Pass} -s -o cert.p7 --cacert ${CA_CERT} --data-binary @req.p10 -H "Content-Type: application/pkcs10" --dump-header resp.hdr

    if grep -q 404 resp.hdr; then
        echo "The EST endpoint call resulted in a 404. Check the EST endpoint."
        rm rsakey.pem
        rm req.p10
        rm cert.p7
        rm resp.hdr
        exit 1
    fi

    if grep -q 403 resp.hdr; then
        echo "The EST endpoint call resulted in a 403. Check the EST credentials."
        rm rsakey.pem
        rm req.p10
        rm cert.p7
        rm resp.hdr
        exit 1
    fi

    openssl base64 -d -in cert.p7 | openssl pkcs7 -inform DER -outform PEM -print_certs -out cert.pem

    # Move the private key with the provided name
    mv rsakey.pem ${Cer_Out_Dir}/${1}_key.pem

    # Move the signed Cert with the provided name
    mv cert.pem ${Cer_Out_Dir}/${1}.pem

    rm req.p10
    rm cert.p7
    rm resp.hdr
    rm cert.conf
}

# If requested, sign the Device ID from the Device CA
function self_sign_device_id() 
{
    openssl genrsa -out rsakey.pem 2048

    openssl req -new -key rsakey.pem -config $(pwd)/cert.conf -out req.p10

    openssl x509 -req -in req.p10 -CA ${Cer_Out_Dir}/est_device_ca.pem -CAkey ${Cer_Out_Dir}/est_device_ca_key.pem -CAcreateserial -out deviceid.p7 -days 365 -sha256

    openssl base64 -d -in deviceid.p7 | openssl pkcs7 -inform DER -outform PEM -print_certs -out deviceid.pem

    # Move the private key with the provided name
    mv rsakey.pem ${Cer_Out_Dir}/deviceid_key.pem

    # Move the signed Cert with the provided name
    mv deviceid.pem ${Cer_Out_Dir}/deviceid.pem

    rm rsakey.pem
    rm req.p10
    rm deviceid.p7
    rm cert.conf
}

function create_conf()
{
    echo "[ req ]" >> cert.conf
    echo "default_bits = 2048" >> cert.conf
    echo "encrypt_key = no" >> cert.conf
    echo "default_md = sha1" >> cert.conf
    echo "prompt = no" >> cert.conf
    echo "utf8 = yes" >> cert.conf
    echo "distinguished_name = my_req_distinguished_name" >> cert.conf

    if [ "${1}" == "ca" ]; then
        echo "req_extensions = my_extensions" >> cert.conf
    fi

    echo "[ my_req_distinguished_name ]" >> cert.conf
    echo "C = US" >> cert.conf
    echo "ST = WA" >> cert.conf
    echo "ST = Some-State" >> cert.conf
    echo "L = ." >> cert.conf
    echo "O  = ." >> cert.conf
    echo "CN = ." >> cert.conf
    

    if [ "${1}" == "ca" ]; then
        echo "[ my_extensions ]" >> cert.conf
        echo "basicConstraints=CA:TRUE" >> cert.conf
    fi
}

if [ "$#" -lt 8 ]; then
    help_docs
fi
if [ "${1}" != "-ServerBaseURL" ]; then
    echo "Invalid Input Format"
    help_docs
fi
if [ "${3}" != "-ServerUser" ]; then
    echo "Invalid Input Format"
    help_docs
fi
if [ "${5}" != "-ServerPass" ]; then
    echo "Invalid Input Format"
    help_docs
fi
if [ "${7}" != "-CertOutDir" ]; then
    echo "Invalid Input Format"
    help_docs
fi

EST_Server_Base="${2}"
EST_Server_User="${4}"
EST_Server_Pass="${6}"
Cer_Out_Dir="${8}"
CHAIN_ID=false

CA_CERT=./server_tls_ca.pem

if [ ! -f "$CA_CERT" ]; then
    echo "$CA_CERT does not exist. Please acquire the TLS CA cert for the EST server and place here."
    exit 1
fi


# Check to ensure that the needed pre-reqs have been installed.
check_prerequisites

if [ "$#" -eq 9 ]; then
    if [ "${9}" == "-ChainIDToDeviceCA" ]; then
        echo "Device ID will be chained to device CA"
        CHAIN_ID=true
    fi
fi

if [ "$#" -eq 8 ]; then
    echo "Device ID will be signed via CSR"
fi

create_conf ca
generate_csr est_device_ca

if [ "$CHAIN_ID" = false ] ; then
    # Sign Device ID via CSR
    create_conf
    generate_csr est_device_id
fi

if [ "$CHAIN_ID" = true ] ; then
    # Sign Device ID via CSR
    create_conf
    self_sign_device_id
fi

echo "The Device CA and Device ID certs have been generated. They are located: ${Cer_Out_Dir}. Please update Edge's Config.yaml accordingly."

exit 0