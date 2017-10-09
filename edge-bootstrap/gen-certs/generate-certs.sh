#! /bin/bash

###############################################################################
# This script generates the required X.509 certificates needed to bootstrap
# an Azure Edge runtime.
#
# Run this script with --help to learn more.
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
SCRIPT_DIR=$(dirname $0)
SCRIPT_DIR=$(readlink -f ${SCRIPT_DIR})

# directories for cert generation
EDGE_CERTS_HOME_DIR=
EDGE_OPENSSL_CONF_DIR=
EDGE_CERTS_OUTPUT_DIR=
EDGE_CERTS_OUTPUT_DIR_NAME="output"
EDGE_ROOT_CA_DIR=
EDGE_INTERMEDIATE_CA_DIR=

# root and intermediate certificate config
EDGE_ROOT_CA_PRIVATE_KEY_PASSWD=
EDGE_ROOT_CA_CERT_PREFIX="azure-iot-edge.root.ca"
EDGE_ROOT_CA_COMMON_NAME=
EDGE_ROOT_CA_CERT_CONF="openssl_root_ca.cnf"
EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD=
EDGE_INTERMEDIATE_CA_CERT_PREFIX="azure-iot-edge.device.ca"
EDGE_DEVICE_CA_COMMON_NAME=
EDGE_INTERMEDIATE_CA_CONF="openssl_device_intermediate_ca.cnf"
EDGE_CHAIN_CA_CERT_PREFIX="azure-iot-edge.chain.ca"
EDGE_HUB_SERVER_CERT_PREFIX="azure-iot-edge.hub.server"

# inputs required for certificate creation
EDGE_DEFAULT_ROOT_CA_COMMON_NAME="Default Edge Root CA Name"
EDGE_DEFAULT_DEVICE_CA_COMMON_NAME="Default Edge Device CA Name"
EDGE_DEFAULT_COUNTRY=US
EDGE_DEFAULT_STATE=Washington
EDGE_DEFAULT_LOCALITY=Redmond
EDGE_DEFAULT_ORGANIZATION_NAME="Default Edge Organization"
EDGE_HUB_SERVER_DNS_NAME=
COUNTRY=
STATE=
LOCALITY=
ORGANIZATION_NAME=
PFX_PASSWD=

# script control variables
EDGE_USE_DEFAULTS=OFF
EDGE_USE_PK_PASSWORDS=ON

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -d,  --edge-certs-dir        Directory where the certificates" \
         "will be generated. Required."
    echo " -dns, --edge-hub-dns         DNS Name of the Edge Runtime Device."
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " -C,  --country               Certificate Issuing Country." \
         "Two Character Country Code (ex. US)."
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " -ST, --state                 Certificate Issuing State."
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " -L,  --locality              Certificate Issuing Locality" \
         "(City)."
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " -OR, --organization          Certificate Issuing Organization Name"
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " --root-ca-common-name        Root CA Certificate Common Name."
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " --device-ca-common-name      Intermediate Device CA Certificate"
    echo "                              If not provided, users will be" \
         "prompted to enter a value."
    echo " --root-ca-password           Root CA private key password."
    echo "                              Password length must be between" \
         "4 - 1023 characters."
    echo "                              If not provided, users will be" \
         "prompted to enter a value unless"
    echo "                              --force-no-passwords is specified."
    echo " --device-ca-password         Device CA private key password."
    echo "                              Password length must be between" \
         "4 - 1023 characters."
    echo "                              If not provided, users will be" \
         "prompted to enter a value unless"
    echo "                              --force-no-passwords is specified."
    echo " --force-no-passwords         Do not use passwords for private keys."
    echo " --no-prompt                  Use defaults for certificate data."
    echo "                              Note: This script does not assume" \
         "any default passwords."
    echo "                              Users should use --force-no-passwords" \
         "for a prompt free experience."
    exit 1;
}

function print_help_and_exit()
{
    echo "Run ${SCRIPT_NAME} --help for more information."
    exit 1
}

