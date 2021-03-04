#!/bin/bash

###############################################################################
# This Script builds a specific Helm chart. It expects a to find the charts in
# <REPO>/kubernetes/charts/<CHART>
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename "$0")
DIR=$(cd "$(dirname "$0")" && pwd)
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
CHART=
CHART_IMAGENAME=
DEFAULT_CHART_NAMESPACE="microsoft"
CHART_NAMESPACE=${DEFAULT_CHART_NAMESPACE}
SKIP_PUSH=0
export HELM_EXPERIMENTAL_OCI=1

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -C, --chart          Chart name/chart directory"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -u, --username       Docker Registry Username"
    echo " -p, --password       Docker Username's password"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_CHART_NAMESPACE)"
    echo " -i, --image-Name     Helm Chart Image Name."
    echo " -v, --image-version  Helm Chart Image Version. Either use this option or set env variable BUILD_BUILDNUMBER"
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
    for arg in "$@"
    do
        if [[ ${save_next_arg} -eq 1 ]]; then
            CHART_REGISTRY="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 2 ]]; then
            CHART_USERNAME="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 3 ]]; then
            CHART_PASSWORD="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 4 ]]; then
            CHART_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            CHART="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            CHART_NAMESPACE="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 7 ]]; then
            CHART_IMAGENAME="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "-C" | "--chart" ) save_next_arg=5;;
                "-n" | "--namespace" ) save_next_arg=6;;
                "-i" | "--image-name" ) save_next_arg=7;;
                "--skip-push" ) SKIP_PUSH=1 ;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${CHART_REGISTRY} ]]; then
        echo "Registry parameter invalid"
        print_help_and_exit
    fi

    if [[ ${SKIP_PUSH} -eq 0 ]]; then
        if [[ -z ${CHART_USERNAME} ]]; then
            echo "Docker username parameter invalid"
            print_help_and_exit
        fi

        if [[ -z ${CHART_PASSWORD} ]]; then
            echo "Docker password parameter invalid"
            print_help_and_exit
        fi
    fi

    if [[ -z ${CHART} ]]; then
        echo "Helm Chart name parameter invalid"
        print_help_and_exit
    fi

    if [[ ! -d ${BUILD_REPOSITORY_LOCALPATH}/kubernetes/charts/${CHART} ]]; then
        echo "Chart not found in ${BUILD_REPOSITORY_LOCALPATH}/kubernetes/charts/${CHART}"
        print_help_and_exit
    fi

    if [[ -z "$CHART_IMAGENAME" ]]; then
        echo "Chart image name not found"
        print_help_and_exit
    fi

    if [[ -z ${CHART_IMAGEVERSION} ]]; then
        if [[ -n "${BUILD_BUILDNUMBER}" ]]; then
            CHART_IMAGEVERSION=${BUILD_BUILDNUMBER}
        else
            echo "Docker image version not found."
            print_help_and_exit
        fi
    fi
}

###############################################################################
# Build Helm chart image and push it to private repo
#
#   @param[1] - imagename; Name of the docker edge image to publish; Required;
#   @param[2] - chart_path; Required;
#
###############################################################################
helm_save_tag_and_push()
{
    imagename="$1"
    chart_path="$2"

    if [[ -z "${imagename}" ]] || [[ -z "${chart_path}" ]]; then
        echo "Error: Arguments are invalid [$imagename] [$chart_path]"
        exit 1
    fi

    echo "Saving and pushing Helm chart $imagename"
    cd ${chart_path}
    full_imagename="$CHART_REGISTRY/$CHART_NAMESPACE/$imagename:$CHART_IMAGEVERSION"
    helm_save_cmd="helm chart save ${chart_path} ${full_imagename}"
    helm_push_cmd="helm chart push ${full_imagename}"
    
    echo "Running... $helm_save_cmd"

    ${helm_save_cmd}

    if [[ $? -ne 0 ]]; then
        echo "Helm chart save failed with exit code $?"
        exit 1
    fi

    if [[ ${SKIP_PUSH} -eq 0 ]]; then
        echo "Running... $helm_push_cmd"
        ${helm_push_cmd}
        if [[ $? -ne 0 ]]; then
            echo "Helm chart push failed with exit code $?"
            exit 1
        fi
    fi

    return $?
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

# log in to container registry
if [[ ${SKIP_PUSH} -eq 0 ]]; then
    helm registry login "${CHART_REGISTRY}" -u "${CHART_USERNAME}" -p "${CHART_PASSWORD}"
    if [[ $? -ne 0 ]]; then
        echo "registry login failed!"
        exit 1
    fi
fi

# push image
helm_save_tag_and_push \
    "$CHART_IMAGENAME" \
    "${BUILD_REPOSITORY_LOCALPATH}/kubernetes/charts/${CHART}"
[[ $? -eq 0 ]] || exit $?

echo "Done creating and pushing Helm chart image $CHART_IMAGENAME for $CHART"

[[ $? -eq 0 ]] || exit $?
