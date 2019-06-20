#!/bin/bash

###############################################################################
# This script simplifies the installation and creation of certs for E2E tests
###############################################################################
set -e

###############################################################################
# Script variables initialization
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
SCRIPT_NAME=$(basename $0)
CERT_SCRIPT_DIR=
ROOT_CA_CERT_PATH=
ROOT_CA_KEY_PATH=
ROOT_CA_PASSWORD=
CREATE_CERT_CN=
IS_EDGE_CA_CERT=0

# quick start and leaf device parameters
RUN_QUICKSTART=0
RUN_LEAF_X509_CA=0
RUN_LEAF_X509_THUMB=0
RUN_LEAF_SAS=0
INSTALL_CA_CERT=0
TEST_EXECUTABLE=
LEAF_DEVICE_ID=
EDGE_DEVICE_ID=""
PACKAGES=
CONNECTION_STRING=
HOSTNAME=
REGISTRY=
REGISTRY_UNAME=
REGISTRY_PW=
IMAGE_TAG=
PROTOCOL="Mqtt"
EVENTHUB_CONNECTION_STRING=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -dir, --script-dir           Certificate generation script dir"
    echo " -ic, --install-root-ca-cert  Valid path to root CA certificate file in PEM format"
    echo " -ik, --install-root-ca-key   Valid path to root CA key file in PEM format"
    echo " -pw, --root-ca-key-password  Root CA private key passowrd"
    echo " -qs, --run-quickstart        Run IoT Edge quickstart test"
    echo " -ld-x509ca, --run-leaf-device-x509-ca-auth  Run IoT leaf device X.509 auth test"
    echo " -ld-x509th, --run-leaf-device-x509-thumbprint-auth  Run IoT leaf device X.509 thumbprint auth test"
    echo " -ld-sas, --run-leaf-device-sas-auth  Run IoT leaf device SAS auth test"
    echo " -exe, --test-executable  Test executable"
    echo " -ld-id, --leaf-device-id  Device Id for a leaf device"
    echo " -a, --packages  Quick start packages"
    echo " -c, --connection-string  Quick start/leaf device connection string"
    echo " -e, --eventhub-connection-string  Event hub connection string used for verification"
    echo " -n, --edge-hostname  Edge device hostname"
    echo " -ed-id, --edge-device-id Edge device id for use in leaf device tests and for creating a Edge deployment"
    echo " -r, --registry  Edge image registry"
    echo " -u, --registry-username  Edge image registry username"
    echo " -p, --registry-password  Edge image registry password"
    echo " -t, --image-tag  Edge image tag"
    echo " -proto, --protocol  Leaf device protocol"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            CERT_SCRIPT_DIR="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            ROOT_CA_CERT_PATH="$arg"
            INSTALL_CA_CERT=1
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            ROOT_CA_KEY_PATH="$arg"
            INSTALL_CA_CERT=1
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            ROOT_CA_PASSWORD="$arg"
            INSTALL_CA_CERT=1
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            TEST_EXECUTABLE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            LEAF_DEVICE_ID="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            PACKAGES="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            CONNECTION_STRING="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 9 ]; then
            HOSTNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 10 ]; then
            REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 11 ]; then
            REGISTRY_UNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 12 ]; then
            REGISTRY_PW="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 13 ]; then
            IMAGE_TAG="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 14 ]; then
            PROTOCOL="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 15 ]; then
            EVENTHUB_CONNECTION_STRING="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 16 ]; then
            EDGE_DEVICE_ID="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-dir" | "--script-dir" ) save_next_arg=1;;
                "-ic" | "--install-root-ca-cert" ) save_next_arg=2;;
                "-ik" | "--install-root-ca-key" ) save_next_arg=3;;
                "-pw" | "--root-ca-key-password" ) save_next_arg=4;;
                "-qs" | "--run-quickstart" ) RUN_QUICKSTART=1;;
                "-ld-x509ca" | "--run-leaf-device-x509-ca-auth" ) RUN_LEAF_X509_CA=1;;
                "-ld-x509th" | "--run-leaf-device-x509-thumbprint-auth" ) RUN_LEAF_X509_THUMB=1;;
                "-ld-sas" | "--run-leaf-device-sas-auth" ) RUN_LEAF_SAS=1;;
                "-exe" | "--test-executable" ) save_next_arg=5;;
                "-ld-id" | "--leaf-device-id" ) save_next_arg=6;;
                "-a" | "--packages" ) save_next_arg=7;;
                "-c" | "--connection-string" ) save_next_arg=8;;
                "-n" | "--edge-hostname" ) save_next_arg=9;;
                "-r" | "--registry" ) save_next_arg=10;;
                "-u" | "--registry-username" ) save_next_arg=11;;
                "-p" | "--registry-password" ) save_next_arg=12;;
                "-t" | "--image-tag" ) save_next_arg=13;;
                "-proto" | "--protocol" ) save_next_arg=14;;
                "-e" | "--eventhub-connection-string" ) save_next_arg=15;;
                "-ed-id" | "--edge-device-id" ) save_next_arg=16;;
                * ) usage;;
            esac
        fi
    done

    if [ -z ${CERT_SCRIPT_DIR} ] || [ ! -d ${CERT_SCRIPT_DIR} ]; then
        echo "Certificate script dir is invalid"
        print_help_and_exit
    fi

    if [ ${RUN_QUICKSTART} -eq 1 ] ||
        [ ${RUN_LEAF_SAS} -eq 1 ] ||
        [ ${RUN_LEAF_X509_CA} -eq 1 ] ||
        [ ${RUN_LEAF_X509_THUMB} -eq 1 ]; then
        if [ -z ${TEST_EXECUTABLE} ] || [ ! -f ${TEST_EXECUTABLE} ]; then
            echo "Test executable not found, please provide a valid path to the test application"
            print_help_and_exit
        fi
    fi
}

