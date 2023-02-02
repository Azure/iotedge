#!/bin/bash

###############################################################################
# This Script builds a specific Edge application in their respective docker
# containers. This script expects that buildBranch.sh was invoked earlier and
# all the necessary application files and their Dockerfile be published in
# directory identified by environment variable BUILD_BINARIESDIRECTORY
###############################################################################

set -xeuo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
SCRIPT_NAME=$(basename "$0")
PUBLISH_DIR=
PROJECT=
DOCKERFILE=
DOCKER_IMAGENAME=
DEFAULT_DOCKER_NAMESPACE='microsoft'
DOCKER_NAMESPACE=${DEFAULT_DOCKER_NAMESPACE}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}
SKIP_PUSH=0
SOURCE_MAP=

###############################################################################
# Check format and content of --arch argument
###############################################################################
check_arch() {
    case "$ARCH" in
        'x86_64') ARCH='amd64' ;;
        'armv7l') ARCH='arm/v7' ;;
        'aarch64') ARCH='arm64' ;;
        'amd64'|'arm64'|'arm/v7') ;;
        *) echo "Unsupported architecture '$ARCH'" && exit 1 ;;
    esac
}

###############################################################################
# Convert from the format of the --arch argument to the format we use in our
# image tags. Docker defines the former (amd64, arm64, or arm/v7; see Docker's
# TARGETARCH automatic variable[1]), we define the latter (amd64, arm64v8, and
# arm32v7).
# [1] https://docs.docker.com/engine/reference/builder/#automatic-platform-args-in-the-global-scope
###############################################################################
convert_arch() {
    arch="$1"
    case "$arch" in
        'amd64') echo 'amd64' ;;
        'arm64') echo 'arm64v8' ;;
        'arm/v7') echo 'arm32v7' ;;
        *) echo "Unsupported architecture '$arch'" && exit 1 ;;
    esac
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage() {
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -i, --image-name     Image name (e.g. edge-agent)"
    echo " -P, --project        Project to build image for (e.g. Microsoft.Azure.Devices.Edge.Agent.Service)"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -v, --image-version  Docker Image Version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " -a, --arch           The target architecture to build (supported values are amd64, arm64, and arm/v7)"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    echo "--source-map          Path to the JSON file that maps Dockerfile image sources to their replacements. Assumes the tool 'gnarly' is in the PATH"
    echo "--skip-push           Build images, but don't push them"
    exit 1
}

