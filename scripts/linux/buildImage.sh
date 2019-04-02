#!/bin/bash

###############################################################################
# This Script builds a specific Edge application in their respective docker
# containers. This script expects that buildBranch.sh was invoked earlier and
# all the necessary application files and their Dockerfile be published in
# directory identified by environment variable BUILD_BINARIESDIRECTORY
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
SCRIPT_NAME=$(basename $0)
PUBLISH_DIR=
BASE_TAG=
PROJECT=
DOCKERFILE=
DOCKER_IMAGENAME=
DEFAULT_DOCKER_NAMESPACE="microsoft"
DOCKER_NAMESPACE=$DEFAULT_DOCKER_NAMESPACE
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}
SKIP_PUSH=0

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ "$ARCH" == "x86_64" ]; then
        ARCH="amd64"
    elif [ "$ARCH" == "armv7l" ]; then
        ARCH="arm32v7"
    elif [ "$ARCH" == "aarch64" ]; then
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
    echo " -P, --project        Project to build image for (e.g. Microsoft.Azure.Devices.Edge.Agent.Service)"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -u, --username       Docker Registry Username"
    echo " -p, --password       Docker Username's password"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -v, --image-version  Docker Image Version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " -t, --target-arch    Target architecture (default: uname -m)"
    echo "--base-tag            Override the tag of the base image (e.g., to use a different version of .NET Core)"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    echo "--skip-push           Build images, but don't push them"
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
            DOCKER_USERNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_PASSWORD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            BASE_TAG="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            PROJECT="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 9 ]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 10 ]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "--bin-dir" ) save_next_arg=5;;
                "--base-tag" ) save_next_arg=6;;
                "-t" | "--target-arch" ) save_next_arg=7;;
                "-P" | "--project" ) save_next_arg=8;;
                "-i" | "--image-name" ) save_next_arg=9;;
                "-n" | "--namespace" ) save_next_arg=10;;
                "--skip-push" ) SKIP_PUSH=1 ;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_REGISTRY} ]]; then
        echo "Registry parameter invalid"
        print_help_and_exit
    fi

    if [[ $SKIP_PUSH -eq 0 ]]; then
        if [[ -z ${DOCKER_USERNAME} ]]; then
            echo "Docker username parameter invalid"
            print_help_and_exit
        fi

        if [[ -z ${DOCKER_PASSWORD} ]]; then
            echo "Docker password parameter invalid"
            print_help_and_exit
        fi
    fi

    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_IMAGEVERSION} ]]; then
        if [ ! -z "${BUILD_BUILDNUMBER}" ]; then
            DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
        else
            echo "Docker image version not found."
            print_help_and_exit
        fi
    fi

    if [[ -z ${BUILD_BINARIESDIRECTORY} ]] || [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        echo "Bin directory does not exist or is invalid"
        print_help_and_exit
    fi

    PUBLISH_DIR=$BUILD_BINARIESDIRECTORY/publish

    if [[ ! -d $PUBLISH_DIR ]]; then
        echo "Publish directory does not exist or is invalid"
        print_help_and_exit
    fi


    EXE_DOCKER_DIR=$PUBLISH_DIR/$PROJECT/docker
    if [[ -z ${EXE_DOCKER_DIR} ]] || [[ ! -d ${EXE_DOCKER_DIR} ]]; then
        echo "No docker directory for $PROJECT at $EXE_DOCKER_DIR"
        print_help_and_exit
    fi

    DOCKERFILE="$EXE_DOCKER_DIR/linux/$ARCH/Dockerfile"
    if [[ ! -f $DOCKERFILE ]]; then
        echo "No Dockerfile at $DOCKERFILE"
        print_help_and_exit
    fi
}

###############################################################################
# Build docker image and push it to private repo
#
#   @param[1] - imagename; Name of the docker edge image to publish; Required;
#   @param[2] - arch; Arch of base image; Required;
#   @param[3] - dockerfile; Path to the dockerfile; Optional;
#               Leave as "" and defaults will be chosen.
#   @param[4] - context_path; docker context path; Required;
#   @param[5] - build_args; docker context path; Optional;
#               Leave as "" and no build args will be supplied.
###############################################################################
docker_build_and_tag_and_push()
{
    imagename="$1"
    arch="$2"
    dockerfile="$3"
    context_path="$4"
    build_args="${@:5}"

    if [ -z "${imagename}" ] || [ -z "${arch}" ] || [ -z "${context_path}" ]; then
        echo "Error: Arguments are invalid [$imagename] [$arch] [$context_path]"
        exit 1
    fi

    echo "Building and pushing Docker image $imagename for $arch"
    docker_build_cmd="docker build --no-cache"
    docker_build_cmd+=" -t $DOCKER_REGISTRY/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
    if [ ! -z "${dockerfile}" ]; then
        docker_build_cmd+=" --file $dockerfile"
    fi
    docker_build_cmd+=" $build_args $context_path"
    
    echo "Running... $docker_build_cmd"

    $docker_build_cmd

    if [ $? -ne 0 ]; then
        echo "Docker build failed with exit code $?"
        exit 1
    fi

    if [ $SKIP_PUSH -eq 0 ]; then
        docker_push_cmd="docker push $DOCKER_REGISTRY/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
        echo "Running... $docker_push_cmd"
        $docker_push_cmd
        if [ $? -ne 0 ]; then
            echo "Docker push failed with exit code $?"
            exit 1
        fi
    fi

    return $?
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"

# log in to container registry
if [ $SKIP_PUSH -eq 0 ]; then
    docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
    if [ $? -ne 0 ]; then
        echo "Docker login failed!"
        exit 1
    fi
fi

build_args=( "EXE_DIR=." )
[ -z "$BASE_TAG" ] || build_args+=( "base_tag=$BASE_TAG" )

# push image
docker_build_and_tag_and_push \
    "$DOCKER_IMAGENAME" \
    "$ARCH" \
    "$DOCKERFILE" \
    "$PUBLISH_DIR/$PROJECT" \
    "${build_args[@]/#/--build-arg buildno=$BUILD_BUILDNUMBER --build-arg }"
[ $? -eq 0 ] || exit $?

echo "Done building and pushing Docker image $DOCKER_IMAGENAME for $PROJECT"

[ $? -eq 0 ] || exit $?
