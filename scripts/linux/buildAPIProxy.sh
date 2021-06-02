#!/bin/bash
#https://github.com/Azure/iotedge/blob/master/scripts/linux/cross-platform-rust-build.sh
###############################################################################
# This script builds a static binary of the api-proxy-module
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
ARCH=$(uname -m)
DIR=$(cd "$(dirname "$0")" && pwd)
TARGET=
SCRIPT_NAME=$(basename "$0")
PROJECT=
DOCKERFILE=
DOCKER_IMAGENAME=
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH}
PUBLISH_DIR=${BUILD_BINARIESDIRECTORY}/publish
API_PROXY_DIR=${BUILD_REPOSITORY_LOCALPATH}/edge-modules/api-proxy-module
BUILD_CONFIGURATION="release"
BUILD_CONFIG_OPTION=

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [[ "$ARCH" == "x86_64" ]]; then
        ARCH="amd64"
    elif [[ "$ARCH" == "armv7l" ]]; then
        ARCH="arm32v7"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="arm64v8"
    else
        echo "Unsupported architecture $ARCH"
        exit 1
    fi
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -i,  --image                  Image name"
    echo " -t,  --target-arch            Target architecture: amd64|arm32v7|aarch64"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

print_args()
{
    echo "Project:      $API_PROXY_DIR"
    echo "Arch:         $ARCH"
    echo "Target:       $TARGET"
    echo "Image:        $DOCKER_IMAGENAME"
    echo "Dockerfile:   $DOCKERFILE"
    echo
}


###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
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
            BUILD_CONFIGURATION="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target-arch" ) save_next_arg=1;;
                "-P" | "--project" ) save_next_arg=2;;
                "-i" | "--image-name" ) save_next_arg=3;;
                "-c" | "--configuration" ) save_next_arg=4;;
                "--bin-dir" ) save_next_arg=5;;
                * ) usage;;
            esac
        fi
    done

    case ${ARCH} in
        amd64) TARGET="x86_64-unknown-linux-musl";;
        arm32v7) TARGET="armv7-unknown-linux-gnueabihf";;
        arm64v8) TARGET="aarch64-unknown-linux-gnu";;
    esac

    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        mkdir "${BUILD_BINARIESDIRECTORY}"
    fi

    DOCKER_DIR=${API_PROXY_DIR}/docker
    if [[ -z ${DOCKER_DIR} ]] || [[ ! -d ${DOCKER_DIR} ]]; then
        echo "No docker directory for $PROJECT at $MODULES_DIR"
        print_help_and_exit
    fi

    DOCKERFILE="$DOCKER_DIR/linux/$ARCH/Dockerfile"
    if [[ ! -f ${DOCKERFILE} ]]; then
        echo "No Dockerfile at $DOCKERFILE"
        print_help_and_exit
    fi

    if [[ ${BUILD_CONFIGURATION,,} == "release" ]]; then
        BUILD_CONFIGURATION='release'
        BUILD_CONFIG_OPTION='--release'
    else
        BUILD_CONFIGURATION='debug'
        BUILD_CONFIG_OPTION=''
    fi
}

###############################################################################
# Build project and publish result
###############################################################################
build_project()
{
    # build project with cross
    if [[ "$ARCH" == "amd64" ]]; then
        execute scripts/linux/cross-platform-rust-build.sh --os alpine --arch "amd64" --build-path edge-modules/api-proxy-module
    elif [[ "$ARCH" == "arm32v7" ]]; then
        execute scripts/linux/cross-platform-rust-build.sh --os ubuntu18.04 --arch "arm32v7" --build-path edge-modules/api-proxy-module
    elif [[ "$ARCH" == "arm64v8" ]]; then
        execute scripts/linux/cross-platform-rust-build.sh --os ubuntu18.04 --arch "aarch64" --build-path edge-modules/api-proxy-module
    else
        echo "Cannot run script Unsupported architecture $ARCH"
        exit 1
    fi

    execute cd "$API_PROXY_DIR"
    # prepare docker folder
    local EXE_DOCKER_DIR="$PUBLISH_DIR/$DOCKER_IMAGENAME/docker/linux/$ARCH"
    execute mkdir -p "$EXE_DOCKER_DIR"

    # copy Dockerfile to publish folder for given arch
    local EXE_DOCKERFILE="$EXE_DOCKER_DIR/Dockerfile"
    execute cp "$DOCKERFILE" "$EXE_DOCKERFILE"

    # copy binaries to publish folder
    execute cp "$API_PROXY_DIR/target/$TARGET/$BUILD_CONFIGURATION/$PROJECT" "$EXE_DOCKER_DIR/"

    # copy template files
    execute cp -r "$API_PROXY_DIR/templates" "$EXE_DOCKER_DIR/"

    if [[ $ARCH == "arm64v8" ]]; then
        execute cp -r "$API_PROXY_DIR/docker/linux/$ARCH/lib" "$EXE_DOCKER_DIR/"
        ls $EXE_DOCKER_DIR
    fi
}

###############################################################################
# Print given command and execute it
###############################################################################
execute()
{
    echo "\$ $*"
    "$@"
    echo
}
check_arch
process_args "$@"

print_args
build_project
