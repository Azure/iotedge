#!/bin/bash

###############################################################################
# This script builds an Edge application as a multi-arch Docker image, tagged:
#   {registry}/{namespace}/{name}:{version}
# Each arch-specific image is also tagged:
#   {registry}/{namespace}/{name}:{version}-linux-{arch}
# ...where {arch} is one of amd64, arm64v8, or arm32v7.
#
# This script expects that buildBranch.sh was invoked earlier and all the
# application's files were published to the directory '{bin}/publish/{app}',
# where {bin} is passed to the script's '--bin' parameter and {app} is passed
# to the '--app' parameter. It also expects that the application's Dockerfile
# was published to '{bin}/publish/{app}/docker/linux/Dockerfile'.
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
ARCH_LIST='amd64,arm/v7,arm64'
APP_BINARIESDIRECTORY=
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}
DOCKER_IMAGENAME=
DOCKER_NAMESPACE='microsoft'
APP=
SCRIPT_NAME=$(basename "$0")
SKIP_PUSH=0
SOURCE_MAP=

###############################################################################
# Convert from Docker's architecture format to our image tag format.
# Docker's format:
#   amd64, arm64, or arm/v7 (see Docker's TARGETARCH automatic variable[1])
# Our format:
#   amd64,  arm64v8, and arm32v7
# [1] https://docs.docker.com/engine/reference/builder/#automatic-platform-args-in-the-global-scope
###############################################################################
convert_arch() {
    arch="$1"
    case "$arch" in
        'amd64') echo 'amd64' ;;
        'arm/v7') echo 'arm32v7' ;;
        'arm64') echo 'arm64v8' ;;
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
    echo " -a, --app            App to build image for (e.g. Microsoft.Azure.Devices.Edge.Agent.Service)"
    echo " -b, --bin            Path to the application binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    echo " -h, --help           Print this message and exit"
    echo " -m, --source-map     Path to the JSON file that maps Dockerfile image sources to their replacements. Assumes the tool 'gnarly' is in the PATH"
    echo " -n, --name           Image name (e.g. azureiotedge-agent)"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -s, --skip-push      Build images, but don't push them"
    echo " -v, --version        App version. Either use this option or set env variable BUILD_BUILDNUMBER"
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
            APP="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 2 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 3 ]]; then
            SOURCE_MAP="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 4 ]]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-a" | "--app") save_next_arg=1 ;;
            "-b" | "--bin") save_next_arg=2 ;;
            "-h" | "--help") usage ;;
            "-m" | "--source-map") save_next_arg=3 ;;
            "-n" | "--name") save_next_arg=4 ;;
            "-r" | "--registry") save_next_arg=5 ;;
            "-s" | "--skip-push") SKIP_PUSH=1 ;;
            "-v" | "--version") save_next_arg=6 ;;
            *) echo "Unknown argument '$arg'"; usage ;;
            esac
        fi
    done

    if [[ -z "$DOCKER_REGISTRY" ]]; then
        echo 'The --registry parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGENAME" ]]; then
        echo 'The --name parameter is required'
        print_help_and_exit
    fi

    if [[ -z "$DOCKER_IMAGEVERSION" ]]; then
        if [[ -n "$BUILD_BUILDNUMBER" ]]; then
            DOCKER_IMAGEVERSION="$BUILD_BUILDNUMBER"
        else
            echo 'The --version parameter is required if BUILD_BUILDNUMBER is not set'
            print_help_and_exit
        fi
    fi

    if [[ -z "$APP" ]]; then
        echo 'The --app parameter is required'
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

    if [[ -z "$BUILD_BINARIESDIRECTORY" ]]; then
        echo 'The --bin parameter is required if BUILD_BINARIESDIRECTORY is not set'
        print_help_and_exit
    fi

    if [[ ! -d "$BUILD_BINARIESDIRECTORY" ]]; then
        echo "Binaries directory '$BUILD_BINARIESDIRECTORY' not found"
        print_help_and_exit
    fi

    APP_BINARIESDIRECTORY="$BUILD_BINARIESDIRECTORY/publish/$APP"
    if [[ ! -d "$APP_BINARIESDIRECTORY" ]]; then
        echo "Application binaries directory '$APP_BINARIESDIRECTORY' not found"
        print_help_and_exit
    fi

    # The API proxy module has separate Dockerfiles for each supported architecture
    if [[ "$APP" == 'api-proxy-module' ]]; then

        if [[ ! -f "$APP_BINARIESDIRECTORY/docker/linux/amd64/Dockerfile" ]]; then
            echo "No Dockerfile at '$APP_BINARIESDIRECTORY/docker/linux/Dockerfile'"
            print_help_and_exit
        elif [[ ! -f "$APP_BINARIESDIRECTORY/docker/linux/arm64v8/Dockerfile" ]]; then
            echo "No Dockerfile at '$APP_BINARIESDIRECTORY/docker/linux/Dockerfile'"
            print_help_and_exit
        elif [[ ! -f "$APP_BINARIESDIRECTORY/docker/linux/arm32v7/Dockerfile" ]]; then
            echo "No Dockerfile at '$APP_BINARIESDIRECTORY/docker/linux/Dockerfile'"
            print_help_and_exit
        fi
    elif [[ ! -f "$APP_BINARIESDIRECTORY/docker/linux/Dockerfile" ]]; then
        echo "No Dockerfile at '$APP_BINARIESDIRECTORY/docker/linux/Dockerfile'"
        print_help_and_exit
    fi
}

