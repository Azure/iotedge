#!/bin/bash

###############################################################################
# This script copies a docker multi-platform image from one repository to
# another within the given registry. This includes copying any platform-specific
# images, as well as any additional tags supplied by the caller. It assumes that
# the caller is logged into the registry.
#
# For example, if registry/src/repo:1.2.3 is a multi-platform image built for
# {linux/amd64, linux/arm/v7, linux/arm64}, and the script is called with the
# following arguments:
#
#   REGISTRY=registry
#   REPO_SRC=src/repo
#   REPO_DST=dst/repo
#   TAG=1.2.3
#   TAGS_ADD='["1.2","latest"]'
#
# ...then the script will discover the digests of each platform-specific image
# (e.g., sha256:aaa, sha256:bbb, sha256:ccc respectively), and will perform the
# following copy operations:
#
#   registry/src/repo@sha256:aaa    => registry/dst/repo:1.2.3-linux-amd64
#   registry/src/repo@sha256:bbb    => registry/dst/repo:1.2.3-linux-arm32v7
#   registry/src/repo@sha256:ccc    => registry/dst/repo:1.2.3-linux-arm32v7
#   registry/src/repo:1.2.3         => registry/dst/repo:1.2.3
#   registry/src/repo:1.2.3         => registry/dst/repo:1.2
#   registry/src/repo:1.2.3         => registry/dst/repo:latest
#
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
SCRIPT_NAME=$(basename $0)
REGISTRY=
REPO_DST=
REPO_SRC=
TAG=
TAGS_ADD=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " --registry           Image registry"
    echo " --repo-dst           Destination repository"
    echo " --repo-src           Source repository"
    echo " --tag                Tag to copy"
    echo " --tags-add           Optional JSON array of tags to add to the destination image"
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
            REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            REPO_DST="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            REPO_SRC="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            TAG="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            TAGS_ADD="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--registry" ) save_next_arg=1;;
                "--repo-dst" ) save_next_arg=2;;
                "--repo-src" ) save_next_arg=3;;
                "--tag" ) save_next_arg=4;;
                "--tags-add" ) save_next_arg=5;;
                "-h" | "--help" ) usage;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "$REGISTRY" ]]; then
        echo "Required parameter --registry not found"
        print_help_and_exit
    fi

    if [[ -z "$REPO_DST" ]]; then
        echo "Required parameter --repo-dst not found"
        print_help_and_exit
    fi

    if [[ -z "$REPO_SRC" ]]; then
        echo "Required parameter --repo-src not found"
        print_help_and_exit
    fi

    if [[ -z "$TAG" ]]; then
        echo "Required parameter --tag not found"
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

source "$SCRIPT_DIR/manifest-tools.sh"
copy_manifest_list
