#!/bin/bash

# This script copies the iodedged executable files that go into the azureiotedge-iotedged image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes executable files along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
TARGET=
STRIP=
SCRIPT_NAME=$(basename "$0")
PROJECT=
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
    elif [[ "$ARCH" == "armv7l" ]]; then
        ARCH="arm32v7"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="arm64v8"
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
    echo " -P, --project        Project to build image for (e.g. aziot-edged)"
    echo " -t, --target-arch    Target architecture (default: uname -m)"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -c, --configuration  Build configuration (default: release)"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

print_args()
{
    echo "Project:      $EDGELET_DIR/$PROJECT"
    echo "Arch:         $ARCH"
    echo "Target:       $TARGET"
    echo "Image:        $DOCKER_IMAGENAME"
    echo "Namespace:    $DOCKER_NAMESPACE"
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

    if [[ ${PROJECT,,} == "aziot-edged" ]]; then
        LIBC="glibc"
    else
        LIBC="musl"
    fi

    case ${ARCH}_${LIBC} in
        amd64_musl) TARGET="x86_64-unknown-linux-musl";;
        amd64_glibc) TARGET="x86_64-unknown-linux-gnu";;
        arm32v7_musl) TARGET="armv7-unknown-linux-musleabihf";;
        arm32v7_glibc) TARGET="armv7-unknown-linux-gnueabihf";;
        arm64v8_musl) TARGET="aarch64-unknown-linux-musl";;
        arm64v8_glibc) TARGET="aarch64-unknown-linux-gnu";;
    esac

    case ${ARCH} in
        amd64) STRIP="strip";;
        arm32v7) STRIP="arm-linux-gnueabihf-strip";;
        arm64v8) STRIP="aarch64-linux-gnu-strip";;
    esac

    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        mkdir "${BUILD_BINARIESDIRECTORY}"
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
    cd "$EDGELET_DIR"

    execute cross build -p "$PROJECT" --manifest-path=aziot-edged/Cargo.toml --no-default-features --features runtime-kubernetes "$BUILD_CONFIG_OPTION" --target "$TARGET"
    execute "$STRIP" "$EDGELET_DIR/target/$TARGET/$BUILD_CONFIGURATION/$PROJECT"

    # prepare docker folder
    local EXE_DOCKER_DIR="$PUBLISH_DIR/$DOCKER_IMAGENAME/docker/linux/$ARCH"
    mkdir -p "$EXE_DOCKER_DIR"

    # copy Dockerfile to publish folder for given arch
    local EXE_DOCKERFILE="$EXE_DOCKER_DIR/Dockerfile"
    execute cp "$DOCKERFILE" "$EXE_DOCKERFILE"

    # copy binaries to publish folder
    execute cp "$EDGELET_DIR/target/$TARGET/$BUILD_CONFIGURATION/$PROJECT" "$EXE_DOCKER_DIR/"

    if [[ ${PROJECT,,} == "aziot-edged" ]] && [[ ${BUILD_CONFIGURATION} == "release" ]]; then
        execute cp "$EDGELET_DIR"/target/"$TARGET"/"$BUILD_CONFIGURATION"/build/hsm-sys-*/out/lib/*.so* "$EXE_DOCKER_DIR/"
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

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"

print_args
build_project