###############################################################################
# Helper function to validate if a password is valid or not
# Input:
#   password to be checked
# Ouput
#   0 -- success
#   1 -- otherwise
###############################################################################
function validate_private_key_password()
{
    local password="${1}"
    local result=
    if  [[ -z "${password}" ]] ||
        [[ ${#password} -lt 4 ]] ||
        [[ ${#password} -gt 1023 ]]; then
        result=1
    else
        result=0
    fi

    echo ${result}
}

###############################################################################
# Obtain and validate the command line options supported by this script
###############################################################################
function process_args()
{
    local save_next_arg=0
    echo "Generate Certs Command: $@"
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            EDGE_CERTS_HOME_DIR="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            EDGE_HUB_SERVER_DNS_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            COUNTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            STATE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            LOCALITY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            ORGANIZATION_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            EDGE_ROOT_CA_COMMON_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            EDGE_DEVICE_CA_COMMON_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 9 ]; then
            EDGE_ROOT_CA_PRIVATE_KEY_PASSWD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 10 ]; then
            EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h"   | "--help" ) usage;;
                "-d"   | "--edge-certs-dir" ) save_next_arg=1;;
                "-dns" | "--edge-hub-dns" ) save_next_arg=2;;
                "-C"   | "--country" ) save_next_arg=3;;
                "-ST"  | "--state" ) save_next_arg=4;;
                "-L"   | "--locality" ) save_next_arg=5;;
                "-OR"  | "--organization" ) save_next_arg=6;;
                "--root-ca-common-name" ) save_next_arg=7;;
                "--device-ca-common-name" ) save_next_arg=8;;
                "--root-ca-password"  ) save_next_arg=9;;
                "--device-ca-password"  ) save_next_arg=10;;
                "--force-no-passwords" ) EDGE_USE_PK_PASSWORDS=OFF;;
                "--no-prompt" ) EDGE_USE_DEFAULTS=ON;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "${EDGE_CERTS_HOME_DIR}" ]]; then
        echo "Edge Certs Directory Cannot Be Empty."
        print_help_and_exit
    else
        EDGE_CERTS_HOME_DIR=$(readlink -f ${EDGE_CERTS_HOME_DIR})
        if [[ ! -d "${EDGE_CERTS_HOME_DIR}" ]]; then
            echo "Edge Certs Directory Is Invalid."
            print_help_and_exit
        fi
    fi

    if [[ $EDGE_USE_DEFAULTS == ON ]]; then
        if [[ -z "${EDGE_HUB_SERVER_DNS_NAME}" ]]; then
            echo "DNS Name should be provided if choosing --no-prompt."
            print_help_and_exit
        fi
        if [[ -z "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}" ]] &&
           [[ $EDGE_USE_PK_PASSWORDS == ON ]]; then
            echo "Root CA private key password should be provided if" \
                 "choosing --no-prompt. Optionally, use --force-no-passwords" \
                 "when using --no-prompt."
            print_help_and_exit
        fi
        if [[ -z "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}" ]] &&
           [[ $EDGE_USE_PK_PASSWORDS == ON ]]; then
            echo "Device CA private key password should be provided if" \
                 "choosing --no-prompt. Optionally, use --force-no-passwords" \
                 "when using --no-prompt."
            print_help_and_exit
        fi
    fi

    if [[ ! -z "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}" ]]; then
        local result=$(validate_private_key_password \
                        "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}")
        if [[ ${result} -ne 0 ]]; then
            echo "Supplied Root CA password is invalid."
            print_help_and_exit
        fi
    fi

    if [[ ! -z "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}" ]]; then
        local result=$(validate_private_key_password \
                        "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}")
        if [[ ${result} -ne 0 ]]; then
            echo "Supplied Device CA password is invalid."
            print_help_and_exit
        fi
    fi
}

###############################################################################
# Functions to interactively obtain data from users to generate certificates
###############################################################################
function request_user_input_for_edge_hub_server_cert()
{
    if [[ -z "${EDGE_HUB_SERVER_DNS_NAME}" ]]; then
        local loop_done=0
        echo ""
        local host_name=$(hostname -A | cut -d' ' -f1)
        while [[ $loop_done -eq 0 ]]; do
            echo "Fully qualified DNS Name (FQDN) of the Edge Device" \
                 "(ex. myserver.mydomain.com). [${host_name}]:"
            read arg
            [[ ! -z "${arg}" ]] || arg="${host_name}"
            if [[ -z "${arg}" ]]; then
                echo ""
                echo "Please Enter a valid DNS Name (FQDN)."
            else
                EDGE_HUB_SERVER_DNS_NAME="${arg}"
                loop_done=1
            fi
        done
    fi
}