###############################################################################
# Run the quick start test application
###############################################################################
function run_quickstart_as_gateway()
{
    local executable=${1}
    local edge_device_id=${2}
    local packages=${3}
    local connection_string=${4}
    local edge_hostname=${5}
    local registry=${6}
    local registry_uname=${7}
    local registry_pw=${8}
    local image_tag=${9}

    # generate the edge device CA certificate and key
    FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_edge_device_certificate ${edge_device_id}
    local device_ca_cert=$(readlink -f ${CERT_SCRIPT_DIR}/certs/iot-edge-device-${edge_device_id}-full-chain.cert.pem)
    local device_ca_key=$(readlink -f ${CERT_SCRIPT_DIR}/private/iot-edge-device-${edge_device_id}.key.pem)
    local trusted_ca=$(readlink -f ${CERT_SCRIPT_DIR}/certs/azure-iot-test-only.root.ca.cert.pem)

    ${executable} \
        -d "${edge_device_id}" \
        -a "${packages}" \
        -c "${connection_string}" \
        -e "${eventhub_connection_string}" \
        -n "${edge_hostname}" \
        -r "${registry}" \
        -u "${registry_uname}" \
        -p "${registry_pw}" \
        -t "${image_tag}" \
        --leave-running=Core \
        --optimize_for_performance=true \
        --no-verify \
        --device_ca_cert "${device_ca_cert}" \
        --device_ca_pk "${device_ca_key}" \
        --trusted_ca_certs "${trusted_ca}"
}

###############################################################################
# Run the leaf device SAS auth test when not setup in the edge device's scope
###############################################################################
function run_leaf_sas_auth_test_not_in_scope()
{
    local executable=${1}
    local leaf_device_id=${2}
    local connection_string=${3}
    local eventhub_connection_string=${4}
    local edge_hostname=${5}
    local protocol=${6}

    local trusted_ca=$(readlink -f ${CERT_SCRIPT_DIR}/certs/azure-iot-test-only.root.ca.cert.pem)

    ${executable} \
        -d "${leaf_device_id}" \
        -c "${connection_string}" \
        -e "${eventhub_connection_string}" \
        -ed "${edge_hostname}" \
        -proto "${protocol}" \
        -ct "${trusted_ca}"
}

###############################################################################
# Run the leaf device SAS auth test when setup in the edge device's scope
###############################################################################
function run_leaf_sas_auth_test_in_scope()
{
    local executable=${1}
    local leaf_device_id=${2}
    local connection_string=${3}
    local eventhub_connection_string=${4}
    local edge_hostname=${5}
    local edge_device_id=${6}
    local protocol=${7}

    local trusted_ca=$(readlink -f ${CERT_SCRIPT_DIR}/certs/azure-iot-test-only.root.ca.cert.pem)

    ${executable} \
        -d "${leaf_device_id}" \
        -c "${connection_string}" \
        -e "${eventhub_connection_string}" \
        -ed "${edge_hostname}" \
        -ed-id "${edge_device_id}" \
        -proto "${protocol}" \
        -ct "${trusted_ca}"
}

###############################################################################
# Run the leaf device X509 CA auth test
###############################################################################
function run_leaf_x509_ca_test()
{
    local executable=${1}
    local leaf_device_id=${2}
    local connection_string=${3}
    local eventhub_connection_string=${4}
    local edge_hostname=${5}
    local edge_device_id=${6}
    local protocol=${7}

    # generate the edge device CA certificate and key
    FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_device_certificate ${leaf_device_id}
    local device_cert=$(readlink -f ${CERT_SCRIPT_DIR}/certs/iot-device-${leaf_device_id}-full-chain.cert.pem)
    local device_key=$(readlink -f ${CERT_SCRIPT_DIR}/private/iot-device-${leaf_device_id}.key.pem)
    local trusted_ca=$(readlink -f ${CERT_SCRIPT_DIR}/certs/azure-iot-test-only.root.ca.cert.pem)

    ${executable} \
        -d "${leaf_device_id}" \
        -c "${connection_string}" \
        -e "${eventhub_connection_string}" \
        -ed "${edge_hostname}" \
        -ed-id "${edge_device_id}" \
        -proto "${protocol}" \
        -ct "${trusted_ca}" \
        -cac "${device_cert}" \
        -cak "${device_key}"
}

