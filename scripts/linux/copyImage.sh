#!/bin/bash

###############################################################################
# This script copies a docker image from one repository to another within a
# registry, or from one tag to another with a repository. It assumes that the
# caller is logged into the registry.
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
DST_REPO=
DST_TAG=
REGISTRY=
SRC_REF=
SRC_REPO=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " --dst-repo           Destination repository"
    echo " --dst-tag            Destination tag"
    echo " --registry           Target image registry"
    echo " --src-ref            Source tag or digest"
    echo " --src-repo           Source repository"
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
            DST_REPO="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DST_TAG="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            SRC_REF="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            SRC_REPO="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--dst-repo") save_next_arg=1;;
                "--dst-tag" ) save_next_arg=2;;
                "--registry") save_next_arg=3;;
                "--src-ref" ) save_next_arg=4;;
                "--src-repo") save_next_arg=5;;
                "-h" | "--help" ) usage;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "$DST_REPO" ]]; then
        echo "Required parameter --dst-repo not found"
        print_help_and_exit
    fi

    if [[ -z "$DST_TAG" ]]; then
        echo "Required parameter --dst-tag not found"
        print_help_and_exit
    fi

    if [[ -z "$REGISTRY" ]]; then
        echo "Required parameter --registry not found"
        print_help_and_exit
    fi

    if [[ -z "$SRC_REF" ]]; then
        echo "Required parameter --src-ref not found"
        print_help_and_exit
    fi

    if [[ -z "$SRC_REPO" ]]; then
        echo "Required parameter --src-repo not found"
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

if [[ "${SRC_REF:0:7}" == "sha256:" ]]; then
    ref="@$SRC_REF"
else
    ref=":$SRC_REF"
fi

echo "Copy $REPOSITORY/$SRC_REPO$ref to $REPOSITORY/$DST_REPO:$DST_TAG"

source "$SCRIPT_DIR/manifest-tools.sh"
copy_manifest
