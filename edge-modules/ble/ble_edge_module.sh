#!/bin/bash
# Copyright (c) Microsoft. All rights reserved.

###############################################################################
# Purpose
#  This script builds and executes the BLE Edge module docker containers
#  Run with --help for information
###############################################################################

###############################################################################
# Obtain absolute path to a directory of the provided file
# $1 : relative filename
###############################################################################

set -e

get_abs_dir()
{
    name="$1"
    if [ -d "$(dirname "$name")" ]; then
        pushd . > /dev/null
        cd $(dirname "$name") > /dev/null
        name=$(pwd)
        popd  > /dev/null
    else
        echo "Invalid Directory Provided."
        exit 1
    fi
    echo "$name"
}

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
GATEWAY_REPO=${GATEWAY_REPO:="https://github.com/Azure/azure-iot-gateway-sdk"}
FORCE_BLE_GATEWAY_SYNC=OFF
BUILD_BLE_GATEWAY=ON
BUILD_DOCKER_IMAGE=ON
RUN_DOCKER_IMAGE=ON
BLE_CONFIG_FILE=
DOCKER_REGISTRY=
SCRIPT_NAME=$(basename $0)
SCRIPT_DIR=$(get_abs_dir $0)
GATEWAY_DIR="$SCRIPT_DIR/gateway"
PUBLISH_DIR="$SCRIPT_DIR/publish"

###############################################################################
# Obtain absolute file path of the file
# $1 : relative filename
###############################################################################
get_abs_filename()
{
    name="$1"
    if [ -d "$(dirname "$name")" ]; then
        pushd . > /dev/null
        cd $(dirname "$name") > /dev/null
        file_name=$(pwd)
        file_name+="/"
        file_name+=$(basename $name)
        popd  > /dev/null
    else
        echo "Invalid Filename Provided."
        exit 1
    fi
    echo "$file_name"
}

###############################################################################
# Function to validate host OS
###############################################################################
check_os()
{
    OS=$(lsb_release -d | awk '{print $2}')
    if [ "$OS" != "Ubuntu" ]; then
        echo "This script works only on Ubuntu"
        exit 1
    fi
}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ "$ARCH" == "x86_64" ]; then
        ARCH="x64"
    elif [ "$ARCH" == "armv7l" ]; then
        ARCH="armv7hf"
    else
        echo "Unsupported Architecture"
        exit 1
    fi
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -r, --registry               Docker registry required to build, tag and run the module"
    echo " --force-ble-gateway-sync     Force resync of the BLE gateway source even if available locally"
    echo " --disable-ble-gateway-build  Do sync out and build BLE module from source"
    echo " --disable-docker-build       Do not build the module docker image"   
    echo " --disable-docker-run         Do not run the docker module"
    echo " --ble_config_file            BLE config file required when running the BLE module."
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
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            BLE_CONFIG_FILE=$(get_abs_filename "$arg")
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "--ble_config_file" ) save_next_arg=2;;
                "--disable-ble-gateway-build" ) BUILD_BLE_GATEWAY=OFF;;
                "--disable-docker-build" ) BUILD_DOCKER_IMAGE=OFF;;
                "--disable-docker-run" ) RUN_DOCKER_IMAGE=OFF;;
                "--force-ble-gateway-sync" ) FORCE_BLE_GATEWAY_SYNC=ON;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_REGISTRY} ]] && [[ $BUILD_DOCKER_IMAGE == ON || $RUN_DOCKER_IMAGE == ON ]]; then
        echo "Registry Not Provided"
        print_help_and_exit
    fi

    if [[ -z ${BLE_CONFIG_FILE} ]] && [[ $RUN_DOCKER_IMAGE == ON ]]; then
        echo "BLE Config File Not Provided"
        print_help_and_exit
    fi
}

###############################################################################
# Function to clean up any publish artifacts
###############################################################################
perform_clean()
{
    echo "Cleaning Publish Dir located in $SCRIPT_DIR"
    rm -fr $PUBLISH_DIR
    [ $? -eq 0 ] || exit $?
}

###############################################################################
# Function to sync out the BLE gateway source files
###############################################################################
sync_ble_gateway()
{
    echo "Syncing Gateway Source"
    rm -fr $GATEWAY_DIR
    [ $? -eq 0 ] || exit $?
    git clone ${GATEWAY_REPO} $GATEWAY_DIR
    [ $? -eq 0 ] || exit $?
    git -C $GATEWAY_DIR checkout master
    [ $? -eq 0 ] || exit $?
}

###############################################################################
# Function to build the BLE gateway source files
###############################################################################
build_ble_gateway()
{
    echo "Building Gateway Sources located here $GATEWAY_DIR"
    $GATEWAY_DIR/tools/build.sh
    if [ $? -ne 0 ]; then
        echo "Gateway Build Failed. Exit Code $?"
        exit $?
    fi
}

###############################################################################
# Function to publish the BLE module and its deps for incluion in the container
###############################################################################
publish_ble_module()
{
    echo "Preparing Publish Dir: $PUBLISH_DIR"
    mkdir $PUBLISH_DIR
    mkdir $PUBLISH_DIR/lib
    mkdir $PUBLISH_DIR/install-deps
    find  $GATEWAY_DIR/build -name '*.so' -exec cp {} $PUBLISH_DIR/lib \;
    cp -r $GATEWAY_DIR/install-deps/include $PUBLISH_DIR/install-deps
    cp -r $GATEWAY_DIR/install-deps/lib $PUBLISH_DIR/install-deps
    cp    $GATEWAY_DIR/build/samples/ble_gateway/ble_gateway $PUBLISH_DIR
}

###############################################################################
# Function to build the BLE module container
###############################################################################
build_edge_ble_docker_container()
{
    docker_file="$SCRIPT_DIR"
    docker_file+="/docker/${ARCH}/Dockerfile"
    
    docker_build_cmd="docker build -t ${DOCKER_REGISTRY}/azedge-ble-${ARCH} -f ${docker_file} ."
    echo "Running Command: $docker_build_cmd"
    $docker_build_cmd
    if [ $? -ne 0 ]; then
        echo "Docker Build Failed With Exit Code $?"
        exit $?
    fi
}

###############################################################################
# Function to run BLE module in its container.
# Note: if it is not being built locally or the container is not available
# locally, it will be pulled down from the registry.
###############################################################################
run_edge_ble_docker_container()
{
    docker_run_cmd="docker run "
    docker_run_cmd+="-v /var/run/dbus:/var/run/dbus "
    docker_run_cmd+="-v ${BLE_CONFIG_FILE}:/app/ble_config/config.json:ro "
    docker_run_cmd+="${DOCKER_REGISTRY}/azedge-ble-${ARCH} "
    echo "Running Command: $docker_run_cmd"
    $docker_run_cmd
    if [ $? -ne 0 ]; then
        echo "Docker Run Failed With Exit Code $?"
        exit $?
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
check_os
check_arch
process_args $@

if [[ $FORCE_BLE_GATEWAY_SYNC == ON ]]; then
    sync_ble_gateway
fi

if [[ $BUILD_BLE_GATEWAY == ON ]]; then
    perform_clean
    if [[ ! -d $GATEWAY_DIR ]]; then
        sync_ble_gateway
    fi
    build_ble_gateway
    publish_ble_module
fi

if [[ $BUILD_DOCKER_IMAGE == ON ]]; then
    build_edge_ble_docker_container
fi

if [[ $RUN_DOCKER_IMAGE == ON ]]; then
    run_edge_ble_docker_container
fi

[ $? -eq 0 ] || exit $?
