#!/bin/bash

set -e

SCRIPT_NAME=$(basename "$0")
DIR=$(cd "$(dirname "$0")"/.. && pwd)
DEFAULT_DOCKER_NAMESPACE="microsoft"
DOCKER_NAMESPACE=$DEFAULT_DOCKER_NAMESPACE
DOCKERFILE=
SKIP_PUSH=0

usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -i, --image-name     Image name (e.g. snitcher)"
    echo " -P, --project        Project to build image for. Must be 'snitcher' or 'prep-mail'"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -u, --username       Docker Registry Username"
    echo " -p, --password       Docker Username's password"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -v, --image-version  Docker Image Version."
    echo "--skip-push           Build images, but don't push them"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

process_args()
{
    save_next_arg=0
    for arg in "$@"
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
            PROJECT="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "-P" | "--project" ) save_next_arg=5;;
                "-i" | "--image-name" ) save_next_arg=6;;
                "-n" | "--namespace" ) save_next_arg=7;;
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
        echo "Docker image version not found."
        print_help_and_exit
    fi

    DOCKERFILE="$DIR/$PROJECT/docker/linux/amd64/Dockerfile"
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
    build_args="${*:5}"

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

    if ! $docker_build_cmd; then
        echo "Docker build failed with exit code $?"
        exit 1
    fi

    if [ $SKIP_PUSH -eq 0 ]; then
        docker_push_cmd="docker push $DOCKER_REGISTRY/$DOCKER_NAMESPACE/$imagename:$DOCKER_IMAGEVERSION-linux-$arch"
        echo "Running... $docker_push_cmd"
        if ! $docker_push_cmd; then
            echo "Docker push failed with exit code $?"
            exit 1
        fi
    fi
}

process_args "$@"

# log in to container registry
if [ $SKIP_PUSH -eq 0 ]; then
    if ! docker login "$DOCKER_REGISTRY" -u "$DOCKER_USERNAME" -p "$DOCKER_PASSWORD"; then
        echo "Docker login failed!"
        exit 1
    fi
fi

# push image
docker_build_and_tag_and_push \
    "$DOCKER_IMAGENAME" \
    "amd64" \
    "$DOCKERFILE" \
    "$DIR" \
    ""

echo "Done building and pushing Docker image $DOCKER_IMAGENAME for $PROJECT"

exit $?
