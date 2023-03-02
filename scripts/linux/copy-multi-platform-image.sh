#!/bin/bash

###############################################################################
# This script copies a docker multi-platform image from one repository to
# another within the given registry. This includes copying any platform-specific
# images, as well as any additional tags supplied by the caller. It assumes that
# the caller is logged into the registry.
#
# For example, if the script is called with the following arguments:
#
#   REGISTRY=registry
#   REPO_SRC=src/repo
#   REPO_DST=dst/repo
#   TAG=1.2.3
#   TAGS_EXTRA='["1.2","latest"]'
#
# ...then the following copy operations will take place:
#
#   registry/src/repo:1.2.3-linux-amd64   => registry/dst/repo:1.2.3-linux-amd64
#   registry/src/repo:1.2.3-linux-arm32v7 => registry/dst/repo:1.2.3-linux-arm32v7
#   registry/src/repo:1.2.3-linux-arm64v8 => registry/dst/repo:1.2.3-linux-arm32v7
#   registry/src/repo:1.2.3               => registry/dst/repo:1.2.3
#   registry/src/repo:1.2.3               => registry/dst/repo:1.2
#   registry/src/repo:1.2.3               => registry/dst/repo:latest
#
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
REGISTRY=
REPO_DST=
REPO_SRC=
TAG=
TAGS_EXTRA=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " --registry           Image registry (source and destination)"
    echo " --repo-dst           Destination repository"
    echo " --repo-src           Source repository"
    echo " --tag                Tag (soure and destination)"
    echo " --tags-extra         Optional JSON array of tags to add to the destination image"
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
            TAGS_EXTRA="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--registry" ) save_next_arg=1;;
                "--repo-dst" ) save_next_arg=2;;
                "--repo-src" ) save_next_arg=3;;
                "--tag" ) save_next_arg=4;;
                "--tags-extra" ) save_next_arg=5;;
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

    if [[ -n "$TAGS_EXTRA" ]] && [[ $(echo "$TAGS_EXTRA" | jq -r '. | type') != 'array' ]]; then
        echo 'The value of --tags-extra must be a JSON array'
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

source "$SCRIPT_DIR/manifest-tools.sh"

platform_tags=( "$TAG-linux-amd64" "$TAG-linux-arm64v8" "$TAG-linux-arm32v7" )

# first, copy the platform-specific images from source to destination repositories
for tag in ${$platform_tags[@]}
do
    echo "Copy '$REGISTRY/$REPO_SRC:$tag' to '$REGISTRY/$REPO_DST:$tag'"
    SRC_TAG="$tag" TAG_DST="$tag" copy_manifest
done

# next, copy the source repo's multi-platform image into the given tags in the destination repo
multi_platform_tags=( $(echo "$TAGS_EXTRA" | jq -r --arg version "$TAG" '. + [ $version ] | join("\n")') )

for tag in ${multi_platform_tags[@]}
do
    echo "Copy '$REGISTRY/$REPO_SRC:$TAG' to '$REGISTRY/$REPO_DST:$tag'"
    SRC_TAG="$TAG" TAG_DST="$tag" copy_manifest
done
