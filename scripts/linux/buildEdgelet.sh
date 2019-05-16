#!/bin/bash

# This script copies the iodedged executable files that goes into the azureiotedge-iotedged image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes executable files along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
TOOLCHAIN=
STRIP=
SCRIPT_NAME=$(basename $0)
PROJECT=
SRC_DOCKERFILE=
DOCKERFILE=
DOCKER_IMAGENAME=
DEFAULT_DOCKER_NAMESPACE="microsoft"
DOCKER_NAMESPACE=${DEFAULT_DOCKER_NAMESPACE}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH}
PUBLISH_DIR=${BUILD_BINARIESDIRECTORY}/publish
EDGELET_DIR=${BUILD_REPOSITORY_LOCALPATH}/edgelet
BUILD_CONFIGURATION="release"
BUILD_CONFIG_OPTION=

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [[ "$ARCH" == "x86_64" ]]; then
        ARCH="amd64"
        TOOLCHAIN="x86_64-unknown-linux-musl"
        STRIP="strip"
    elif [[ "$ARCH" == "armv7l" ]]; then
        ARCH="arm32v7"
        #TOOLCHAIN="armv7-unknown-linux-gnueabihf"
        TOOLCHAIN="armv7-unknown-linux-musleabihf"
        STRIP="arm-linux-gnueabihf-strip"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="arm64v8"
        TOOLCHAIN="aarch64-unknown-linux-musl"
        STRIP="aarch64-linux-gnu-strip"
    else
        echo "Unsupported architecture"
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
    echo " -i, --image-name     Image name (e.g. edge-agent)"
    echo " -P, --project        Project to build image for (e.g. iotedged)"
    echo " -t, --target-arch    Target architecture (default: uname -m)"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -c, --configuration  Build configuration"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
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
        if [[ ${save_next_arg} -eq 1 ]]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 2 ]]; then
            PROJECT="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 3 ]]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 4 ]]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            BUILD_CONFIGURATION="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target-arch" ) save_next_arg=1;;
                "-P" | "--project" ) save_next_arg=2;;
                "-i" | "--image-name" ) save_next_arg=3;;
                "-n" | "--namespace" ) save_next_arg=4;;
                "-c" | "--configuration" ) save_next_arg=5;;
                "--bin-dir" ) save_next_arg=6;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        mkdir ${BUILD_BINARIESDIRECTORY}
    fi

    DOCKER_DIR=${EDGELET_DIR}/${PROJECT}/docker
    if [[ -z ${DOCKER_DIR} ]] || [[ ! -d ${DOCKER_DIR} ]]; then
        echo "No docker directory for $PROJECT at $EDGELET_DIR"
        print_help_and_exit
    fi

    DOCKERFILE="$DOCKER_DIR/linux/$ARCH/Dockerfile"
    if [[ ! -f ${DOCKERFILE} ]]; then
        echo "No Dockerfile at $DOCKERFILE"
        print_help_and_exit
    fi

    if [[ ${BUILD_CONFIG_OPTION} -eq "release" ]]; then
        BUILD_CONFIGURATION='release'
        BUILD_CONFIG_OPTION='--release'
    else
        BUILD_CONFIGURATION='debug'
        BUILD_CONFIG_OPTION=''
    fi
}

print_args()
{
    echo "Project:      $EDGELET_DIR/$PROJECT"
    echo "Arch:         $ARCH"
    echo "Image:        $DOCKER_IMAGENAME"
    echo "Namespace:    $DOCKER_NAMESPACE"
    echo "Dockerfile:   $DOCKERFILE"
    echo
}

###############################################################################
# Build project and publish result
###############################################################################
build_project()
{
    # build project with cross
    cd ${EDGELET_DIR}

    local BUILD_CMD="cross build -p ${PROJECT} ${BUILD_CONFIG_OPTION} --target ${TOOLCHAIN}"
    echo ${BUILD_CMD}
    ${BUILD_CMD}

    ${STRIP} ${EDGELET_DIR}/target/${TOOLCHAIN}/${BUILD_CONFIGURATION}/${PROJECT}

    # prepare docker folder
    local EXE_DOCKER_DIR=${PUBLISH_DIR}/${DOCKER_IMAGENAME}/docker/linux/${ARCH}
    mkdir -p ${EXE_DOCKER_DIR}

    # copy Dockerfile to publish folder for given arch
    local EXE_DOCKERFILE=${EXE_DOCKER_DIR}/Dockerfile

    local COPY_DOCKERFILE_CMD="cp ${DOCKERFILE} ${EXE_DOCKERFILE}"
    echo ${COPY_DOCKERFILE_CMD}
    ${COPY_DOCKERFILE_CMD}

    # copy binaries to publish folder
    local COPY_CMD="cp ${EDGELET_DIR}/target/${TOOLCHAIN}/${BUILD_CONFIGURATION}/${PROJECT} ${EXE_DOCKER_DIR}/${PROJECT}"
    echo ${COPY_CMD}
    ${COPY_CMD}
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"

print_args
build_project