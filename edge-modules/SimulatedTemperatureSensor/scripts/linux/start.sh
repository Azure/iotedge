#!/bin/bash

###############################################################################
# Script to setup a edge module before executing it. The input to this script
# are driven through environament variables
#    Required Environament Variables
#        MODULE_NAME - This is the name of the dotnet DLL to execute
#    Optional Environament Variables
#        EdgeModuleCACertificateFile - This is set by the Edge Agent when
#                                      launching the module and is used for
#                                      TLS validation of the EdgeHub server
###############################################################################

###############################################################################
# Global Script Variables
###############################################################################
# Path on Debian OSes where CA certs are to be copied before they are installed
CA_CERT_PATH="/usr/local/share/ca-certificates/edge-module-ca.crt"

# Module name to be launched
LAUNCH_MODULE_NAME=

###############################################################################
# Helper function to install a CA certificate
# Input:
#   CA certificate file path
#   CA certificate install path
#
# Return Ouput: None
# Globals Updated: None
###############################################################################
function install_ca_cert()
{
    ca_cert_file_path="${1}"
    ca_cert_install_path="${2}"

    # copy the CA cert into the ca cert dir
    command="cp ${ca_cert_file_path} ${ca_cert_install_path}"
    echo "Executing Command:${command}"
    ${command}
    if [ $? -ne 0 ]; then
        echo "Failed to Copy Edge Device CA Certificate."
        exit 1
    fi

    # register the newly added CA cert
    command="update-ca-certificates"
    echo "Executing: ${command}"
    ${command}
    if [ $? -ne 0 ]; then
        echo "Failed to Update CA Certificate."
        exit 1
    fi
    echo "Certificate installed successfully!"

}
###############################################################################
# Validate Environment Variables and Install any CA certificate(s)
#    MODULE_NAME - Edge Module DLL to launch
#    EdgeModuleCACertificateFile - Edge CA Certificate To Install
#
# Return Ouput: None
# Globals Updated: None
###############################################################################
function validate_and_process_env_args()
{
    # make sure MODULE to be launched is provided
    LAUNCH_MODULE_NAME=$(printenv MODULE_NAME)
    if [[ -z "${LAUNCH_MODULE_NAME}" ]]; then
        echo "Module Name Cannot Be Empty"
        exit 1
    fi

    # check if the CA certificate is available and if so install it
    EdgeModuleCACertificateFile=$(printenv EdgeModuleCACertificateFile)
    echo "CA Certificate File: ${EdgeModuleCACertificateFile}"
    # check if the EdgeAgent supplied the module with the Edge Runtime CA cert
    if [[ ! -z "${EdgeModuleCACertificateFile}" ]]; then
        install_ca_cert ${EdgeModuleCACertificateFile} ${CA_CERT_PATH}
    else
        echo "Warning: Edge Hub CA Certificate Not Available! Edge Hub" \
             " Server TLS validation will likely fail."
    fi
}

function launch_module()
{
    local module="${1}"
    echo "Launching Edge Module: ${module}"
    exec dotnet ${module}
}

###############################################################################
# Main Script Execution
###############################################################################
validate_and_process_env_args
launch_module "${LAUNCH_MODULE_NAME}"
