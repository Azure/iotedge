#!/bin/bash

###############################################################################
# This script builds an Edge application as a multi-platform Docker image
# and tags it as:
#   {registry}/{namespace}/{name}:{version}
# Each platform-specific image is also tagged:
#   {registry}/{namespace}/{name}:{version}-{platform}
# ...where {platform} is one of linux-amd64, linux-arm64v8, or linux-arm32v7.
#
# The script expects that buildBranch.sh was invoked earlier and all the
# application's files were published to the directory '{bin}/publish/{app}',
# where {bin} is passed to the script's '--bin' parameter and {app} is passed
# to the '--app' parameter. It also expects that the application's Dockerfile
# was published to '{bin}/publish/{app}/docker/linux/Dockerfile'.
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
APP=
APP_BINARIESDIRECTORY=
PLATFORMS='linux/amd64,linux/arm/v7,linux/arm64'
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}
DOCKER_IMAGENAME=
DOCKER_NAMESPACE='microsoft'
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
SCRIPT_NAME=$(basename "$0")
SOURCE_MAP=

###############################################################################
# Check format and content of the --platforms argument
###############################################################################
check_platforms() {
    IFS=',' read -a plat_arr <<< "$PLATFORMS"
    for platform in ${plat_arr[@]}
    do
        case "$platform" in
            'linux/amd64'|'linux/arm64'|'linux/arm/v7') ;;
            *) echo "Unsupported platform '$platform'" && exit 1 ;;
        esac
    done
}

###############################################################################
# Convert from Docker's platform format to our image tag format.
# Docker's format:
#   linux/amd64, linux/arm64, or linux/arm/v7
#   (see Docker's TARGETPLATFORM automatic variable[1])
# Our format:
#   linux-amd64,  linux-arm64v8, and linux-arm32v7
# [1] https://docs.docker.com/engine/reference/builder/#automatic-platform-args-in-the-global-scope
###############################################################################
convert_platform() {
    platform="$1"
    case "$platform" in
        'linux/amd64') echo 'linux-amd64' ;;
        'linux/arm64') echo 'linux-arm64v8' ;;
        'linux/arm/v7') echo 'linux-arm32v7' ;;
        *) echo "Unsupported platform '$platform'" && exit 1 ;;
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
    echo " -p, --platforms      Platforms to build. Default is '$PLATFORMS'"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
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
            PLATFORMS="$arg"
            check_platforms
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 7 ]]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-a" | "--app") save_next_arg=1 ;;
            "-b" | "--bin") save_next_arg=2 ;;
            "-h" | "--help") usage ;;
            "-m" | "--source-map") save_next_arg=3 ;;
            "-n" | "--name") save_next_arg=4 ;;
            "-p" | "--platforms") save_next_arg=5 ;;
            "-r" | "--registry") save_next_arg=6 ;;
            "-v" | "--version") save_next_arg=7 ;;
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

    # The API proxy module has separate Dockerfiles for each supported platform
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

    if ! command -v jq > /dev/null; then
        command jq
    fi
}

###############################################################################
# Main script execution
###############################################################################
process_args $@

BUILD_CONTEXT=
DOCKERFILE="$APP_BINARIESDIRECTORY/docker/linux/Dockerfile"
IMAGE="$DOCKER_REGISTRY/$DOCKER_NAMESPACE/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION"

echo "Building and pushing image '$IMAGE'"

docker buildx create --use --bootstrap
trap "docker buildx rm" EXIT

if [[ "$APP" == 'api-proxy-module' ]]; then
    # First, build each platform-specific image from a separate Dockerfile. This will create
    # intermediate manifest lists, each pointing to:
    #   1. a platform-specific image
    #   2. a provenance artifact
    PLAT_IMAGES=()
    IFS=',' read -a PLAT_ARR <<< "$PLATFORMS"
    for PLATFORM in ${PLAT_ARR[@]}
    do
        CONVERTED_PLATFORM="$(convert_platform $PLATFORM)"
        PLAT_IMAGE="$IMAGE-$CONVERTED_PLATFORM"

        if [[ -n "$SOURCE_MAP" ]]; then
            BUILD_CONTEXT=$(gnarly --mod-config $SOURCE_MAP \
                "$APP_BINARIESDIRECTORY/docker/${CONVERTED_PLATFORM/-/\/}/Dockerfile")
        fi

        docker buildx build \
            --no-cache \
            --platform "$PLATFORM" \
            --file "$APP_BINARIESDIRECTORY/docker/${CONVERTED_PLATFORM/-/\/}/Dockerfile" \
            --output=type=registry,name=$PLAT_IMAGE \
            --build-arg EXE_DIR=. \
            $([ -z "$BUILD_CONTEXT" ] || echo $BUILD_CONTEXT) \
            $APP_BINARIESDIRECTORY

        PLAT_IMAGES+=( $PLAT_IMAGE )
    done

    # Next, create the multi-platform image from the platform-specific images
    docker buildx imagetools create --tag $IMAGE ${PLAT_IMAGES[@]}

    # Finally, tag each platform-specific image. This will untag the intermediate manifest lists,
    # which are no longer needed.
    source "$SCRIPT_DIR/manifest-tools.sh"

    REGISTRY="$DOCKER_REGISTRY" \
    REPOSITORY="$DOCKER_NAMESPACE/$DOCKER_IMAGENAME" \
    TAG="$DOCKER_IMAGEVERSION" \
    copy_manifests
else
    if [[ -n "$SOURCE_MAP" ]]; then
        BUILD_CONTEXT=$(gnarly --mod-config $SOURCE_MAP $DOCKERFILE)
    fi

    # First, build the complete multi-platform image
    docker buildx build \
        --no-cache \
        --platform "$PLATFORMS" \
        --file "$DOCKERFILE" \
        --output=type=registry,name=$IMAGE \
        --build-arg EXE_DIR=. \
        $([ -z "$BUILD_CONTEXT" ] || echo $BUILD_CONTEXT) \
        "$APP_BINARIESDIRECTORY"

    # Next, tag each platform-specific image
    source "$SCRIPT_DIR/manifest-tools.sh"

    PLATFORM_MAP="$(echo "$DEFAULT_PLATFORM_MAP" | jq -c --arg platforms "$PLATFORMS" '
        map(select(.platform == ($platforms | split(",")[])))
    ')" \
    REGISTRY="$DOCKER_REGISTRY" \
    REPOSITORY="$DOCKER_NAMESPACE/$DOCKER_IMAGENAME" \
    TAG="$DOCKER_IMAGEVERSION" \
    copy_manifests
fi

echo "Built and pushed image '$IMAGE'"