###############################################################################
# Run the leaf device X509 thumbprint auth test
###############################################################################
function run_leaf_x509_thumbprint_test()
{
    local executable=${1}
    local leaf_device_id=${2}
    local connection_string=${3}
    local eventhub_connection_string=${4}
    local edge_hostname=${5}
    local edge_device_id=${6}
    local protocol=${7}

    # generate the device primary certificate and key
    FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_device_certificate "${leaf_device_id}-pri"
    # generate the device primary certificate and key
    FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_device_certificate "${leaf_device_id}-sec"

    local pri_device_cert=$(readlink -f ${CERT_SCRIPT_DIR}/certs/iot-device-${leaf_device_id}-pri-full-chain.cert.pem)
    local pri_device_key=$(readlink -f ${CERT_SCRIPT_DIR}/private/iot-device-${leaf_device_id}-pri.key.pem)
    local sec_device_cert=$(readlink -f ${CERT_SCRIPT_DIR}/certs/iot-device-${leaf_device_id}-sec-full-chain.cert.pem)
    local sec_device_key=$(readlink -f ${CERT_SCRIPT_DIR}/private/iot-device-${leaf_device_id}-sec.key.pem)
    local trusted_ca=$(readlink -f ${CERT_SCRIPT_DIR}/certs/azure-iot-test-only.root.ca.cert.pem)

    ${executable} \
        -d "${leaf_device_id}" \
        -c "${connection_string}" \
        -e "${eventhub_connection_string}" \
        -ed "${edge_hostname}" \
        -ed-id "${edge_device_id}" \
        -proto "${protocol}" \
        -ct "${trusted_ca}" \
        -ctpc "${pri_device_cert}" \
        -ctpk "${pri_device_key}" \
        -ctsc "${sec_device_cert}" \
        -ctsk "${sec_device_key}"
}

###############################################################################
# Main script entry
###############################################################################

process_args "$@"

if [ ${INSTALL_CA_CERT} -eq 1 ]; then
    FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh install_root_ca_from_files ${ROOT_CA_CERT_PATH} ${ROOT_CA_KEY_PATH} ${ROOT_CA_PASSWORD}
else
    if [ ${RUN_QUICKSTART} -eq 1 ]; then
        run_quickstart_as_gateway \
            ${TEST_EXECUTABLE} \
            ${EDGE_DEVICE_ID} \
            ${PACKAGES} \
            ${CONNECTION_STRING} \
            ${HOSTNAME} \
            ${REGISTRY} \
            ${REGISTRY_UNAME} \
            ${REGISTRY_PW} \
            ${IMAGE_TAG}
    elif [ ${RUN_LEAF_X509_CA} -eq 1 ]; then
        run_leaf_x509_ca_test \
            ${TEST_EXECUTABLE} \
            ${LEAF_DEVICE_ID} \
            ${CONNECTION_STRING} \
            ${EVENTHUB_CONNECTION_STRING} \
            ${HOSTNAME} \
            ${EDGE_DEVICE_ID} \
            ${PROTOCOL}
    elif [ ${RUN_LEAF_X509_THUMB} -eq 1 ]; then
        run_leaf_x509_thumbprint_test \
            ${TEST_EXECUTABLE} \
            ${LEAF_DEVICE_ID} \
            ${CONNECTION_STRING} \
            ${EVENTHUB_CONNECTION_STRING} \
            ${HOSTNAME} \
            ${EDGE_DEVICE_ID} \
            ${PROTOCOL}
    elif [ ${RUN_LEAF_SAS} -eq 1 ]; then
        if [[ ! -z ${EDGE_DEVICE_ID} ]]; then
            run_leaf_sas_auth_test_in_scope \
                ${TEST_EXECUTABLE} \
                ${LEAF_DEVICE_ID} \
                ${CONNECTION_STRING} \
                ${EVENTHUB_CONNECTION_STRING} \
                ${HOSTNAME} \
                ${EDGE_DEVICE_ID} \
                ${PROTOCOL}
        else
            run_leaf_sas_auth_test_not_in_scope \
                ${TEST_EXECUTABLE} \
                ${LEAF_DEVICE_ID} \
                ${CONNECTION_STRING} \
                ${EVENTHUB_CONNECTION_STRING} \
                ${HOSTNAME} \
                ${PROTOCOL}
        fi
    else
        echo "Invalid command line arguments supplied"
        print_help_and_exit
    fi
fi