print_help_and_exit() {
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args() {
    save_next_arg=0
    for arg in $@; do
        if [[ ${save_next_arg} -eq 1 ]]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 2 ]]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 3 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 4 ]]; then
            SOURCE_MAP="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            PROJECT="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 7 ]]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 8 ]]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-r" | "--registry") save_next_arg=1 ;;
            "-v" | "--image-version") save_next_arg=2 ;;
            "--bin-dir") save_next_arg=3 ;;
            "--source-map") save_next_arg=4 ;;
            "-a" | "--arch") save_next_arg=5 ;;
            "-P" | "--project") save_next_arg=6 ;;
            "-i" | "--image-name") save_next_arg=7 ;;
            "-n" | "--namespace") save_next_arg=8 ;;
            "--skip-push") SKIP_PUSH=1 ;;
            *) echo "Unknown argument '$arg'"; usage ;;
            esac
        fi
    done

    if [[ -z "$DOCKER_REGISTRY" ]]; then
        echo 'The --registry parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGENAME" ]]; then
        echo 'The --image-name parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGEVERSION" ]]; then
        if [[ -n "$BUILD_BUILDNUMBER" ]]; then
            DOCKER_IMAGEVERSION="$BUILD_BUILDNUMBER"
        else
            echo 'The --image-version parameter is required if BUILD_BUILDNUMBER is not set'
            print_help_and_exit
        fi
    fi

    if [[ -z "$BUILD_BINARIESDIRECTORY" ]]; then
        echo 'The --bin-dir parameter is required if BUILD_BINARIESDIRECTORY is not set'
        print_help_and_exit
    fi

    if [[ ! -d "$BUILD_BINARIESDIRECTORY" ]]; then
        echo "Binaries dir '$BUILD_BINARIESDIRECTORY' not found"
        print_help_and_exit
    fi

    PUBLISH_DIR="$BUILD_BINARIESDIRECTORY/publish"

    if [[ ! -d "$PUBLISH_DIR" ]]; then
        echo "$PUBLISH_DIR not found"
        print_help_and_exit
    fi

    EXE_DOCKER_DIR=${PUBLISH_DIR}/${PROJECT}/docker

    if [[ ! -d "$EXE_DOCKER_DIR" ]]; then
        echo "$EXE_DOCKER_DIR not found"
        print_help_and_exit
    fi

    if [[ -n "$SOURCE_MAP" ]] && [[ ! -f "$SOURCE_MAP" ]]; then
        echo 'File specified by --source-map not found'
        print_help_and_exit
    fi

    if [[ -n "$SOURCE_MAP" ]] && ! command -v gnarly > /dev/null; then
        echo '--source-map specified, but required tool 'gnarly' not found in PATH'
        print_help_and_exit
    fi

    dockerfile1="$EXE_DOCKER_DIR/linux/Dockerfile"
    if [[ -f "$dockerfile1" ]]; then
        DOCKERFILE="$dockerfile1"
    else
        arch=$(convert_arch $ARCH)
        dockerfile2="$EXE_DOCKER_DIR/linux/$arch/Dockerfile"
        if [[ -f "$dockerfile2" ]]; then
            DOCKERFILE="$dockerfile2"
        else
            echo "No Dockerfile at '$dockerfile1' or '$dockerfile2'"
            print_help_and_exit
        fi
    fi
}

###############################################################################
# Build docker image and push it to private repo
#
#   @param[1] - imagename; Name of the docker edge image to publish; Required;
#   @param[2] - arch; Architectures to build; Required;
#   @param[3] - dockerfile; Path to the dockerfile; Required;
#   @param[4] - context_path; docker context path; Required;
#   @param[5] - build_args; docker context path; Optional;
#               Leave as "" and no build args will be supplied.
###############################################################################
docker_build_and_tag_and_push() {
    imagename="$1"
    arch="$2"
    dockerfile="$3"
    context_path="$4"
    build_args="$5"
    build_context=''

    if [[ -z "$imagename" ]] || [[ -z "$arch" ]] || [[ -z "$dockerfile" ]] || [[ -z "$context_path" ]]; then
        echo "Error: Arguments are invalid [$imagename] [$arch] [$dockerfile] [$context_path]"
        exit 1
    fi

    docker buildx create --use --bootstrap
    trap "docker buildx rm" EXIT

    suffix=$(convert_arch $arch)
    image="$DOCKER_REGISTRY/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$suffix"
    platform="linux/$arch"

    if [[ ${SKIP_PUSH} -eq 0 ]]; then
        output_type='registry'
        echo "Building and pushing image '$image'"
    else
        output_type='docker'
        echo "Building image '$image', skipping push"
    fi

    if [[ -n "$SOURCE_MAP" ]]; then
        build_context=$(gnarly --mod-config $SOURCE_MAP $dockerfile)
    fi

    docker buildx build \
        --no-cache \
        --platform $platform \
        $([ -z "$build_args" ] || echo $build_args) \
        --file $dockerfile \
        --output=type=$output_type,name=$image,buildinfo-attrs=true \
        $([ -z "$build_context" ] || echo $build_context) \
        $context_path
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args $@

build_args=("EXE_DIR=.")

# push image
docker_build_and_tag_and_push \
    "$DOCKER_IMAGENAME" \
    "$ARCH" \
    "$DOCKERFILE" \
    "$PUBLISH_DIR/$PROJECT" \
    "${build_args[@]/#/--build-arg }"
[[ $? -eq 0 ]] || exit $?

echo "Done building and pushing Docker image $DOCKER_IMAGENAME for $PROJECT"

[[ $? -eq 0 ]] || exit $?