###############################################################################
# Main script execution
###############################################################################
process_args $@

BUILD_CONTEXT=
DOCKERFILE="$APP_BINARIESDIRECTORY/docker/linux/Dockerfile"
IMAGE="$DOCKER_REGISTRY/$DOCKER_NAMESPACE/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION"
PLATFORMS="linux/${ARCH_LIST//,/,linux/}"

if [[ "$SKIP_PUSH" -eq 0 ]]; then
    OUTPUT_TYPE='registry'
    echo "Building and pushing image '$IMAGE'"
else
    OUTPUT_TYPE='docker'
    echo "Building image '$IMAGE', skipping push"
fi

if [[ -n "$SOURCE_MAP" ]]; then
    BUILD_CONTEXT=$(gnarly --mod-config $SOURCE_MAP $DOCKERFILE)
fi

docker buildx create --use --bootstrap
trap "docker buildx rm" EXIT

if [[ "$APP" == 'api-proxy-module' ]]; then
    # First, build each platform-specific image from a separate Dockerfile
    ARCH_IMAGES=()
    IFS=',' read -a ARCH_ARR <<< "$ARCH_LIST"
    for ARCH in ${ARCH_ARR[@]}
    do
        ARCH_IMAGE="$IMAGE-linux-$(convert_arch $ARCH)"

        docker buildx build \
            --no-cache \
            --platform "linux/$ARCH" \
            --file "$APP_BINARIESDIRECTORY/docker/linux/$ARCH/Dockerfile" \
            --output=type=$OUTPUT_TYPE,name=$ARCH_IMAGE,buildinfo-attrs=true \
            --build-arg EXE_DIR=. \
            $([ -z "$BUILD_CONTEXT" ] || echo $BUILD_CONTEXT) \
            $APP_BINARIESDIRECTORY

        ARCH_IMAGES+=( $ARCH_IMAGE )
    done

    # Next, build the multi-arch image
    docker buildx imagetools create --tag $IMAGE ${ARCH_IMAGES[@]}
else
    # First, build the complete multi-arch image
    docker buildx build \
        --no-cache \
        --platform "$PLATFORMS" \
        --file "$DOCKERFILE" \
        --output=type=$OUTPUT_TYPE,name=$IMAGE,buildinfo-attrs=true \
        --build-arg EXE_DIR=. \
        $([ -z "$BUILD_CONTEXT" ] || echo $BUILD_CONTEXT) \
        "$APP_BINARIESDIRECTORY"

    # Next, tag each arch-specific image
    IFS=',' read -a ARCH_ARR <<< "$ARCH_LIST"
    for ARCH in ${ARCH_ARR[@]}
    do
        digest=$(docker buildx imagetools inspect $IMAGE --format '{{json .Manifest}}' |
            jq -r --arg arch "$ARCH" '
                .manifests[] |
                select($arch == ([.platform | (.architecture, .variant // empty)] | join("/"))) |
                .digest')

        suffix=$(convert_arch $ARCH)
        docker buildx imagetools create --tag "$IMAGE-linux-$suffix" "$IMAGE@$digest"
    done
fi


echo "Done building Docker image $DOCKER_IMAGENAME for $APP"