function request_user_input_for_self_signed_certs()
{
    echo ">>> Your Input is Required To Generate Certificates for the Azure " \
         "Edge Runtime. Please press ENTER if [defaults] are acceptable."

    if [[ -z "${COUNTRY}" ]] || [[ ${#COUNTRY} -ne 2 ]]; then
        echo ""
        echo "Input Two Letter Country Code. [${EDGE_DEFAULT_COUNTRY}]:"
        read -n 2 arg
        if [[ -z "${arg}" ]]; then
            COUNTRY=$EDGE_DEFAULT_COUNTRY
        else
            COUNTRY="${arg}"
        fi
    fi

    if [[ -z "${STATE}" ]]; then
        echo ""
        echo "Input State or Province Name. [${EDGE_DEFAULT_STATE}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            STATE=$EDGE_DEFAULT_STATE
        else
            STATE="${arg}"
        fi
    fi

    if [[ -z "${LOCALITY}" ]]; then
        echo ""
        echo "Input Locality Name (ex. city). [${EDGE_DEFAULT_LOCALITY}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            LOCALITY=$EDGE_DEFAULT_LOCALITY
        else
            LOCALITY="${arg}"
        fi
    fi

    if [[ -z "${ORGANIZATION_NAME}" ]]; then
        echo ""
        echo "Organization Name (ex.company)." \
             "[${EDGE_DEFAULT_ORGANIZATION_NAME}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            ORGANIZATION_NAME=$EDGE_DEFAULT_ORGANIZATION_NAME
        else
            ORGANIZATION_NAME="${arg}"
        fi
    fi

    if [[ -z "${LOCALITY}" ]]; then
        echo ""
        echo "Input Locality Name (ex. city). [${EDGE_DEFAULT_LOCALITY}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            LOCALITY=$EDGE_DEFAULT_LOCALITY
        else
            LOCALITY="${arg}"
        fi
    fi

    if [[ -z "${EDGE_ROOT_CA_COMMON_NAME}" ]]; then
        echo ""
        echo "Input Root CA Certificate Name." \
             "[${EDGE_DEFAULT_ROOT_CA_COMMON_NAME}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            EDGE_ROOT_CA_COMMON_NAME=$EDGE_DEFAULT_ROOT_CA_COMMON_NAME
        else
            EDGE_ROOT_CA_COMMON_NAME="${arg}"
        fi
    fi

    if [[ $EDGE_USE_PK_PASSWORDS == ON ]] &&
       [[ -z "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}" ]]; then
        local loop_done=0
        echo ""
        while [[ $loop_done -eq 0 ]]; do
            echo "Input Private Key Password for Root CA." \
                 "Passwords can be 4 - 1023 characters long."
            read -s EDGE_ROOT_CA_PRIVATE_KEY_PASSWD
            local result=$(validate_private_key_password \
                            "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}")
            if [[ ${result} -ne 0 ]]; then
                echo "Supplied Password Cannot Be Used. Please try again."
            else
                loop_done=1
            fi
        done
        echo ""
    fi

    if [[ -z "${EDGE_DEVICE_CA_COMMON_NAME}" ]]; then
        echo ""
        echo "Input Device CA Certificate Name." \
             "[${EDGE_DEFAULT_DEVICE_CA_COMMON_NAME}]:"
        read arg
        if [[ -z "${arg}" ]]; then
            EDGE_DEVICE_CA_COMMON_NAME=$EDGE_DEFAULT_DEVICE_CA_COMMON_NAME
        else
            EDGE_DEVICE_CA_COMMON_NAME="${arg}"
        fi
    fi

    if [[ $EDGE_USE_PK_PASSWORDS == ON ]] &&
       [[ -z "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}" ]]; then
        local loop_done=0
        echo ""
        while [[ $loop_done -eq 0 ]]; do
            echo "Input Private Key Password for (Intermediate) Device CA." \
                 "Passwords can be 4 - 1023 characters long."
            read -s EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD
            local result=$(validate_private_key_password \
                            "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}")
            if [[ ${result} -ne 0 ]]; then
                echo "Supplied Password Cannot Be Used. Please try again."
            else
                loop_done=1
            fi
        done
        echo ""
    fi

    request_user_input_for_edge_hub_server_cert
}

function request_user_input()
{
    echo ""
    echo "*********************************************************************"
    echo "Generating Edge Certificates In Directory:"
    echo " ${EDGE_CERTS_HOME_DIR}"
    echo "*********************************************************************"
    request_user_input_for_self_signed_certs
    request_user_input_for_edge_hub_server_cert
}

###############################################################################
# Prepare file system structure to generate required certs
###############################################################################
function prepare_filesystem()
{
    echo "Preparing Workspace To Generate Edge Certificates"
    EDGE_OPENSSL_CONF_DIR="${EDGE_CERTS_HOME_DIR}/config"
    EDGE_ROOT_CA_BASE_DIR="${EDGE_CERTS_HOME_DIR}/root"
    EDGE_ROOT_CA_DIR="${EDGE_ROOT_CA_BASE_DIR}/ca"
    EDGE_INTERMEDIATE_CA_BASE_DIR="${EDGE_CERTS_HOME_DIR}/device"
    EDGE_INTERMEDIATE_CA_DIR="${EDGE_INTERMEDIATE_CA_BASE_DIR}/ca"
    EDGE_CERTS_OUTPUT_DIR="${EDGE_CERTS_HOME_DIR}/${EDGE_CERTS_OUTPUT_DIR_NAME}"

    cd "${EDGE_CERTS_HOME_DIR}"
    rm -fr "${EDGE_OPENSSL_CONF_DIR}"
    rm -fr "${EDGE_ROOT_CA_BASE_DIR}"
    rm -fr "${EDGE_INTERMEDIATE_CA_BASE_DIR}"
    mkdir "${EDGE_OPENSSL_CONF_DIR}"
    mkdir "${EDGE_ROOT_CA_BASE_DIR}"
    mkdir "${EDGE_ROOT_CA_DIR}"
    mkdir "${EDGE_INTERMEDIATE_CA_BASE_DIR}"
    mkdir "${EDGE_INTERMEDIATE_CA_DIR}"
    mkdir -p "${EDGE_CERTS_OUTPUT_DIR_NAME}"
    EDGE_CERTS_OUTPUT_DIR=$(readlink -f ${EDGE_CERTS_OUTPUT_DIR})


    cd "${EDGE_ROOT_CA_DIR}"
    mkdir certs crl newcerts private
    chmod 700 private
    touch index.txt
    echo 1000 > serial
    cp ${SCRIPT_DIR}/${EDGE_ROOT_CA_CERT_CONF} ${EDGE_OPENSSL_CONF_DIR}

    cd "${EDGE_INTERMEDIATE_CA_DIR}"
    mkdir certs crl csr newcerts private
    chmod 700 private
    touch index.txt
    echo 1000 > serial
    echo 1000 > crlnumber
    cp ${SCRIPT_DIR}/${EDGE_INTERMEDIATE_CA_CONF} ${EDGE_OPENSSL_CONF_DIR}
}

###############################################################################
# Generate Root CA Edge Cert
###############################################################################
function generate_root_ca()
{
    local home_dir="${1}"
    local root_ca_dir="${2}"
    local root_ca_prefix="${3}"
    local openssl_config_file="${4}"
    local common_name="${5}"
    local algorithm="${6}"
    local key_bits_length="${7}"
    local root_ca_password="${8}"
    local password_cmd=

    if [ "${algorithm}" == "rsa" ]; then
        algorithm="genrsa"
    else
        echo "Unknown Algorithm ${algorithm} Specified"
        exit 1
    fi

    echo "Creating the Root CA"
    echo "--------------------"

    cd ${home_dir}
    echo "Creating the Root CA Private Key"

    password_cmd=
    if [[ ! -z $root_ca_password ]]; then
        password_cmd=" -aes256 -passout pass:${root_ca_password} "
    fi
    openssl ${algorithm} \
            ${password_cmd} \
            -out ${root_ca_dir}/private/${root_ca_prefix}.key.pem \
            ${key_bits_length}
    [ $? -eq 0 ] || exit $?
    chmod 400 ${root_ca_dir}/private/${root_ca_prefix}.key.pem
    [ $? -eq 0 ] || exit $?

    echo "Creating the Root CA Certificate"
    password_cmd=
    if [[ ! -z $root_ca_password ]]; then
        password_cmd=" -passin pass:${root_ca_password} "
    fi
    openssl req \
            -new \
            -x509 \
            -config ${openssl_config_file} \
            ${password_cmd} \
            -key ${root_ca_dir}/private/${root_ca_prefix}.key.pem \
            -subj "/C=${COUNTRY}/ST=${STATE}/L=${LOCALITY}/CN=${common_name}/O=${ORGANIZATION_NAME}" \
            -days 7300 \
            -sha256 \
            -extensions v3_ca \
            -out ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?
    chmod 444 ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "CA Root Certificate Generated At:"
    echo "---------------------------------"
    echo "    ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem"
    echo ""
    openssl x509 -noout -text \
            -in ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?
}

###############################################################################
# Generate Intermediate (Device) CA Edge Cert
###############################################################################
function generate_device_ca()
{
    local home_dir="${1}"
    local root_ca_dir="${2}"
    local intermediate_ca_dir="${3}"
    local root_ca_prefix="${4}"
    local intermediate_ca_prefix="${5}"
    local ca_chain_prefix="${6}"
    local openssl_root_ca_config_file="${7}"
    local openssl_intermediate_config_file="${8}"
    local common_name="${9}"
    local algorithm="${10}"
    local key_bits_length="${11}"
    local root_ca_password="${12}"
    local intermediate_ca_password="${13}"
    local password_cmd=

    if [ "${algorithm}" == "rsa" ]; then
        algorithm="genrsa"
    else
        echo "Unknown Algorithm ${algorithm} Specified"
        exit 1
    fi

    echo "Creating the Intermediate Device CA"
    echo "-----------------------------------"
    cd ${home_dir}

    echo "Creating the Intermediate Device CA Key"
    password_cmd=
    if [[ ! -z $intermediate_ca_password ]]; then
        password_cmd=" -aes256 -passout pass:${intermediate_ca_password} "
    fi
    openssl ${algorithm} \
        ${password_cmd} \
        -out ${intermediate_ca_dir}/private/${intermediate_ca_prefix}.key.pem \
        ${key_bits_length}
    [ $? -eq 0 ] || exit $?
    chmod 400 ${intermediate_ca_dir}/private/${intermediate_ca_prefix}.key.pem
    [ $? -eq 0 ] || exit $?

    echo "Creating the Intermediate Device CA CSR"
    password_cmd=
    if [[ ! -z $intermediate_ca_password ]]; then
        password_cmd=" -passin pass:${intermediate_ca_password} "
    fi
    openssl req -new -sha256 \
        ${password_cmd} \
        -config ${openssl_intermediate_config_file} \
        -subj "/C=${COUNTRY}/ST=${STATE}/L=${LOCALITY}/CN=${common_name}/O=${ORGANIZATION_NAME}" \
        -key ${intermediate_ca_dir}/private/${intermediate_ca_prefix}.key.pem \
        -out ${intermediate_ca_dir}/csr/${intermediate_ca_prefix}.csr.pem
    [ $? -eq 0 ] || exit $?

    echo "Signing the Intermediate Certificate with Root CA Cert"
    password_cmd=
    if [[ ! -z $root_ca_password ]]; then
        password_cmd=" -passin pass:${root_ca_password} "
    fi
    openssl ca -batch \
        -config ${openssl_root_ca_config_file} \
        ${password_cmd} \
        -extensions v3_intermediate_ca \
        -days 3650 -notext -md sha256 \
        -in ${intermediate_ca_dir}/csr/${intermediate_ca_prefix}.csr.pem \
        -out ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?
    chmod 444 ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "Verify signature of the Intermediate Device Certificate with Root CA"
    openssl verify \
            -CAfile ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem \
            ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "Intermediate CA Certificate Generated At:"
    echo "-----------------------------------------"
    echo "    ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem"
    echo ""
    openssl x509 -noout -text \
            -in ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "Create Root + Intermediate CA Chain Certificate"
    cat ${intermediate_ca_dir}/certs/${intermediate_ca_prefix}.cert.pem \
        ${root_ca_dir}/certs/${root_ca_prefix}.cert.pem > \
        ${intermediate_ca_dir}/certs/${ca_chain_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?
    chmod 444 ${intermediate_ca_dir}/certs/${ca_chain_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "Root + Intermediate CA Chain Certificate Generated At:"
    echo "------------------------------------------------------"
    echo "    ${intermediate_ca_dir}/certs/${ca_chain_prefix}.cert.pem"
}

###############################################################################
# Generate Edge Hub Server Cert
###############################################################################
function generate_edgehub_server_cert()
{
    local home_dir="${1}"
    local intermediate_ca_dir="${2}"
    local ca_chain_prefix="${3}"
    local server_prefix="${4}"
    local openssl_intermediate_config_file="${5}"
    local common_name="${6}"
    local algorithm="${7}"
    local key_bits_length="${8}"
    local intermediate_ca_password="${9}"
    local server_pfx_password="${10}"
    local password_cmd=

    if [ "${algorithm}" == "rsa" ]; then
        algorithm="genrsa"
    else
        echo "Unknown Algorithm ${algorithm} Specified"
        exit 1
    fi

    echo "Creating the Edge Hub Server Certificate"
    echo "----------------------------------------"
    cd ${home_dir}

    echo "Creating the Edge Hub Server Certificate"
    echo "----------------------------------------"
    openssl ${algorithm} \
            -out ${intermediate_ca_dir}/private/${server_prefix}.key.pem \
            ${key_bits_length}
    [ $? -eq 0 ] || exit $?
    chmod 400 ${intermediate_ca_dir}/private/${server_prefix}.key.pem
    [ $? -eq 0 ] || exit $?

    echo "Create the EdgeHub Server Certificate Request"
    openssl req -config ${openssl_intermediate_config_file} \
        -key ${intermediate_ca_dir}/private/${server_prefix}.key.pem \
        -subj "/C=${COUNTRY}/ST=${STATE}/L=${LOCALITY}/CN=${common_name}/O=${ORGANIZATION_NAME}" \
        -new -sha256 -out ${intermediate_ca_dir}/csr/${server_prefix}.csr.pem
    [ $? -eq 0 ] || exit $?

    echo "Create the EdgeHub Server Certificate"
    password_cmd=
    if [[ ! -z $intermediate_ca_password ]]; then
        password_cmd=" -passin pass:${intermediate_ca_password} "
    fi
    openssl ca -batch -config ${openssl_intermediate_config_file} \
            ${password_cmd} \
            -extensions server_cert -days 365 -notext -md sha256 \
            -in ${intermediate_ca_dir}/csr/${server_prefix}.csr.pem \
            -out ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?
    chmod 444 ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    openssl verify \
            -CAfile ${intermediate_ca_dir}/certs/${ca_chain_prefix}.cert.pem \
            ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "EdgeHub Server Certificate Generated At:"
    echo "----------------------------------------"
    echo "    ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem"
    echo ""
    openssl x509 -noout -text \
            -in ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem
    [ $? -eq 0 ] || exit $?

    echo "Create the EdgeHub Server PFX Certificate"
    openssl pkcs12 -in ${intermediate_ca_dir}/certs/${server_prefix}.cert.pem \
            -inkey ${intermediate_ca_dir}/private/${server_prefix}.key.pem \
            -password pass:${server_pfx_password} \
            -export -out ${intermediate_ca_dir}/certs/${server_prefix}.cert.pfx
    [ $? -eq 0 ] || exit $?

    echo "EdgeHub Server PFX Certificate Generated At:"
    echo "--------------------------------------------"
    echo "    ${intermediate_ca_dir}/certs/${server_prefix}.cert.pfx"
    [ $? -eq 0 ] || exit $?
}

###############################################################################
# Copy Required Edge Certs To the Output Dir
###############################################################################
function copy_certs_to_output_dir()
{
    local intermediate_ca_dir="${1}"
    local output_dir="${2}"
    local ca_chain_prefix="${3}"
    local server_prefix="${4}"
    local in_crt=

    # create crt file from PEM file
    in_crt="${intermediate_ca_dir}/certs/${ca_chain_prefix}.cert.pem"
    local out_chain_crt="${output_dir}/${ca_chain_prefix}.pem.crt"
    cp -f ${in_crt} ${out_chain_crt}
    [ $? -eq 0 ] || exit $?

    # create crt file from PEM file
    in_crt="${intermediate_ca_dir}/certs/${server_prefix}.cert.pfx"
    local out_server_crt="${output_dir}/${server_prefix}.cert.pfx"
    cp -f ${in_crt} ${out_server_crt}
    [ $? -eq 0 ] || exit $?

    echo ""
    echo "*********************************************************************"
    echo "Generated Required Edge Certificates In Directory:"
    echo "  ${output_dir}"
    echo "    ${out_chain_crt}"
    echo "    ${out_server_crt}"
    echo "*********************************************************************"
}

###############################################################################
# Generate Required Edge Certs
###############################################################################
function generate_edge_certs()
{
    generate_root_ca "${EDGE_CERTS_HOME_DIR}" \
                    "${EDGE_ROOT_CA_DIR}" \
                    "${EDGE_ROOT_CA_CERT_PREFIX}" \
                    "${EDGE_OPENSSL_CONF_DIR}/${EDGE_ROOT_CA_CERT_CONF}" \
                    "${EDGE_ROOT_CA_COMMON_NAME}" \
                    "rsa" \
                    "4096" \
                    "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}"

    generate_device_ca "${EDGE_CERTS_HOME_DIR}" \
                    "${EDGE_ROOT_CA_DIR}" \
                    "${EDGE_INTERMEDIATE_CA_DIR}" \
                    "${EDGE_ROOT_CA_CERT_PREFIX}" \
                    "${EDGE_INTERMEDIATE_CA_CERT_PREFIX}" \
                    "${EDGE_CHAIN_CA_CERT_PREFIX}" \
                    "${EDGE_OPENSSL_CONF_DIR}/${EDGE_ROOT_CA_CERT_CONF}" \
                    "${EDGE_OPENSSL_CONF_DIR}/${EDGE_INTERMEDIATE_CA_CONF}" \
                    "${EDGE_DEVICE_CA_COMMON_NAME}" \
                    "rsa" \
                    "4096" \
                    "${EDGE_ROOT_CA_PRIVATE_KEY_PASSWD}" \
                    "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}"

    generate_edgehub_server_cert "${EDGE_CERTS_HOME_DIR}" \
                    "${EDGE_INTERMEDIATE_CA_DIR}" \
                    "${EDGE_CHAIN_CA_CERT_PREFIX}" \
                    "${EDGE_HUB_SERVER_CERT_PREFIX}" \
                    "${EDGE_OPENSSL_CONF_DIR}/${EDGE_INTERMEDIATE_CA_CONF}" \
                    "${EDGE_HUB_SERVER_DNS_NAME}" \
                    "rsa" \
                    "2048" \
                    "${EDGE_INTERMEDIATE_CA_PRIVATE_KEY_PASSWD}" \
                    "${PFX_PASSWD}"

    copy_certs_to_output_dir "${EDGE_INTERMEDIATE_CA_DIR}" \
                    "${EDGE_CERTS_OUTPUT_DIR}" \
                    "${EDGE_CHAIN_CA_CERT_PREFIX}" \
                    "${EDGE_HUB_SERVER_CERT_PREFIX}"
}

###############################################################################
# Main Script Execution
###############################################################################
process_args $@
request_user_input
prepare_filesystem
generate_edge_certs
